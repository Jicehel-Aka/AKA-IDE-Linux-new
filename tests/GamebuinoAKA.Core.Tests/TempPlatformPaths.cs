using System;
using System.IO;
using GamebuinoAKA.Core.Platform;

namespace GamebuinoAKA.Core.Tests
{
    /// <summary>
    /// IPlatformPaths de test : tout est isolé dans un dossier temporaire jetable.
    /// Réutilisable par les différents tests (settings, log, …).
    /// </summary>
    public sealed class TempPlatformPaths : IPlatformPaths, IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "aka-test-" + Guid.NewGuid().ToString("N"));

        public string ConfigDirectory => Path.Combine(Root, "config");
        public string DataDirectory => Path.Combine(Root, "data");
        public string CacheDirectory => Path.Combine(Root, "cache");
        public string DefaultWorkspaceDirectory => Path.Combine(Root, "workspace");

        public void Dispose()
        {
            try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
            catch { /* nettoyage best-effort */ }
        }
    }
}
