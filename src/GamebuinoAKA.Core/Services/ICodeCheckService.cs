using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Vérifie un code source AVANT de l'envoyer à l'AKA (voir ITransferService) — voir
    /// CodeCheckResult pour ce que ce contrôle couvre, et surtout ce qu'il NE couvre PAS.
    /// </summary>
    public interface ICodeCheckService
    {
        CodeCheckResult Check(string code, CodeLanguage language);
    }
}
