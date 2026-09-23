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
    public sealed partial class SpriteEditorViewModel : ViewModelBase
    {
        private const long ScreenPixels = 320L * 240L;
        private const int PreviewCap = 200_000;

        private readonly AssetService _asset;
        private readonly IFileDialogService _files;
        private readonly IDialogService _dialogs;

        private SKBitmap? _working;
        private SKBitmap? _original;
        private ColorFormat _fmt;
        private ushort _key;
        private bool _isSheet;

        [ObservableProperty] private Bitmap? _displayImage;
        [ObservableProperty] private int _zoom = 4;
        [ObservableProperty] private string _dimensions = "—";
        [ObservableProperty] private bool _selectionMode = true;
        [ObservableProperty] private Rect _selection;
        [ObservableProperty] private int _targetWidth = 32;
        [ObservableProperty] private int _targetHeight = 32;
        [ObservableProperty] private bool _keepAspect = true;
        [ObservableProperty] private bool _smooth = true;
        [ObservableProperty] private int _frameWidth = 16;
        [ObservableProperty] private int _frameHeight = 16;
        [ObservableProperty] private bool _useTransparency = true;
        [ObservableProperty] private string _exportedCode = string.Empty;
        [ObservableProperty] private string _status = "Importez une image, sélectionnez une zone, rognez/réduisez, puis Convertir.";

        public bool HasImage => _working != null;
        public string TransparentKeyHex => "0x" + _key.ToString("X4");
        public string FrameInfo
        {
            get
            {
                if (!_isSheet || _working is null || FrameWidth <= 0 || FrameHeight <= 0) return "1 frame";
                int c = _working.Width / FrameWidth, r = _working.Height / FrameHeight;
                return $"{c}×{r} = {Math.Max(1, c * r)} frames de {FrameWidth}×{FrameHeight}";
            }
        }

        public SpriteEditorViewModel(AssetService asset, ISettingsService settings,
            IFileDialogService files, IDialogService dialogs)
        {
            _asset = asset; _files = files; _dialogs = dialogs;
            _fmt = settings.Settings.DefaultColorFormat;
            _key = settings.Settings.DefaultTransparentKey;
        }

        partial void OnTargetWidthChanged(int value)
        {
            if (KeepAspect && _working != null && _working.Width > 0 && !_adjusting)
            { _adjusting = true; TargetHeight = Math.Max(1, (int)Math.Round(value * (double)_working.Height / _working.Width)); _adjusting = false; }
        }
        partial void OnTargetHeightChanged(int value)
        {
            if (KeepAspect && _working != null && _working.Height > 0 && !_adjusting)
            { _adjusting = true; TargetWidth = Math.Max(1, (int)Math.Round(value * (double)_working.Width / _working.Height)); _adjusting = false; }
        }
        private bool _adjusting;

        [RelayCommand]
        private async Task ImportImage()
        {
            var path = await _files.OpenFileAsync("Importer une image",
                new[] { new FileFilter("Images", new[] { "png", "bmp", "jpg", "jpeg" }) });
            if (path is null) return;
            try
            {
                var sk = await Task.Run(() => _asset.LoadArgb(path));
                SetWorking(sk, keepOriginal: true);
                _isSheet = false;
                ExportedCode = "/* Sélectionnez une zone, rognez/réduisez, puis Convertir. */";
                Status = $"Image {sk.Width}×{sk.Height}. Glissez sur l'image pour sélectionner un sprite.";
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        private async Task ImportSheet()
        {
            if (FrameWidth <= 0 || FrameHeight <= 0) { Status = "Renseignez la taille de frame."; return; }
            var path = await _files.OpenFileAsync("Importer une planche",
                new[] { new FileFilter("Images", new[] { "png", "bmp" }) });
            if (path is null) return;
            try
            {
                var sk = await Task.Run(() => _asset.LoadArgb(path));
                SetWorking(sk, keepOriginal: true);
                _isSheet = true;
                OnPropertyChanged(nameof(FrameInfo));
                Status = $"Planche {sk.Width}×{sk.Height} — {FrameInfo}.";
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        private void Crop()
        {
            if (_working is null || Selection.Width < 1 || Selection.Height < 1) return;
            try
            {
                var c = _asset.Crop(_working, (int)Selection.X, (int)Selection.Y, (int)Selection.Width, (int)Selection.Height);
                SetWorking(c, keepOriginal: false);
                _isSheet = false;
                Selection = default;
                Status = $"Rogné à {c.Width}×{c.Height}.";
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        private void Resize()
        {
            if (_working is null) return;
            try
            {
                var r = _asset.Resize(_working, TargetWidth, TargetHeight, Smooth);
                SetWorking(r, keepOriginal: false);
                _isSheet = false;
                Status = $"Réduit à {r.Width}×{r.Height}.";
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        private void Revert()
        {
            if (_original is null) return;
            SetWorking(_original.Copy(), keepOriginal: false);
            _isSheet = false;
            Selection = default;
            Status = "Image d'origine rétablie.";
        }

        [RelayCommand]
        private async Task Convert()
        {
            if (_working is null) return;
            long px = (long)_working.Width * _working.Height;
            if (px > ScreenPixels)
            {
                ExportedCode = $"/* Image {_working.Width}x{_working.Height} ({px} px) : au-dela de l'ecran AKA 320x240. Reduisez avant de convertir. */";
                Status = "Trop grand : réduisez avant de convertir.";
                return;
            }
            Status = "Conversion…";
            var (fw, fh, count) = FrameData();
            var snap = _working.Copy();
            try
            {
                var code = await Task.Run(() =>
                {
                    var sp = _asset.BuildSprite(snap, "sprite", _fmt, _key, UseTransparency, fw, fh, count);
                    return _asset.ExportSpriteToCpp(sp);
                });
                ExportedCode = code.Length > PreviewCap ? code.Substring(0, PreviewCap) + "\n/* … tronqué */" : code;
                Status = $"Converti : {_working.Width}×{_working.Height}.";
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
            finally { snap.Dispose(); }
        }

        [RelayCommand]
        private async Task ExportCpp()
        {
            if (_working is null) return;
            var path = await _files.SaveFileAsync("Exporter en C++", "sprite.h",
                new[] { new FileFilter("C/C++", new[] { "h", "cpp" }) });
            if (path is null) return;
            var (fw, fh, count) = FrameData();
            var snap = _working.Copy();
            try
            {
                var code = await Task.Run(() =>
                {
                    var sp = _asset.BuildSprite(snap, Path.GetFileNameWithoutExtension(path), _fmt, _key, UseTransparency, fw, fh, count);
                    return _asset.ExportSpriteToCpp(sp);
                });
                await Task.Run(() => File.WriteAllText(path, code));
                Status = $"Exporté : {path}";
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
            finally { snap.Dispose(); }
        }

        [RelayCommand]
        private async Task SaveProject()
        {
            if (_working is null) return;
            var path = await _files.SaveFileAsync("Enregistrer le projet sprite", "sprite.gbspr",
                new[] { new FileFilter("Projet sprite", new[] { "gbspr" }) });
            if (path is null) return;
            var (fw, fh, count) = FrameData();
            try
            {
                var sp = _asset.BuildSprite(_working, Path.GetFileNameWithoutExtension(path), _fmt, _key, UseTransparency, fw, fh, count);
                _asset.SaveSprite(sp, path);
                Status = $"Projet enregistré : {path}";
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        private async Task OpenProject()
        {
            var path = await _files.OpenFileAsync("Ouvrir un projet sprite",
                new[] { new FileFilter("Projet sprite", new[] { "gbspr" }) });
            if (path is null) return;
            try
            {
                var sp = _asset.LoadSprite(path);
                if (sp.PixelData is null) throw new InvalidDataException("Données pixel absentes.");
                _fmt = sp.ColorFormat; _key = sp.TransparentKey; UseTransparency = sp.UseTransparency;
                OnPropertyChanged(nameof(TransparentKeyHex));
                var sk = _asset.UnpackToBitmap(sp.PixelData, sp.Width, sp.Height, sp.ColorFormat, sp.UseTransparency, sp.TransparentKey);
                SetWorking(sk, keepOriginal: true);
                FrameWidth = sp.FrameWidth > 0 ? sp.FrameWidth : sp.Width;
                FrameHeight = sp.FrameHeight > 0 ? sp.FrameHeight : sp.Height;
                _isSheet = sp.FrameCount > 1;
                OnPropertyChanged(nameof(FrameInfo));
                Status = $"Projet ouvert : {sp.Name}.";
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        private (int fw, int fh, int count) FrameData()
        {
            if (_isSheet && _working != null && FrameWidth > 0 && FrameHeight > 0)
            {
                int c = _working.Width / FrameWidth, r = _working.Height / FrameHeight;
                return (FrameWidth, FrameHeight, Math.Max(1, c * r));
            }
            return (_working?.Width ?? 0, _working?.Height ?? 0, 1);
        }

        private void SetWorking(SKBitmap bmp, bool keepOriginal)
        {
            _working?.Dispose();
            _working = bmp;
            if (keepOriginal) { _original?.Dispose(); _original = bmp.Copy(); }
            TargetWidth = bmp.Width; TargetHeight = bmp.Height;
            Dimensions = $"{bmp.Width}×{bmp.Height}";
            Zoom = FitZoom(bmp.Width, bmp.Height);
            DisplayImage = SkiaImageBridge.ToAvalonia(bmp);
            Selection = default;
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(FrameInfo));
        }

        private static int FitZoom(int w, int h)
        {
            int m = Math.Max(w, h);
            if (m > 256) return 1;
            if (m > 96) return 2;
            if (m > 48) return 4;
            if (m > 24) return 8;
            return 16;
        }
    }
}
