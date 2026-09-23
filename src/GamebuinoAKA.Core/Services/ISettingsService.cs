using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    public interface ISettingsService
    {
        /// <summary>Paramètres courants (jamais null).</summary>
        AppSettings Settings { get; }

        /// <summary>Chemin effectif du fichier de paramètres.</summary>
        string SettingsFilePath { get; }

        /// <summary>Recharge depuis le disque (fallback valeurs par défaut si absent/illisible).</summary>
        void Load();

        /// <summary>Écrit sur le disque (crée le dossier si besoin).</summary>
        void Save();

        /// <summary>Ajoute un projet en tête de la liste des récents (dédoublonné, plafonné).</summary>
        void AddRecentProject(string folderPath);
    }
}
