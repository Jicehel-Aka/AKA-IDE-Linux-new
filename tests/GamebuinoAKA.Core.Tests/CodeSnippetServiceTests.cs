using System.IO;
using System.Linq;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class CodeSnippetServiceTests
    {
        private static (CodeSnippetService svc, SettingsService settings) Make()
        {
            var paths = new TempPlatformPaths();
            var settings = new SettingsService(paths);
            settings.Settings.WorkspaceFolder = Path.Combine(paths.Root, "ws");
            Directory.CreateDirectory(settings.Settings.WorkspaceFolder);
            return (new CodeSnippetService(settings), settings);
        }

        [Fact]
        public void GetAll_PlatformIO_OnlyPlatformIO()
        {
            var (svc, _) = Make();
            var list = svc.GetAll(BuildSystem.PlatformIO);
            Assert.NotEmpty(list);
            Assert.All(list, s => Assert.True(s.ForPlatformIO));
        }

        // Robuste : le filtrage est correct même si le catalogue était vide côté ESP-IDF.
        [Fact]
        public void GetAll_EspIdf_ReturnsOnlyEspIdfSnippets()
        {
            var (svc, _) = Make();
            Assert.All(svc.GetAll(BuildSystem.EspIdf), s => Assert.True(s.ForEspIdf));
        }

        // Réel : le catalogue contient bien des procédures ESP-IDF.
        [Fact]
        public void GetAll_EspIdf_ContainsBuiltinProcedures()
        {
            var (svc, _) = Make();
            var list = svc.GetAll(BuildSystem.EspIdf);
            Assert.NotEmpty(list);
            Assert.True(list.Count >= 15, "Le catalogue ESP-IDF doit être aligné sur PlatformIO.");
            Assert.Contains(list, s => s.Id == "esp_gfx_shapes");
        }

        [Fact]
        public void GenerateFiles_EspIdf_InjectsIntoAppMain()
        {
            var (svc, _) = Make();
            var snip = svc.GetAll(BuildSystem.EspIdf).First(s => s.Id == "esp_gfx_shapes");
            var files = svc.GenerateFiles(new[] { snip }, BuildSystem.EspIdf, "MonJeu");
            Assert.Contains(files.Keys, k => k.Contains("app_main"));
            Assert.Contains("fillRect", files.First(kv => kv.Key.Contains("app_main")).Value);
        }

        // Robuste : teste le moteur d'injection avec un snippet synthétique.
        [Fact]
        public void GenerateFiles_InjectsSnippetMarkerContent()
        {
            var (svc, _) = Make();
            var snip = new CodeSnippet
            {
                Id = "test_marker", Name = "Test", ForPlatformIO = true,
                TargetFile = SnippetTargetFile.GameCpp,
                Code = "//@@GLOBALS@@\nstatic int MARKER_XZ = 1;\n"
            };
            var files = svc.GenerateFiles(new[] { snip }, BuildSystem.PlatformIO, "MonJeu");
            Assert.NotEmpty(files);
            Assert.Contains(files.Values, v => v.Contains("MARKER_XZ"));
        }

        [Fact]
        public void GenerateFiles_PlatformIO_ProducesFiles()
        {
            var (svc, _) = Make();
            var snip = svc.GetAll(BuildSystem.PlatformIO).First();
            var files = svc.GenerateFiles(new[] { snip }, BuildSystem.PlatformIO, "MonJeu");
            Assert.NotEmpty(files);
        }

        [Fact]
        public void UserBank_SaveLoad_RoundTrips()
        {
            var (svc, settings) = Make();
            var bank = svc.LoadUserBank();
            bank.UserSnippets.Add(new CodeSnippet { Id = "u1", Name = "Mon snippet", ForPlatformIO = true });
            svc.SaveUserBank(bank);
            Assert.Contains(new CodeSnippetService(settings).LoadUserBank().UserSnippets, s => s.Id == "u1");
        }
    }
}
