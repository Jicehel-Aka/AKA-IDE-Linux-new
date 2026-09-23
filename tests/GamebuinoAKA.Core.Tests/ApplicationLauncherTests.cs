using System;
using GamebuinoAKA.Core.Platform;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class ApplicationLauncherTests
    {
        [Fact]
        public void OpenFolder_UsesSystemOpener_WithPath()
        {
            var runner = new RecordingProcessRunner();
            var launcher = new ApplicationLauncher(runner);

            launcher.OpenFolder("/home/jean/ws/game");

            var expected = OperatingSystem.IsWindows() ? "explorer.exe" : "xdg-open";
            Assert.Equal(expected, runner.Last!.FileName);
            Assert.Equal(new[] { "/home/jean/ws/game" }, runner.Last.Arguments);
        }

        [Fact]
        public void OpenUrl_UsesSystemOpener()
        {
            var runner = new RecordingProcessRunner();
            new ApplicationLauncher(runner).OpenUrl("https://github.com/Jicehel-Aka");
            Assert.Equal(new[] { "https://github.com/Jicehel-Aka" }, runner.Last!.Arguments);
        }
    }
}
