using System;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class PlatformIOServiceTests
    {
        private static (PlatformIOService svc, RecordingProcessRunner runner) Make(string? pioPath = "pio")
        {
            var paths = new TempPlatformPaths();
            var settings = new SettingsService(paths);
            var runner = new RecordingProcessRunner();
            var tools = new FakeToolLocator();
            if (pioPath != null) tools.Map["pio"] = pioPath;
            return (new PlatformIOService(settings, runner, tools), runner);
        }

        private static GamebuinoProject Proj() =>
            new GamebuinoProject { FolderPath = "/home/jean/ws/game", BuildSystem = BuildSystem.PlatformIO };

        [Fact]
        public async Task Build_RunsPioRun_InProjectDir()
        {
            var (svc, runner) = Make();
            await svc.BuildAsync(Proj(), null);
            Assert.Equal("pio", runner.Last!.FileName);
            Assert.Equal(new[] { "run" }, runner.Last.Arguments);
            Assert.Equal("/home/jean/ws/game", runner.Last.WorkingDirectory);
        }

        [Fact]
        public async Task Flash_RunsUpload()
        {
            var (svc, runner) = Make();
            await svc.FlashAsync(Proj(), null);
            Assert.Equal(new[] { "run", "-t", "upload" }, runner.Last!.Arguments);
        }

        [Fact]
        public async Task Monitor_RunsDeviceMonitor()
        {
            var (svc, runner) = Make();
            await svc.MonitorAsync(Proj(), null);
            Assert.Equal(new[] { "device", "monitor" }, runner.Last!.Arguments);
        }

        [Fact]
        public async Task Clean_RunsRunTClean()
        {
            var (svc, runner) = Make();
            await svc.CleanAsync(Proj(), null);
            Assert.Equal(new[] { "run", "-t", "clean" }, runner.Last!.Arguments);
        }

        [Fact]
        public async Task NoPio_Throws()
        {
            var (svc, _) = Make(pioPath: null); // ni pio ni platformio localisés
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.BuildAsync(Proj(), null));
        }

        [Fact]
        public void DetectPioPath_ReturnsLocated()
        {
            var (svc, _) = Make("pio");
            Assert.Equal("pio", svc.DetectPioPath());
        }
    }
}
