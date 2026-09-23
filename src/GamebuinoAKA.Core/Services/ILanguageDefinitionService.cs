using System.Collections.Generic;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    public interface ILanguageDefinitionService
    {
        /// <summary>Tous les langages connus de l'éditeur, dans l'ordre d'affichage du sélecteur.</summary>
        IReadOnlyList<LanguageDefinition> All { get; }

        LanguageDefinition Get(CodeLanguage language);

        /// <summary>Devine le langage d'après l'extension d'un chemin de fichier (".lua", ".py"…).</summary>
        LanguageDefinition? FromFilePath(string path);

        /// <summary>
        /// Chemin du fichier <c>languages.json</c> modifiable (à côté des paramètres de l'application) —
        /// pour un futur écran « Réglages → Langages », ou pour l'ouvrir dans un éditeur de texte en
        /// attendant. Modifier ce fichier puis relancer AKA-IDE change les chemins proposés par défaut
        /// dans l'éditeur de code ET ce que <see cref="ExportLanguagesCsv"/> écrit sur la carte SD.
        /// </summary>
        string ConfigFilePath { get; }

        /// <summary>
        /// Écrit <c>AKA/languages.csv</c> (voir docs/PROTOCOLE_TRANSFERT.md du dépôt AKA-Love) sous
        /// <paramref name="sdRootFolder"/> — typiquement le dossier <c>SD_files</c> assemblé pour une
        /// release, ou directement la racine d'une carte SD montée. Une seule source de vérité
        /// (<see cref="ConfigFilePath"/>) pour l'éditeur ET pour ce qui part sur la carte.
        /// </summary>
        void ExportLanguagesCsv(string sdRootFolder);
    }
}
