using System;
using System.IO;
using System.Linq;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using SkiaSharp;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class AssetServiceTests
    {
        private static AssetService NewService()
            => new AssetService(new SettingsService(new TempPlatformPaths()));

        private static SKBitmap MakeBitmap(int w, int h)
            => new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));

        // ── Vecteurs BGR565 (ordre lib AKA) ──────────────────────────────────────
        [Fact]
        public void Pack_Bgr565Aka_MatchesReferenceVectors()
        {
            Assert.Equal(0x001F, AssetService.Pack(255, 0, 0, ColorFormat.Bgr565Aka));   // rouge
            Assert.Equal(0xF800, AssetService.Pack(0, 0, 255, ColorFormat.Bgr565Aka));   // bleu
            Assert.Equal(0x07E0, AssetService.Pack(0, 255, 0, ColorFormat.Bgr565Aka));   // vert
            Assert.Equal(0xFFFF, AssetService.Pack(255, 255, 255, ColorFormat.Bgr565Aka)); // blanc
            Assert.Equal(0x0000, AssetService.Pack(0, 0, 0, ColorFormat.Bgr565Aka));     // noir
        }

        [Fact]
        public void Pack_Rgb565Std_SwapsRedBlue()
        {
            Assert.Equal(0xF800, AssetService.Pack(255, 0, 0, ColorFormat.Rgb565Std));   // rouge en bits hauts
            Assert.Equal(0x001F, AssetService.Pack(0, 0, 255, ColorFormat.Rgb565Std));   // bleu en bits bas
            Assert.Equal(0x07E0, AssetService.Pack(0, 255, 0, ColorFormat.Rgb565Std));   // vert (symétrique)
        }

        [Fact]
        public void Unpack_RoundTrips_PureColors()
        {
            Assert.Equal(((byte)255, (byte)0, (byte)0), AssetService.Unpack(0x001F, ColorFormat.Bgr565Aka));
            Assert.Equal(((byte)0, (byte)0, (byte)255), AssetService.Unpack(0xF800, ColorFormat.Bgr565Aka));
        }

        // ── Transparence par alpha ───────────────────────────────────────────────
        [Fact]
        public void PackBitmap_AlphaThreshold_UsesKeyBelow128()
        {
            using var bmp = MakeBitmap(3, 1);
            bmp.SetPixel(0, 0, new SKColor(255, 0, 0, 255)); // opaque → rouge
            bmp.SetPixel(1, 0, new SKColor(0, 0, 0, 127));   // alpha 127 → clé
            bmp.SetPixel(2, 0, new SKColor(0, 0, 0, 128));   // alpha 128 → couleur (noir)

            var packed = AssetService.PackBitmap(bmp, ColorFormat.Bgr565Aka, 0xF81F);

            Assert.Equal(0x001F, packed[0]);
            Assert.Equal(0xF81F, packed[1]);
            Assert.Equal(0x0000, packed[2]);
        }

        // ── Crop / resize ────────────────────────────────────────────────────────
        [Fact]
        public void Crop_ReturnsSubregion()
        {
            var svc = NewService();
            using var bmp = MakeBitmap(4, 4);
            bmp.SetPixel(1, 1, new SKColor(10, 20, 30, 255));

            using var cropped = svc.Crop(bmp, 1, 1, 2, 2);

            Assert.Equal(2, cropped.Width);
            Assert.Equal(2, cropped.Height);
            var c = cropped.GetPixel(0, 0);
            Assert.Equal(10, c.Red);
            Assert.Equal(20, c.Green);
            Assert.Equal(30, c.Blue);
        }

        [Fact]
        public void Resize_ChangesDimensions()
        {
            var svc = NewService();
            using var bmp = MakeBitmap(4, 4);
            using var small = svc.Resize(bmp, 2, 2, smooth: false);
            Assert.Equal(2, small.Width);
            Assert.Equal(2, small.Height);
        }

        // ── BuildSprite + .gbspr ─────────────────────────────────────────────────
        [Fact]
        public void BuildSprite_PacksAllPixels()
        {
            var svc = NewService();
            using var bmp = MakeBitmap(2, 2);
            var a = svc.BuildSprite(bmp, "test", ColorFormat.Bgr565Aka, 0xF81F, true);
            Assert.Equal(2, a.Width);
            Assert.Equal(4, a.PixelData!.Length);
        }

        [Fact]
        public void SaveLoad_Gbspr_RoundTrips()
        {
            var svc = NewService();
            using var bmp = MakeBitmap(2, 2);
            bmp.SetPixel(0, 0, new SKColor(255, 0, 0, 255));
            var a = svc.BuildSprite(bmp, "sp", ColorFormat.Bgr565Aka, 0xF81F, true, 0, 0, 1);

            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + AssetService.SpriteProjectExt);
            try
            {
                svc.SaveSprite(a, path);
                var b = svc.LoadSprite(path);
                Assert.Equal(a.Width, b.Width);
                Assert.Equal(a.ColorFormat, b.ColorFormat);
                Assert.Equal(a.TransparentKey, b.TransparentKey);
                Assert.Equal(a.PixelData!, b.PixelData!);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ExportSprite_IsDeterministic_AndContainsHex()
        {
            var svc = NewService();
            using var bmp = MakeBitmap(1, 1);
            bmp.SetPixel(0, 0, new SKColor(255, 0, 0, 255));
            var a = svc.BuildSprite(bmp, "rouge", ColorFormat.Bgr565Aka, 0xF81F, false);

            var c1 = svc.ExportSpriteToCpp(a);
            var c2 = svc.ExportSpriteToCpp(a);
            Assert.Equal(c1, c2);                    // déterministe
            Assert.Contains("const uint16_t rouge[]", c1);
            Assert.Contains("0x001F", c1);           // rouge packé en BGR565
        }

        // ── Tilemap ──────────────────────────────────────────────────────────────
        [Fact]
        public void ExportTilemap_ContainsLayersAndDims()
        {
            var svc = NewService();
            var t = new TilemapAsset { Name = "map", MapColumns = 2, MapRows = 2 };
            t.InitializeLayers();
            t.BackgroundLayer[0] = 3;

            var code = svc.ExportTilemapToCpp(t);
            Assert.Contains("map_bg[]", code);
            Assert.Contains("map_fg[]", code);
            Assert.Contains("map_cols = 2", code);
        }

        [Fact]
        public void SaveLoad_Gbmap_RoundTrips()
        {
            var svc = NewService();
            var t = new TilemapAsset { Name = "m", MapColumns = 3, MapRows = 2 };
            t.InitializeLayers();
            t.BackgroundLayer[4] = 7;

            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + AssetService.TilemapProjectExt);
            try
            {
                svc.SaveTilemap(t, path);
                var u = svc.LoadTilemap(path);
                Assert.Equal(t.MapColumns, u.MapColumns);
                Assert.Equal(t.BackgroundLayer, u.BackgroundLayer);
            }
            finally { File.Delete(path); }
        }
    }
}
