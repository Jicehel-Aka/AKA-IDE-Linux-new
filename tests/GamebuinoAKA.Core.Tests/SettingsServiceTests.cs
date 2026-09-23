using System;
using System.IO;
using System.Linq;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Platform;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class SettingsServiceTests
    {
        /// <summary>IPlatformPaths de test : tout dans un dossier temporaire jetable.</summary>
        private sealed class TempPaths : IPlatformPaths, IDisposable
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

        [Fact]
        public void Load_NoFile_UsesDefaults()
        {
            using var paths = new TempPaths();
            var svc = new SettingsService(paths);

            Assert.Equal(BuildSystem.EspIdf, svc.Settings.DefaultBuildSystem);
            Assert.Equal(ColorFormat.Bgr565Aka, svc.Settings.DefaultColorFormat);
            Assert.Equal(0xF81F, svc.Settings.DefaultTransparentKey);
            Assert.Equal(paths.DefaultWorkspaceDirectory, svc.Settings.WorkspaceFolder);
        }

        [Fact]
        public void Save_Then_Load_RoundTrips()
        {
            using var paths = new TempPaths();

            var svc = new SettingsService(paths);
            svc.Settings.WorkspaceFolder = "/home/jean/mesjeux";
            svc.Settings.IdfSerialPort = "/dev/ttyUSB0";
            svc.Settings.DefaultBuildSystem = BuildSystem.PlatformIO;
            svc.Save();

            Assert.True(File.Exists(svc.SettingsFilePath));

            var reloaded = new SettingsService(paths);
            Assert.Equal("/home/jean/mesjeux", reloaded.Settings.WorkspaceFolder);
            Assert.Equal("/dev/ttyUSB0", reloaded.Settings.IdfSerialPort);
            Assert.Equal(BuildSystem.PlatformIO, reloaded.Settings.DefaultBuildSystem);
        }

        [Fact]
        public void Load_InvalidJson_FallsBackToDefaults()
        {
            using var paths = new TempPaths();
            Directory.CreateDirectory(paths.ConfigDirectory);
            File.WriteAllText(Path.Combine(paths.ConfigDirectory, "settings.json"),
                "{ ceci n'est pas du JSON valide ");

            var svc = new SettingsService(paths); // ne doit PAS lever d'exception

            Assert.Equal(BuildSystem.EspIdf, svc.Settings.DefaultBuildSystem);
            Assert.Equal(paths.DefaultWorkspaceDirectory, svc.Settings.WorkspaceFolder);
        }

        [Fact]
        public void Save_CreatesConfigDirectory()
        {
            using var paths = new TempPaths();
            Assert.False(Directory.Exists(paths.ConfigDirectory));

            var svc = new SettingsService(paths);
            svc.Save();

            Assert.True(Directory.Exists(paths.ConfigDirectory));
        }

        [Fact]
        public void AddRecentProject_DedupsAndCaps()
        {
            using var paths = new TempPaths();
            var svc = new SettingsService(paths);
            svc.Settings.MaxRecentProjects = 3;

            svc.AddRecentProject("/a");
            svc.AddRecentProject("/b");
            svc.AddRecentProject("/c");
            svc.AddRecentProject("/a"); // remonte en tête, sans doublon

            Assert.Equal(new[] { "/a", "/c", "/b" }, svc.Settings.RecentProjects.ToArray());

            svc.AddRecentProject("/d"); // dépasse le plafond → éjecte le plus ancien (/b)

            Assert.Equal(3, svc.Settings.RecentProjects.Count);
            Assert.Equal("/d", svc.Settings.RecentProjects[0]);
            Assert.DoesNotContain("/b", svc.Settings.RecentProjects);
        }
    }
}
