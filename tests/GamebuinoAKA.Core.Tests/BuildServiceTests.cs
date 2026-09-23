using System;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class BuildServiceTests
    {
        private sealed class FakePio : IPlatformIOService
        {
            public bool Built;
            public Task BuildAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default) { Built = true; return Task.CompletedTask; }
            public Task FlashAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default) => Task.CompletedTask;
            public Task MonitorAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default) => Task.CompletedTask;
            public Task CleanAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default) => Task.CompletedTask;
            public Task<string> GetVersionAsync() => Task.FromResult("x");
            public string DetectPioPath() => "pio";
        }

        private sealed class FakeIdf : IEspIdfService
        {
            public bool Built;
            public Task BuildAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default) { Built = true; return Task.CompletedTask; }
            public Task FlashAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default) => Task.CompletedTask;
            public Task MonitorAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default) => Task.CompletedTask;
            public Task CleanAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default) => Task.CompletedTask;
            public Task<string> GetVersionAsync() => Task.FromResult("x");
            public string[] DetectSerialPorts() => Array.Empty<string>();
        }

        [Fact]
        public async Task Dispatches_ToPlatformIO_ForPioProject()
        {
            var pio = new FakePio(); var idf = new FakeIdf();
            var svc = new BuildService(pio, idf);
            await svc.BuildAsync(new GamebuinoProject { BuildSystem = BuildSystem.PlatformIO }, null);
            Assert.True(pio.Built);
            Assert.False(idf.Built);
        }

        [Fact]
        public async Task Dispatches_ToEspIdf_ForIdfProject()
        {
            var pio = new FakePio(); var idf = new FakeIdf();
            var svc = new BuildService(pio, idf);
            await svc.BuildAsync(new GamebuinoProject { BuildSystem = BuildSystem.EspIdf }, null);
            Assert.True(idf.Built);
            Assert.False(pio.Built);
        }
    }
}
