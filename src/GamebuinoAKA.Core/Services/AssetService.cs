using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;
using Newtonsoft.Json;
using SkiaSharp;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Pipeline d'assets portable (SkiaSharp) : import/crop/resize, empaquetage
    /// BGR565 (ordre lib AKA) avec couleur-clé de transparence, export C++ et
    /// formats ré-éditables .gbspr / .gbmap. Aucune dépendance System.Drawing / UI.
    /// </summary>
    public sealed class AssetService
    {
        private readonly ISettingsService _settings;

        public AssetService(ISettingsService settings)
        {
            _settings = settings;
        }

        public const string SpriteProjectExt = ".gbspr";
        public const string TilemapProjectExt = ".gbmap";

        // ── Conversion couleur (format-aware) ────────────────────────────────────

        /// <summary>Empaquette une couleur RVB en 16 bits selon le format.</summary>
        public static ushort Pack(byte r, byte g, byte b, ColorFormat fmt)
        {
            int r5 = r >> 3, g6 = g >> 2, b5 = b >> 3;
            return fmt == ColorFormat.Bgr565Aka
                ? (ushort)(r5 | (g6 << 5) | (b5 << 11))   // R bas, B haut (lib AKA)
                : (ushort)((r5 << 11) | (g6 << 5) | b5);  // R haut, B bas (standard)
        }

        public static ushort Pack(SKColor c, ColorFormat fmt) => Pack(c.Red, c.Green, c.Blue, fmt);

        /// <summary>Dépaquette un 16 bits en (r,g,b) selon le format.</summary>
        public static (byte r, byte g, byte b) Unpack(ushort v, ColorFormat fmt)
        {
            int r5, g6, b5;
            if (fmt == ColorFormat.Bgr565Aka)
            {
                r5 = v & 0x1F; g6 = (v >> 5) & 0x3F; b5 = (v >> 11) & 0x1F;
            }
            else
            {
                r5 = (v >> 11) & 0x1F; g6 = (v >> 5) & 0x3F; b5 = v & 0x1F;
            }
            return ((byte)((r5 << 3) | (r5 >> 2)),
                    (byte)((g6 << 2) | (g6 >> 4)),
                    (byte)((b5 << 3) | (b5 >> 2)));
        }

        public static SKColor UnpackColor(ushort v, ColorFormat fmt)
        {
            var (r, g, b) = Unpack(v, fmt);
            return new SKColor(r, g, b, 255);
        }

        // ── Outils image (SkiaSharp) ─────────────────────────────────────────────

        /// <summary>Charge une image décodée (PNG/BMP/JPG…).</summary>
        public SKBitmap LoadArgb(string path)
            => SKBitmap.Decode(path) ?? throw new InvalidOperationException($"Image illisible : {path}");

        /// <summary>Rogne une région (coordonnées image, clampée).</summary>
        public SKBitmap Crop(SKBitmap src, int x, int y, int w, int h)
        {
            var rect = SKRectI.Intersect(SKRectI.Create(x, y, w, h),
                                         new SKRectI(0, 0, src.Width, src.Height));
            if (rect.Width <= 0 || rect.Height <= 0)
                throw new ArgumentException("Sélection vide.");

            using var subset = new SKBitmap();
            if (!src.ExtractSubset(subset, rect))
                throw new InvalidOperationException("Rognage impossible.");
            return subset.Copy();   // copie indépendante (ExtractSubset partage les pixels)
        }

        /// <summary>Redimensionne (bicubique haute qualité si smooth, sinon au plus proche).</summary>
        public SKBitmap Resize(SKBitmap src, int w, int h, bool smooth)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            var info = new SKImageInfo(w, h, src.ColorType, src.AlphaType);
            var dst = src.Resize(info, smooth ? SKFilterQuality.High : SKFilterQuality.None);
            return dst ?? throw new InvalidOperationException("Redimensionnement impossible.");
        }

        /// <summary>Empaquète un bitmap en uint16 (alpha &lt; 128 → couleur-clé).</summary>
        public static ushort[] PackBitmap(SKBitmap bmp, ColorFormat fmt, ushort transparentKey)
        {
            int w = bmp.Width, h = bmp.Height;
            var outData = new ushort[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    outData[y * w + x] = c.Alpha < 128 ? transparentKey : Pack(c, fmt);
                }
            return outData;
        }

        /// <summary>Reconstruit un bitmap éditable depuis des pixels packés.</summary>
        public SKBitmap UnpackToBitmap(ushort[] data, int w, int h, ColorFormat fmt,
            bool useTransparency, ushort key)
        {
            var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    ushort v = (y * w + x) < data.Length ? data[y * w + x] : (ushort)0;
                    bmp.SetPixel(x, y, useTransparency && v == key
                        ? SKColors.Transparent
                        : UnpackColor(v, fmt));
                }
            return bmp;
        }

        /// <summary>Construit un SpriteAsset packé à partir d'un bitmap de travail.</summary>
        public SpriteAsset BuildSprite(SKBitmap bmp, string name, ColorFormat fmt,
            ushort key, bool useTransparency, int frameW = 0, int frameH = 0, int frameCount = 1)
        {
            if (frameW <= 0) frameW = bmp.Width;
            if (frameH <= 0) frameH = bmp.Height;
            return new SpriteAsset
            {
                Name = string.IsNullOrEmpty(name) ? "sprite" : name,
                Width = bmp.Width,
                Height = bmp.Height,
                FrameWidth = frameW,
                FrameHeight = frameH,
                FrameCount = Math.Max(1, frameCount),
                ColorFormat = fmt,
                TransparentKey = key,
                UseTransparency = useTransparency,
                PixelData = PackBitmap(bmp, fmt, key)
            };
        }

        // ── Import (asynchrone) ──────────────────────────────────────────────────

        public Task<SpriteAsset> ImportSpriteAsync(string imagePath) =>
            Task.Run(() =>
            {
                using var bmp = LoadArgb(imagePath);
                var fmt = _settings.Settings.DefaultColorFormat;
                var key = _settings.Settings.DefaultTransparentKey;
                var a = BuildSprite(bmp, Path.GetFileNameWithoutExtension(imagePath), fmt, key, true);
                a.SourcePath = imagePath;
                return a;
            });

        public Task<SpriteAsset> ImportSpritesheetAsync(string imagePath, int frameWidth, int frameHeight) =>
            Task.Run(() =>
            {
                using var bmp = LoadArgb(imagePath);
                var fmt = _settings.Settings.DefaultColorFormat;
                var key = _settings.Settings.DefaultTransparentKey;
                int cols = frameWidth > 0 ? bmp.Width / frameWidth : 1;
                int rows = frameHeight > 0 ? bmp.Height / frameHeight : 1;
                var a = BuildSprite(bmp, Path.GetFileNameWithoutExtension(imagePath), fmt, key, true,
                    frameWidth, frameHeight, Math.Max(1, cols * rows));
                a.SourcePath = imagePath;
                return a;
            });

        public Task<TilemapAsset> ImportTilesetAsync(string imagePath, int tileWidth, int tileHeight) =>
            Task.Run(() =>
            {
                using var bmp = LoadArgb(imagePath);
                var fmt = _settings.Settings.DefaultColorFormat;
                var key = _settings.Settings.DefaultTransparentKey;
                var a = new TilemapAsset
                {
                    Name = Path.GetFileNameWithoutExtension(imagePath),
                    TilesetPath = imagePath,
                    TileWidth = tileWidth,
                    TileHeight = tileHeight,
                    TilesetColumns = tileWidth > 0 ? bmp.Width / tileWidth : 1,
                    TilesetRows = tileHeight > 0 ? bmp.Height / tileHeight : 1,
                    ColorFormat = fmt,
                    TransparentKey = key,
                    UseTransparency = true,
                    TilesetPixels = PackBitmap(bmp, fmt, key)
                };
                a.InitializeLayers();
                return a;
            });

        // ── Export C++ ───────────────────────────────────────────────────────────

        public string ExportSpriteToCpp(SpriteAsset a)
        {
            if (a.PixelData is null) return "// Pas de données pixel";
            string name = SanitizeName(a.Name);
            string fmtLabel = a.ColorFormat == ColorFormat.Bgr565Aka ? "BGR565 (ordre lib AKA)" : "RGB565 standard";

            var sb = new StringBuilder(a.PixelData.Length * 8 + 512);
            sb.Append("// Sprite: ").Append(a.Name)
              .Append(" (").Append(a.Width).Append('x').Append(a.Height).Append(")\n");
            sb.Append("// Frames: ").Append(a.FrameCount)
              .Append("  Frame: ").Append(a.FrameWidth).Append('x').Append(a.FrameHeight).Append('\n');
            sb.Append("// Format couleur: ").Append(fmtLabel).Append('\n');
            if (a.UseTransparency)
            {
                sb.Append("// Transparence: couleur-clé 0x").Append(a.TransparentKey.ToString("X4"))
                  .Append(" — use_transparency=true dans graphics_draw_bitmap565()\n");
                sb.Append("#define ").Append(name).Append("_TRANSPARENT 0x")
                  .Append(a.TransparentKey.ToString("X4")).Append('\n');
            }

            AppendHexBlock(sb, $"const uint16_t {name}[] PROGMEM", a.PixelData, 16);
            sb.Append("const uint16_t ").Append(name).Append("_width  = ").Append(a.FrameWidth).Append(";\n");
            sb.Append("const uint16_t ").Append(name).Append("_height = ").Append(a.FrameHeight).Append(";\n");
            sb.Append("const uint8_t  ").Append(name).Append("_frames = ").Append(a.FrameCount).Append(";\n");
            return sb.ToString();
        }

        public string ExportTilemapToCpp(TilemapAsset a)
        {
            string name = SanitizeName(a.Name);
            var sb = new StringBuilder(a.TotalTiles * 8 + 512);
            sb.Append("// Tilemap: ").Append(a.Name)
              .Append(" (").Append(a.MapColumns).Append('x').Append(a.MapRows).Append(" tuiles)\n");
            sb.Append("// Taille tuile: ").Append(a.TileWidth).Append('x').Append(a.TileHeight).Append('\n');
            if (a.UseTransparency)
                sb.Append("#define ").Append(name).Append("_TRANSPARENT 0x")
                  .Append(a.TransparentKey.ToString("X4")).Append('\n');
            sb.Append('\n');

            AppendByteRows(sb, $"const uint8_t {name}_bg[] PROGMEM", a.BackgroundLayer, a.MapColumns);
            AppendByteRows(sb, $"const uint8_t {name}_fg[] PROGMEM", a.ForegroundLayer, a.MapColumns);
            sb.Append("const uint8_t ").Append(name).Append("_cols = ").Append(a.MapColumns).Append(";\n");
            sb.Append("const uint8_t ").Append(name).Append("_rows = ").Append(a.MapRows).Append(";\n");
            return sb.ToString();
        }

        // ── Sauvegarde ré-éditable (JSON) ────────────────────────────────────────

        public void SaveSprite(SpriteAsset a, string path) =>
            File.WriteAllText(path, JsonConvert.SerializeObject(a, Formatting.Indented));

        public SpriteAsset LoadSprite(string path) =>
            JsonConvert.DeserializeObject<SpriteAsset>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Fichier sprite illisible.");

        public void SaveTilemap(TilemapAsset a, string path) =>
            File.WriteAllText(path, JsonConvert.SerializeObject(a, Formatting.Indented));

        public TilemapAsset LoadTilemap(string path) =>
            JsonConvert.DeserializeObject<TilemapAsset>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Fichier tilemap illisible.");

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static readonly char[] HexDigits = "0123456789ABCDEF".ToCharArray();

        private static void AppendHexBlock(StringBuilder sb, string decl, ushort[] data, int perRow)
        {
            sb.Append(decl).Append(" = {\n");
            for (int i = 0; i < data.Length; i++)
            {
                if (i % perRow == 0) sb.Append("    ");
                ushort v = data[i];
                sb.Append("0x")
                  .Append(HexDigits[(v >> 12) & 0xF]).Append(HexDigits[(v >> 8) & 0xF])
                  .Append(HexDigits[(v >> 4) & 0xF]).Append(HexDigits[v & 0xF]);
                if (i < data.Length - 1) sb.Append(", ");
                if ((i + 1) % perRow == 0) sb.Append('\n');
            }
            if (data.Length % perRow != 0) sb.Append('\n');
            sb.Append("};\n\n");
        }

        private static void AppendByteRows(StringBuilder sb, string decl, byte[] data, int cols)
        {
            sb.Append(decl).Append(" = {\n");
            for (int i = 0; i < data.Length; i++)
            {
                if (cols > 0 && i % cols == 0) sb.Append("    ");
                byte v = data[i];
                sb.Append("0x").Append(HexDigits[(v >> 4) & 0xF]).Append(HexDigits[v & 0xF]);
                if (i < data.Length - 1) sb.Append(", ");
                if (cols > 0 && (i + 1) % cols == 0) sb.Append('\n');
            }
            sb.Append("\n};\n\n");
        }

        private static string SanitizeName(string name) =>
            Regex.Replace(string.IsNullOrEmpty(name) ? "asset" : name, @"[^a-zA-Z0-9_]", "_");
    }
}
