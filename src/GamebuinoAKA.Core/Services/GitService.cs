using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Platform;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Clonage git, portable : localise « git » via IToolLocator (jamais « git.exe »
    /// en dur) et l'exécute via IProcessRunner avec des arguments SÉPARÉS (pas de
    /// commande shell concaténée).
    /// </summary>
    public sealed class GitService : IGitService
    {
        private readonly ISettingsService _settings;
        private readonly IProcessRunner _runner;
        private readonly IToolLocator _tools;

        public GitService(ISettingsService settings, IProcessRunner runner, IToolLocator tools)
        {
            _settings = settings;
            _runner = runner;
            _tools = tools;
        }

        public Task<bool> IsInstalledAsync()
            => Task.FromResult(_tools.Locate("git") != null);

        public async Task<string> CloneAsync(string repoUrl, string? folderName,
            Action<string>? onOutput, CancellationToken ct = default)
        {
            var workspace = _settings.Settings.WorkspaceFolder;
            if (string.IsNullOrEmpty(workspace))
                throw new InvalidOperationException(
                    "Dossier workspace non configuré. Allez dans Paramètres.");

            Directory.CreateDirectory(workspace);

            if (string.IsNullOrWhiteSpace(folderName))
                folderName = ExtractRepoName(repoUrl);

            var destination = Path.Combine(workspace, folderName);
            if (Directory.Exists(destination))
                throw new InvalidOperationException(
                    $"Le dossier « {folderName} » existe déjà dans le workspace.");

            var git = _tools.Locate("git")
                ?? throw new InvalidOperationException(
                    "git est introuvable. Installez-le (ou ajoutez-le au PATH).");

            onOutput?.Invoke($"Clonage de {repoUrl} → {destination}");

            // Arguments séparés : espaces gérés, pas d'injection shell.
            var request = new ProcessRequest(git, new[] { "clone", repoUrl, destination }, workspace);
            var exit = await _runner.RunStreamingAsync(request, onOutput, ct).ConfigureAwait(false);

            if (exit != 0 || !Directory.Exists(destination))
                throw new InvalidOperationException("Le clonage a échoué.");

            return destination;
        }

        /// <summary>
        /// Déduit un nom de dossier depuis une URL GitHub.
        /// https://github.com/user/mon-repo(.git) → mon-repo
        /// </summary>
        public static string ExtractRepoName(string url)
        {
            url = (url ?? string.Empty).Trim().TrimEnd('/');
            if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                url = url.Substring(0, url.Length - 4);
            var parts = url.Split('/');
            var name = parts.Length > 0 ? parts[parts.Length - 1] : string.Empty;
            return string.IsNullOrEmpty(name) ? "repo" : name;
        }
    }
}
