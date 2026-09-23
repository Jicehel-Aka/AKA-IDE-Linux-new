using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;

namespace GamebuinoAKA.App.ViewModels
{
    public sealed partial class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settings;
        private readonly IPlatformIOService _pio;
        private readonly IEspIdfService _idf;
        private readonly IVSCodeService _vscode;
        private readonly IFileDialogService _files;

        [ObservableProperty] private string _workspaceFolder = string.Empty;
        [ObservableProperty] private string _platformIOPath = string.Empty;
        [ObservableProperty] private string _vSCodePath = string.Empty;
        [ObservableProperty] private string _idfExportScript = string.Empty;
        [ObservableProperty] private string _idfSerialPort = string.Empty;
        [ObservableProperty] private string _referenceComponentPath = string.Empty;
        [ObservableProperty] private bool _defaultEspIdf = true;
        [ObservableProperty] private string _status = string.Empty;

        public SettingsViewModel(ISettingsService settings, IPlatformIOService pio,
            IEspIdfService idf, IVSCodeService vscode, IFileDialogService files)
        {
            _settings = settings; _pio = pio; _idf = idf; _vscode = vscode; _files = files;

            var s = settings.Settings;
            _workspaceFolder = s.WorkspaceFolder;
            _platformIOPath = s.PlatformIOPath;
            _vSCodePath = s.VSCodePath;
            _idfExportScript = s.IdfExportScript;
            _idfSerialPort = s.IdfSerialPort;
            _referenceComponentPath = s.ReferenceGamebuinoComponentPath;
            _defaultEspIdf = s.DefaultBuildSystem == BuildSystem.EspIdf;
        }

        [RelayCommand]
        private async Task BrowseWorkspace()
        {
            var f = await _files.OpenFolderAsync("Dossier workspace", WorkspaceFolder);
            if (f != null) WorkspaceFolder = f;
        }

        [RelayCommand]
        private async Task BrowseReference()
        {
            var f = await _files.OpenFolderAsync("components/gamebuino de référence", ReferenceComponentPath);
            if (f != null) ReferenceComponentPath = f;
        }

        [RelayCommand]
        private async Task BrowseExportScript()
        {
            var f = await _files.OpenFileAsync("Script d'environnement ESP-IDF (export.sh / export.bat)");
            if (f != null) IdfExportScript = f;
        }

        [RelayCommand]
        private void Save()
        {
            var s = _settings.Settings;
            s.WorkspaceFolder = WorkspaceFolder;
            s.PlatformIOPath = PlatformIOPath;
            s.VSCodePath = VSCodePath;
            s.IdfExportScript = IdfExportScript;
            s.IdfSerialPort = IdfSerialPort;
            s.ReferenceGamebuinoComponentPath = ReferenceComponentPath;
            s.DefaultBuildSystem = DefaultEspIdf ? BuildSystem.EspIdf : BuildSystem.PlatformIO;
            _settings.Save();
            Status = "Paramètres enregistrés.";
        }

        [RelayCommand]
        private void AutoDetect()
        {
            PlatformIOPath = _pio.DetectPioPath();
            VSCodePath = _vscode.DetectVSCodePath();
            var ports = _idf.DetectSerialPorts();
            if (ports.Length == 1) IdfSerialPort = ports[0];

            var pio = string.IsNullOrEmpty(PlatformIOPath) ? "non trouvé" : "OK";
            var code = string.IsNullOrEmpty(VSCodePath) ? "non trouvé" : "OK";
            Status = $"PlatformIO : {pio}  |  VS Code : {code}  |  ports série : {ports.Length}";
        }
    }
}
