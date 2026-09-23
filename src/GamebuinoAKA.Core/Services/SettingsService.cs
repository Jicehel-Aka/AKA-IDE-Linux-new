using System.IO;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Platform;
using Newtonsoft.Json;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Chargement/sauvegarde des paramètres, portable. Le dossier de config vient
    /// d'IPlatformPaths (XDG sous Linux, %APPDATA% sous Windows). Un fichier absent
    /// ou illisible retombe proprement sur les valeurs par défaut.
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        private const string FileName = "settings.json";
        private readonly IPlatformPaths _paths;
        private AppSettings _settings = new AppSettings();

        public AppSettings Settings => _settings;

        public string SettingsFilePath => Path.Combine(_paths.ConfigDirectory, FileName);

        public SettingsService(IPlatformPaths paths)
        {
            _paths = paths;
            Load();
        }

        public void Load()
        {
            try
            {
                _settings = File.Exists(SettingsFilePath)
                    ? JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(SettingsFilePath)) ?? new AppSettings()
                    : new AppSettings();
            }
            catch
            {
                // JSON corrompu / illisible → valeurs par défaut (jamais d'exception ici).
                _settings = new AppSettings();
            }

            if (string.IsNullOrEmpty(_settings.WorkspaceFolder))
                _settings.WorkspaceFolder = _paths.DefaultWorkspaceDirectory;
        }

        public void Save()
        {
            Directory.CreateDirectory(_paths.ConfigDirectory);
            File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(_settings, Formatting.Indented));
        }

        public void AddRecentProject(string folderPath)
        {
            _settings.RecentProjects.Remove(folderPath);
            _settings.RecentProjects.Insert(0, folderPath);
            while (_settings.RecentProjects.Count > _settings.MaxRecentProjects)
                _settings.RecentProjects.RemoveAt(_settings.RecentProjects.Count - 1);
            Save();
        }
    }
}
