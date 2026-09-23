using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    public interface ITemplateService
    {
        /// <summary>
        /// Crée un projet. Pour ESP-IDF, withAudio=true génère la plomberie audio
        /// minimale (module audio.h/.cpp + tâche mixeur + init dans app_main).
        /// </summary>
        Task CreateProjectAsync(string projectName, string template,
            string destinationFolder, BuildSystem buildSystem, bool withAudio = false);
    }
}
