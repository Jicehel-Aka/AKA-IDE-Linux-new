using System;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Platform;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Chaîne PlatformIO, portable : localise « pio » (ou « platformio ») via
    /// IToolLocator et l'exécute via IProcessRunner avec des arguments séparés.
    /// Aucun « pio.exe » en dur, aucune commande shell concaténée.
    /// </summary>
    public sealed class PlatformIOService : IPlatformIOService
    {
        private readonly ISettingsService _settings;
        private readonly IProcessRunner _runner;
        private readonly IToolLocator _tools;

        public PlatformIOService(ISettingsService settings, IProcessRunner runner, IToolLocator tools)
        {
            _settings = settings;
            _runner = runner;
            _tools = tools;
        }

        public string DetectPioPath()
            => _tools.Locate("pio", _settings.Settings.PlatformIOPath)
               ?? _tools.Locate("platformio")
               ?? string.Empty;

        private string ResolvePio()
            => _tools.Locate("pio", _settings.Settings.PlatformIOPath)
               ?? _tools.Locate("platformio")
               ?? throw new InvalidOperationException(
                   "PlatformIO (pio) introuvable. Installez-le ou renseignez son chemin dans les Paramètres.");

        public Task BuildAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => Run(p, o, ct, "run");

        public Task FlashAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => Run(p, o, ct, "run", "-t", "upload");

        public Task MonitorAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => Run(p, o, ct, "device", "monitor");

        public Task CleanAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => Run(p, o, ct, "run", "-t", "clean");

        private async Task Run(GamebuinoProject p, Action<string>? onOutput, CancellationToken ct, params string[] args)
        {
            var pio = ResolvePio();
            var request = new ProcessRequest(pio, args, p.FolderPath);
            onOutput?.Invoke($"[PlatformIO] pio {string.Join(' ', args)}");
            await _runner.RunStreamingAsync(request, onOutput, ct).ConfigureAwait(false);
        }

        public async Task<string> GetVersionAsync()
        {
            try
            {
                var pio = DetectPioPath();
                if (string.IsNullOrEmpty(pio)) return "Non détecté";
                var result = await _runner.RunAsync(new ProcessRequest(pio, new[] { "--version" }));
                var v = result.StandardOutput.Trim();
                return string.IsNullOrEmpty(v) ? "Non détecté" : v;
            }
            catch { return "Non détecté"; }
        }
    }
}
