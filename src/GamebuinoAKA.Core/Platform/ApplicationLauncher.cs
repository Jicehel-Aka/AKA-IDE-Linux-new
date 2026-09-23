using System;
using GamebuinoAKA.Core.Platform;

namespace GamebuinoAKA.Core.Platform
{
    /// <summary>
    /// Ouvre dossiers/fichiers/URL via l'outil système, sans bloquer :
    /// Linux → xdg-open ; Windows → explorer.exe (dossiers/fichiers) et
    /// association système (URL). Lancement « fire-and-forget » (on n'attend pas
    /// la fermeture de l'appli ouverte).
    /// </summary>
    public sealed class ApplicationLauncher : IApplicationLauncher
    {
        private readonly IProcessRunner _runner;

        public ApplicationLauncher(IProcessRunner runner)
        {
            _runner = runner;
        }

        public void OpenFolder(string folderPath) => Open(folderPath);
        public void OpenFile(string filePath) => Open(filePath);
        public void OpenUrl(string url) => Open(url);

        private void Open(string target)
        {
            var request = OperatingSystem.IsWindows()
                ? new ProcessRequest("explorer.exe", new[] { target })
                : new ProcessRequest("xdg-open", new[] { target });

            // fire-and-forget : on ne bloque pas sur la durée de vie de l'appli ouverte.
            _ = _runner.RunStreamingAsync(request, null);
        }
    }
}
