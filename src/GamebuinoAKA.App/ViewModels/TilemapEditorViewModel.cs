using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebuinoAKA.App.Services;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using SkiaSharp;

namespace GamebuinoAKA.App.ViewModels
{
    public sealed partial class TilemapEditorViewModel : ViewModelBase
    {
        private readonly AssetService _asset;
        private readonly IFileDialogService _files;

        private TilemapAsset? _map;
        private SKBitmap? _tileset;

        [ObservableProperty] private Bitmap? _tilesetImage;
        [ObservableProperty] private Bitmap? _mapImage;
        [ObservableProperty] private int _tileWidth = 16;
        [ObservableProperty] private int _tileHeight = 16;
        [ObservableProperty] private int _mapColumns = 20;
        [ObservableProperty] private int _mapRows = 15;
        [ObservableProperty] private int _selectedTileIndex;
        [ObservableProperty] private Point _selectedCell = new(-1, -1);
        [ObservableProperty] private int _activeLayer;        // 0 = fond, 1 = premier plan
        [ObservableProperty] private int _tilesetZoom = 2;
        [ObservableProperty] private int _mapZoom = 2;
        [ObservableProperty] private string _exportedCode = string.Empty;
        [ObservableProperty] private string _status = "Importez un tileset, sélectionnez une tuile, peignez la carte.";

        public bool HasMap => _map != null;
        public int TilesetColumns => _map?.TilesetColumns ?? 0;

        public TilemapEditorViewModel(AssetService asset, IFileDialogService files)
        {
            _asset = asset; _files = files;
        }

        [RelayCommand]
        private async Task ImportTileset()
        {
            var path = await _files.OpenFileAsync("Importer un tileset",
                new[] { new FileFilter("Images", new[] { "png", "bmp" }) });
            if (path is null) return;
            try
            {
                var map = await _asset.ImportTilesetAsync(path, TileWidth, TileHeight);
                _map = map;
                MapColumns = map.MapColumns;
                MapRows = map.MapRows;
                _tileset?.Dispose();
                _tileset = _asset.UnpackToBitmap(map.TilesetPixels!, map.TilesetColumns * map.TileWidth,
                    map.TilesetRows * map.TileHeight, map.ColorFormat, false, map.TransparentKey);
                TilesetImage = SkiaImageBridge.ToAvalonia(_tileset);
                ComposeMap();
                OnPropertyChanged(nameof(HasMap));
                Status = $"Tileset : {map.TilesetColumns}×{map.TilesetRows} tuiles.";
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        private void SelectTile(Point cell)
        {
            if (_map is null || _map.TilesetColumns <= 0) return;
            int idx = (int)cell.Y * _map.TilesetColumns + (int)cell.X;
            SelectedTileIndex = idx;
            SelectedCell = cell;
        }

        [RelayCommand]
        private void PaintCell(Point cell)
        {
            if (_map is null) return;
            int c = (int)cell.X, r = (int)cell.Y;
            if (c < 0 || r < 0 || c >= _map.MapColumns || r >= _map.MapRows) return;
            var layer = ActiveLayer == 0 ? _map.BackgroundLayer : _map.ForegroundLayer;
            layer[r * _map.MapColumns + c] = (byte)SelectedTileIndex;
            ComposeMap();
        }

        [RelayCommand]
        private void ApplyMapSize()
        {
            if (_map is null) return;
            _map.MapColumns = Math.Max(1, MapColumns);
            _map.MapRows = Math.Max(1, MapRows);
            _map.InitializeLayers();
            ComposeMap();
            Status = $"Carte {_map.MapColumns}×{_map.MapRows}.";
        }

        [RelayCommand]
        private async Task ExportCpp()
        {
            if (_map is null) return;
            var path = await _files.SaveFileAsync("Exporter la tilemap en C++", "tilemap.h",
                new[] { new FileFilter("C/C++", new[] { "h" }) });
            if (path is null) return;
            try
            {
                var code = _asset.ExportTilemapToCpp(_map);
                ExportedCode = code;
                await Task.Run(() => File.WriteAllText(path, code));
                Status = "Exporté : " + path;
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        private async Task SaveProject()
        {
            if (_map is null) return;
            var path = await _files.SaveFileAsync("Enregistrer la tilemap", "tilemap.gbmap",
                new[] { new FileFilter("Projet tilemap", new[] { "gbmap" }) });
            if (path is null) return;
            try { _asset.SaveTilemap(_map, path); Status = "Enregistré : " + path; }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        private async Task OpenProject()
        {
            var path = await _files.OpenFileAsync("Ouvrir une tilemap",
                new[] { new FileFilter("Projet tilemap", new[] { "gbmap" }) });
            if (path is null) return;
            try
            {
                var map = _asset.LoadTilemap(path);
                _map = map;
                TileWidth = map.TileWidth; TileHeight = map.TileHeight;
                MapColumns = map.MapColumns; MapRows = map.MapRows;
                _tileset?.Dispose();
                if (map.TilesetPixels != null)
                {
                    _tileset = _asset.UnpackToBitmap(map.TilesetPixels, map.TilesetColumns * map.TileWidth,
                        map.TilesetRows * map.TileHeight, map.ColorFormat, false, map.TransparentKey);
                    TilesetImage = SkiaImageBridge.ToAvalonia(_tileset);
                }
                ComposeMap();
                OnPropertyChanged(nameof(HasMap));
                Status = "Tilemap ouverte : " + map.Name;
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        private void ComposeMap()
        {
            if (_map is null || _tileset is null) return;
            int tw = _map.TileWidth, th = _map.TileHeight, cols = _map.MapColumns, rows = _map.MapRows;
            int tsCols = Math.Max(1, _map.TilesetColumns);

            var info = new SKImageInfo(cols * tw, rows * th, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(20, 16, 40));

            for (int layer = 0; layer < 2; layer++)
            {
                var data = layer == 0 ? _map.BackgroundLayer : _map.ForegroundLayer;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        int t = data[r * cols + c];
                        if (layer == 1 && t == 0) continue;
                        int sc = t % tsCols, sr = t / tsCols;
                        var src = new SKRect(sc * tw, sr * th, sc * tw + tw, sr * th + th);
                        var dst = new SKRect(c * tw, r * th, c * tw + tw, r * th + th);
                        canvas.DrawBitmap(_tileset, src, dst);
                    }
            }

            using var img = surface.Snapshot();
            using var d = img.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream();
            d.SaveTo(ms); ms.Position = 0;
            MapImage = new Bitmap(ms);
        }
    }
}
