namespace GamebuinoAKA.Core.Models
{
    /// <summary>
    /// Langage de programmation d'un fichier ouvert dans l'éditeur de code — commun à l'éditeur, à la
    /// coloration syntaxique et au transfert (protocole AKAT, voir docs/PROTOCOLE_TRANSFERT.md du dépôt
    /// AKA-Love). Ajouter un langage : un membre ici + une entrée dans LanguageDefinitionService.
    /// </summary>
    public enum CodeLanguage
    {
        /// <summary>Lua / Love2D, pour le firmware AKA-Love.</summary>
        Lua = 0,

        /// <summary>MicroPython, pour le firmware MicroPython AKA (module natif <c>aka</c>).</summary>
        MicroPython = 1,
    }
}
