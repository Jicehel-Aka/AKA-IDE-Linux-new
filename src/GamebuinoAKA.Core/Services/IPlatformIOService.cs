using System.Threading.Tasks;

namespace GamebuinoAKA.Core.Services
{
    public interface IPlatformIOService : IBuildBackend
    {
        Task<string> GetVersionAsync();
        /// <summary>Chemin résolu de pio (ou vide si introuvable).</summary>
        string DetectPioPath();
    }
}
