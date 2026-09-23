using System;
using System.IO;
using GamebuinoAKA.Core.Platform;

namespace GamebuinoAKA.App.Platform
{
    /// <summary>Chemins conformes à la spec XDG Base Directory (Linux).</summary>
    public sealed class LinuxPlatformPaths : IPlatformPaths
    {
        private const string AppName = "GamebuinoAKA";

        private static string Home =>
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        private static string Xdg(string variable, params string[] fallbackParts)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrEmpty(value) ? Path.Combine(fallbackParts) : value;
        }

        public string ConfigDirectory =>
            Path.Combine(Xdg("XDG_CONFIG_HOME", Home, ".config"), AppName);

        public string DataDirectory =>
            Path.Combine(Xdg("XDG_DATA_HOME", Home, ".local", "share"), AppName);

        public string CacheDirectory =>
            Path.Combine(Xdg("XDG_CACHE_HOME", Home, ".cache"), AppName);

        public string DefaultWorkspaceDirectory =>
            Path.Combine(Home, AppName);
    }
}
