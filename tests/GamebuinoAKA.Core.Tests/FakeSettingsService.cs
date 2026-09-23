using System;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;

namespace GamebuinoAKA.Core.Tests
{
    /// <summary>Faux ISettingsService minimal, pour les tests qui n'ont besoin que de SettingsFilePath
    /// (LanguageDefinitionServiceTests notamment) — évite de dépendre d'un vrai fichier sur disque.</summary>
    internal sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public string SettingsFilePath { get; }

        public FakeSettingsService(string directory) =>
            SettingsFilePath = System.IO.Path.Combine(directory, "settings.json");

        public void Load() { }
        public void Save() { }
        public void AddRecentProject(string folderPath) { }
    }
}
