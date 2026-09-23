using System.Collections.Generic;

namespace GamebuinoAKA.Core.Models
{
    /// <summary>
    /// Paramètres applicatifs — DONNÉES PURES et portables.
    /// La localisation du fichier de config n'est PLUS ici : elle est fournie par
    /// IPlatformPaths (voir SettingsService), pour rester multiplateforme.
    /// </summary>
    public class AppSettings
    {
        public string WorkspaceFolder { get; set; } = string.Empty;

        // ── PlatformIO ─────────────────────────────────────────────────────────
        public string PlatformIOPath { get; set; } = string.Empty;
        public string GamebuinoLibRepoUrl { get; set; } = "https://github.com/jmp42/Gamebuino_AKA_lib";

        // ── ESP-IDF ────────────────────────────────────────────────────────────
        /// <summary>
        /// Chemin vers idf.py, OU vers le script d'export qui met idf.py dans le PATH
        /// (Windows : export.bat ; Linux : export.sh). Vide = « idf.py » du PATH.
        /// </summary>
        public string IdfPyPath { get; set; } = string.Empty;

        /// <summary>Script d'environnement ESP-IDF à sourcer (export.bat / export.sh). Optionnel.</summary>
        public string IdfExportScript { get; set; } = string.Empty;

        /// <summary>Port série pour flash/monitor (Windows : COM5 ; Linux : /dev/ttyUSB0). Vide = auto.</summary>
        public string IdfSerialPort { get; set; } = string.Empty;

        /// <summary>Dossier « components/gamebuino » de référence à copier dans un nouveau projet ESP-IDF.</summary>
        public string ReferenceGamebuinoComponentPath { get; set; } = string.Empty;

        // ── Éditeur / commun ────────────────────────────────────────────────────
        public string VSCodePath { get; set; } = string.Empty;
        public string Theme { get; set; } = "Dark";

        /// <summary>Chaîne de build proposée par défaut pour un nouveau projet.</summary>
        public BuildSystem DefaultBuildSystem { get; set; } = BuildSystem.EspIdf;

        /// <summary>Format d'export couleur par défaut (BGR565 AKA = correct pour la lib).</summary>
        public ColorFormat DefaultColorFormat { get; set; } = ColorFormat.Bgr565Aka;

        /// <summary>Couleur-clé de transparence par défaut (magenta 0xF81F).</summary>
        public ushort DefaultTransparentKey { get; set; } = 0xF81F;

        public List<string> RecentProjects { get; set; } = new List<string>();
        public int MaxRecentProjects { get; set; } = 10;
        public bool AutoDetectTools { get; set; } = true;
    }
}
