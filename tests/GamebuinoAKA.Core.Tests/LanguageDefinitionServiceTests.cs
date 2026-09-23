using System.IO;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    /// <summary>
    /// Vérifie le chargement (valeurs par défaut, écriture au premier lancement, relecture d'un fichier
    /// personnalisé) et l'export CSV (voir docs/PROTOCOLE_TRANSFERT.md du dépôt AKA-Love) de
    /// LanguageDefinitionService. Chaque test travaille dans un dossier temporaire isolé.
    /// </summary>
    public class LanguageDefinitionServiceTests
    {
        private static LanguageDefinitionService CreateInTempDir(out string dir)
        {
            dir = Path.Combine(Path.GetTempPath(), "akaide_test_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            return new LanguageDefinitionService(new FakeSettingsService(dir));
        }

        [Fact]
        public void FirstRun_WritesDefaultsToConfigFile()
        {
            var svc = CreateInTempDir(out _);
            Assert.True(File.Exists(svc.ConfigFilePath));
            Assert.Equal(2, svc.All.Count);
        }

        [Fact]
        public void Defaults_ContainLuaAndMicroPythonWithSdScriptsRoot()
        {
            var svc = CreateInTempDir(out _);
            var lua = svc.Get(CodeLanguage.Lua);
            Assert.Equal("AKA_Love/games", lua.SdScriptsRoot);
            Assert.Equal((byte)0, lua.TransferLangId);

            var mpy = svc.Get(CodeLanguage.MicroPython);
            Assert.Equal("py", mpy.SdScriptsRoot);
            Assert.Equal((byte)1, mpy.TransferLangId);
        }

        [Theory]
        [InlineData("main.lua", CodeLanguage.Lua)]
        [InlineData("snake.py", CodeLanguage.MicroPython)]
        public void FromFilePath_ResolvesByExtension(string path, CodeLanguage expected)
        {
            var svc = CreateInTempDir(out _);
            Assert.Equal(expected, svc.FromFilePath(path)!.Language);
        }

        [Fact]
        public void ExistingCustomConfigFile_OverridesDefaults()
        {
            var dir = Path.Combine(Path.GetTempPath(), "akaide_test_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            var configPath = Path.Combine(dir, "languages.json");
            File.WriteAllText(configPath, """
                [ { "Language": 0, "DisplayName": "Lua perso", "FileExtension": "lua",
                    "HighlightingName": "Lua", "TransferLangId": 0, "SdScriptsRoot": "mes_scripts_lua",
                    "Keywords": [], "ApiSymbols": [], "NewFileTemplate": "" } ]
                """);

            var svc = new LanguageDefinitionService(new FakeSettingsService(dir));

            Assert.Single(svc.All);
            Assert.Equal("mes_scripts_lua", svc.Get(CodeLanguage.Lua).SdScriptsRoot);
        }

        [Fact]
        public void ExportLanguagesCsv_WritesOneLinePerLanguageUnderAkaFolder()
        {
            var svc = CreateInTempDir(out _);
            var sdRoot = Path.Combine(Path.GetTempPath(), "akaide_sd_" + Path.GetRandomFileName());

            svc.ExportLanguagesCsv(sdRoot);

            var csvPath = Path.Combine(sdRoot, "AKA", "languages.csv");
            Assert.True(File.Exists(csvPath));
            var lines = File.ReadAllLines(csvPath);
            Assert.Contains(lines, l => l.StartsWith("0,AKA_Love/games,"));
            Assert.Contains(lines, l => l.StartsWith("1,py,"));
        }
    }
}
