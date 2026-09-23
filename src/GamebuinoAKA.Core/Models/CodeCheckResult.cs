namespace GamebuinoAKA.Core.Models
{
    /// <summary>
    /// Résultat d'une vérification de code (voir ICodeCheckService). Un contrôle de STRUCTURE léger —
    /// équilibre des parenthèses/crochets/accolades, des chaînes et commentaires bien refermés, et pour
    /// Lua l'équilibre des mots-clés de bloc (if/for/while/do/function → end, repeat → until) — PAS un
    /// compilateur complet : du code structurellement correct mais sémantiquement faux (une variable qui
    /// n'existe pas, un type qui ne correspond pas) passe ce contrôle sans rien signaler. Objectif :
    /// attraper AVANT l'envoi les fautes de frappe les plus courantes (un "end" oublié, une parenthèse
    /// non refermée, une chaîne qui déborde sur la ligne suivante) plutôt que de les découvrir sur
    /// l'écran rouge de la console, après le transfert.
    /// </summary>
    public sealed record CodeCheckResult(bool Ok, int? Line, int? Column, string Message)
    {
        public static CodeCheckResult Success { get; } = new(true, null, null, string.Empty);
    }
}
