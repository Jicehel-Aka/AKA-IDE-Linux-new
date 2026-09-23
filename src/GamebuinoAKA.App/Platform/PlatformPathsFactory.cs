using System;
using GamebuinoAKA.Core.Platform;

namespace GamebuinoAKA.App.Platform
{
    /// <summary>
    /// Sélectionne l'implémentation d'IPlatformPaths selon l'OS. La détection d'OS
    /// reste dans App (jamais dans Core). Sera branché dans la DI d'App.
    /// </summary>
    public static class PlatformPathsFactory
    {
        public static IPlatformPaths Create() =>
            OperatingSystem.IsWindows()
                ? new WindowsPlatformPaths()
                : new LinuxPlatformPaths();   // Linux (et par défaut : tout Unix)
    }
}
