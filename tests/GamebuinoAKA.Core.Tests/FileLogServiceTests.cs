using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class FileLogServiceTests
    {
        [Fact]
        public void Info_WritesFile_CreatesDirectory_AndContainsMessage()
        {
            using var paths = new TempPlatformPaths();
            Assert.False(Directory.Exists(paths.DataDirectory));

            var log = new FileLogService(paths);
            log.Info("bonjour");

            Assert.True(Directory.Exists(paths.DataDirectory));
            Assert.True(File.Exists(log.LogFilePath));
            Assert.Contains("bonjour", File.ReadAllText(log.LogFilePath));
        }

        [Fact]
        public void Line_HasLevelAndTimestamp()
        {
            using var paths = new TempPlatformPaths();
            var log = new FileLogService(paths);
            log.Info("ligne test");

            var line = File.ReadAllLines(log.LogFilePath)[0];
            // Format attendu : "yyyy-MM-dd HH:mm:ss [INFO] ..."
            Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[INFO\] ", line);
            Assert.Contains("ligne test", line);
        }

        [Fact]
        public void Error_WithException_WritesExceptionDetails()
        {
            using var paths = new TempPlatformPaths();
            var log = new FileLogService(paths);
            log.Error("échec", new InvalidOperationException("boom-xyz"));

            var text = File.ReadAllText(log.LogFilePath);
            Assert.Contains("[ERREUR]", text);
            Assert.Contains("boom-xyz", text);
        }

        [Fact]
        public void ManyWrites_DoNotCorrupt_AllLinesPresent()
        {
            using var paths = new TempPlatformPaths();
            var log = new FileLogService(paths);

            for (int i = 0; i < 100; i++)
                log.Info("msg-" + i);

            var lines = File.ReadAllLines(log.LogFilePath);
            Assert.Equal(100, lines.Length);
            Assert.Contains("msg-0", lines[0]);
            Assert.Contains("msg-99", lines[99]);
        }

        [Fact]
        public void ConcurrentWrites_AreThreadSafe()
        {
            using var paths = new TempPlatformPaths();
            var log = new FileLogService(paths);

            Parallel.For(0, 200, i => log.Info("t-" + i));

            var lines = File.ReadAllLines(log.LogFilePath);
            Assert.Equal(200, lines.Length);
            // chaque ligne est bien formée (pas d'entrelacement)
            Assert.All(lines, l => Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \[INFO\] t-\d+$", l));
        }

        [Fact]
        public void Rotation_MovesOldFile_WhenThresholdExceeded()
        {
            using var paths = new TempPlatformPaths();
            var log = new FileLogService(paths, maxBytes: 200); // seuil minuscule pour le test

            for (int i = 0; i < 50; i++)
                log.Info("remplissage " + i);

            Assert.True(File.Exists(log.LogFilePath));           // le journal courant existe
            Assert.True(File.Exists(log.LogFilePath + ".old"));  // l'historique a été créé
        }

        [Fact]
        public void Clear_RemovesLogAndHistory()
        {
            using var paths = new TempPlatformPaths();
            var log = new FileLogService(paths, maxBytes: 200);

            for (int i = 0; i < 50; i++)
                log.Info("x " + i);

            Assert.True(File.Exists(log.LogFilePath));

            Assert.True(log.Clear());
            Assert.False(File.Exists(log.LogFilePath));
            Assert.False(File.Exists(log.LogFilePath + ".old"));
        }

        [Fact]
        public void Log_Facade_NoOp_BeforeConfigure_DoesNotThrow()
        {
            // Repart d'un état non configuré : la façade doit être silencieuse.
            Log.Configure(null!);
            Assert.False(Log.IsConfigured);
            Log.Info("ne doit rien faire");   // aucune exception
            Assert.Equal(string.Empty, Log.LogFilePath);
        }

        [Fact]
        public void Log_Facade_Delegates_WhenConfigured()
        {
            using var paths = new TempPlatformPaths();
            var svc = new FileLogService(paths);
            Log.Configure(svc);

            Assert.True(Log.IsConfigured);
            Assert.Equal(svc.LogFilePath, Log.LogFilePath);

            Log.Info("via la façade");
            Assert.Contains("via la façade", File.ReadAllText(svc.LogFilePath));

            Log.Configure(null!); // remet à zéro pour ne pas polluer d'autres tests
        }
    }
}
