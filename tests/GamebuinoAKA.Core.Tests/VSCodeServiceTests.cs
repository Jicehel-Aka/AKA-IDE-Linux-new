using System;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class VSCodeServiceTests
    {
        private static (VSCodeService svc, RecordingProcessRunner runner) Make(string? codePath = "code")
        {
            var settings = new SettingsService(new TempPlatformPaths());
            var runner = new RecordingProcessRunner();
            var tools = new FakeToolLocator();
            if (codePath != null) tools.Map["code"] = codePath;
            return (new VSCodeService(settings, runner, tools), runner);
        }

        [Fact]
        public void OpenProject_LaunchesCode_WithFolderPath()
        {
            var (svc, runner) = Make();
            svc.OpenProject(new GamebuinoProject { FolderPath = "/home/jean/ws/mon jeu" });
            Assert.Equal("code", runner.Last!.FileName);
            Assert.Equal(new[] { "/home/jean/ws/mon jeu" }, runner.Last.Arguments); // espace conservé
        }

        [Fact]
        public void IsInstalled_TrueWhenLocated()
        {
            var (svc, _) = Make("code");
            Assert.True(svc.IsInstalled());
            Assert.Equal("code", svc.GetDisplayPath());
        }

        [Fact]
        public void OpenFolder_NotInstalled_Throws()
        {
            var (svc, _) = Make(codePath: null);
            Assert.Throws<InvalidOperationException>(() => svc.OpenFolder("/x"));
        }
    }
}
