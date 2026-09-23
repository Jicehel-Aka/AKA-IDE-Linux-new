using System;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class EspIdfServiceTests
    {
        private static (EspIdfService svc, RecordingProcessRunner runner, SettingsService settings) Make()
        {
            var settings = new SettingsService(new TempPlatformPaths());
            settings.Settings.IdfExportScript = OperatingSystem.IsWindows()
                ? @"C:\ESP-IDF\frameworks\esp-idf\export.bat"
                : "/home/jean/esp/esp-idf/export.sh";
            var runner = new RecordingProcessRunner();
            return (new EspIdfService(settings, runner), runner, settings);
        }

        private static GamebuinoProject Proj() =>
            new GamebuinoProject { FolderPath = "/home/jean/ws/asteria", BuildSystem = BuildSystem.EspIdf };

        [Fact]
        public async Task Build_WrapsIdfPyInShell_SourcingExportScript()
        {
            var (svc, runner, _) = Make();
            await svc.BuildAsync(Proj(), null);

            var req = runner.Last!;
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal("cmd.exe", req.FileName);
                Assert.Equal("/c", req.Arguments[0]);
                Assert.Contains("idf.py -C", req.Arguments[1]);
                Assert.Contains("build", req.Arguments[1]);
                Assert.Contains("call", req.Arguments[1]);
            }
            else
            {
                Assert.Equal("bash", req.FileName);
                Assert.Equal("-lc", req.Arguments[0]);
                Assert.Contains("source", req.Arguments[1]);
                Assert.Contains("idf.py -C", req.Arguments[1]);
                Assert.Contains("build", req.Arguments[1]);
            }
        }

        [Fact]
        public async Task Flash_IncludesSerialPort()
        {
            var (svc, runner, settings) = Make();
            settings.Settings.IdfSerialPort = "/dev/ttyUSB0";
            await svc.FlashAsync(Proj(), null);
            Assert.Contains("-p /dev/ttyUSB0 flash", runner.Last!.Arguments[^1]);
        }

        [Fact]
        public void DetectSerialPorts_DoesNotThrow()
        {
            var (svc, _, _) = Make();
            Assert.NotNull(svc.DetectSerialPorts());
        }
    }
}
