using System;
using System.IO;
using GamebuinoAKA.Core.Platform;

namespace GamebuinoAKA.App.Platform
{
    /// <summary>
    /// Chemins Windows — reproduit le comportement de la version WPF d'origine :
    /// config sous %APPDATA%\GamebuinoAKA, workspace sous Documents\GamebuinoAKA.
    /// </summary>
    public sealed class WindowsPlatformPaths : IPlatformPaths
    {
        private const string AppName = "GamebuinoAKA";

        public string ConfigDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

        public string DataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

        public string CacheDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "cache");

        public string DefaultWorkspaceDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppName);
    }
}
