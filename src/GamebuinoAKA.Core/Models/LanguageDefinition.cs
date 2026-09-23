using System.Collections.Generic;

namespace GamebuinoAKA.Core.Models
{
    /// <summary>
    /// Tout ce qui distingue un langage dans l'éditeur : DES DONNÉES, jamais de code spécifique à
    /// l'éditeur lui-même (coloration, transfert) — voir la note d'architecture dans
    /// LanguageDefinitionService. C'est ce qui permet d'ajouter un langage (BASIC, par exemple) sans
    /// toucher à l'éditeur ni au service de transfert.
    /// </summary>
    public sealed class LanguageDefinition
    {
        /// <summary>Identifie le langage (Lua, MicroPython…).</summary>
        public CodeLanguage Language { get; init; }

        /// <summary>Nom affiché dans le sélecteur de langage de l'éditeur.</summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Extension de fichier par défaut, SANS le point (ex. "lua", "py").</summary>
        public string FileExtension { get; init; } = string.Empty;

        /// <summary>
        /// Nom de la définition de coloration syntaxique AvaloniaEdit (fichier .xshd embarqué dans
        /// GamebuinoAKA.App/Assets/Highlighting/, chargé par nom — voir CodeEditorViewModel).
        /// </summary>
        public string HighlightingName { get; init; } = string.Empty;

        /// <summary>
        /// Octet <c>lang_id</c> du protocole AKAT (voir docs/PROTOCOLE_TRANSFERT.md d'AKA-Love).
        /// Doit rester STABLE une fois publié : c'est un contrat d'octets sur le fil, pas un simple index.
        /// </summary>
        public byte TransferLangId { get; init; }

        /// <summary>
        /// Mots-clés du langage lui-même (pas l'API AKA), pour une coloration simple par mot entier —
        /// AvaloniaEdit gère aussi des grammaires plus riches (.xshd), ceci sert de repli minimal et à
        /// l'autocomplétion basique.
        /// </summary>
        public IReadOnlyList<string> Keywords { get; init; } = System.Array.Empty<string>();

        /// <summary>
        /// Fonctions/valeurs de l'API AKA propre à ce langage (ex. <c>love.graphics.newImage</c>,
        /// <c>aka.buttons</c>), pour l'autocomplétion et une coloration distincte des mots-clés du
        /// langage. Un sous-ensemble volontairement couvrant plutôt qu'exhaustif — voir la remarque en
        /// tête de LanguageDefinitionService pour comment l'étendre.
        /// </summary>
        public IReadOnlyList<string> ApiSymbols { get; init; } = System.Array.Empty<string>();

        /// <summary>
        /// Modèle de fichier proposé à la création d'un nouveau script dans ce langage (un conf.lua +
        /// main.lua minimal pour Lua, un main.py minimal pour MicroPython…).
        /// </summary>
        /// <summary>
        /// Dossier des scripts de ce langage, RELATIF À LA RACINE DE LA CARTE SD (pas au dossier d'un
        /// firmware) — ex. "AKA_Love/games" pour Lua, "py" pour MicroPython (à plat, sans sous-dossier).
        /// Correspond à la ligne de <c>AKA/languages.csv</c> pour ce langage (voir
        /// docs/PROTOCOLE_TRANSFERT.md du dépôt AKA-Love) : c'est ce fichier, dans ce dossier de
        /// paramètres AKA-IDE, qui fait AUTORITÉ — le firmware sur la carte SD relit sa propre copie de
        /// <c>AKA/languages.csv</c> à chaque démarrage, régénérée à partir d'ici par ExportLanguagesCsv().
        /// </summary>
        public string SdScriptsRoot { get; init; } = string.Empty;

        public string NewFileTemplate { get; init; } = string.Empty;
    }
}
