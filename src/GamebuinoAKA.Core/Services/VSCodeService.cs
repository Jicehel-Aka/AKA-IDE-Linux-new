using System;
using System.IO;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Platform;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Ouvre des projets dans VS Code, portable : localise « code » via IToolLocator
    /// (Linux : code ; Windows : code.cmd/Code.exe) et le lance via IProcessRunner.
    /// </summary>
    public sealed class VSCodeService : IVSCodeService
    {
        private readonly ISettingsService _settings;
        private readonly IProcessRunner _runner;
        private readonly IToolLocator _tools;

        public VSCodeService(ISettingsService settings, IProcessRunner runner, IToolLocator tools)
        {
            _settings = settings;
            _runner = runner;
            _tools = tools;
        }

        public string DetectVSCodePath()
            => _tools.Locate("code", _settings.Settings.VSCodePath) ?? string.Empty;

        public bool IsInstalled() => !string.IsNullOrEmpty(DetectVSCodePath());

        public void OpenProject(GamebuinoProject project) => OpenFolder(project.FolderPath);

        public void OpenFolder(string folderPath)
        {
            var code = DetectVSCodePath();
            if (string.IsNullOrEmpty(code))
                throw new InvalidOperationException("VS Code introuvable. Vérifiez les Paramètres.");

            // Arguments séparés : le chemin (avec espaces) passe tel quel.
            _ = _runner.RunStreamingAsync(new ProcessRequest(code, new[] { folderPath }), null);
        }

        public string GetDisplayPath()
        {
            var p = DetectVSCodePath();
            if (string.IsNullOrEmpty(p)) return "Non détecté";
            return Path.GetFileName(p);
        }
    }
}
