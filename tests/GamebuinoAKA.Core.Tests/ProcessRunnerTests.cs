using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Platform;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class ProcessRunnerTests
    {
        // « dotnet » est présent partout où ces tests tournent (SDK installé).
        [Fact]
        public async Task RunAsync_Dotnet_Version_Succeeds()
        {
            var runner = new SystemProcessRunner();
            var result = await runner.RunAsync(new ProcessRequest("dotnet", new[] { "--version" }));

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
        }

        [Fact]
        public async Task RunStreamingAsync_StreamsOutput()
        {
            var runner = new SystemProcessRunner();
            var lines = new List<string>();
            var exit = await runner.RunStreamingAsync(
                new ProcessRequest("dotnet", new[] { "--version" }),
                line => { lock (lines) lines.Add(line); });

            Assert.Equal(0, exit);
            Assert.NotEmpty(lines);
        }

        [Fact]
        public async Task RunAsync_MissingExecutable_Throws()
        {
            var runner = new SystemProcessRunner();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                runner.RunAsync(new ProcessRequest("aka-outil-inexistant-xyz", Array.Empty<string>())));
        }
    }

    public class ToolLocatorTests
    {
        [Fact]
        public void Locate_OnPath_ReturnsPath()
        {
            var loc = new ToolLocator();
            Assert.NotNull(loc.Locate("dotnet")); // dotnet est sur le PATH
        }

        [Fact]
        public void Locate_Unknown_ReturnsNull()
        {
            var loc = new ToolLocator();
            Assert.Null(loc.Locate("aka-outil-inexistant-xyz"));
        }

        [Fact]
        public void Locate_ConfiguredPath_TakesPriority()
        {
            var loc = new ToolLocator();
            var tmp = Path.Combine(Path.GetTempPath(), "aka-fake-tool-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(tmp, "#!/bin/sh\n");
            try
            {
                Assert.Equal(tmp, loc.Locate("git", tmp)); // chemin configuré prioritaire
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void Locate_ConfiguredPathMissing_FallsBackToPath()
        {
            var loc = new ToolLocator();
            var bogus = Path.Combine(Path.GetTempPath(), "n-existe-pas-" + Guid.NewGuid().ToString("N"));
            // chemin configuré invalide → on retombe sur le PATH
            Assert.NotNull(loc.Locate("dotnet", bogus));
        }
    }
}
