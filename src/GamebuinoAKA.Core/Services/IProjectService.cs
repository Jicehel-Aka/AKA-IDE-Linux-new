using System.Collections.Generic;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    public interface IProjectService
    {
        /// <summary>Scanne le workspace (dossiers contenant platformio.ini ou CMakeLists.txt).</summary>
        Task<List<GamebuinoProject>> ScanWorkspaceAsync();

        /// <summary>Projets récents encore présents sur le disque.</summary>
        Task<List<GamebuinoProject>> GetRecentProjectsAsync();

        /// <summary>Supprime le dossier du projet et le retire des récents.</summary>
        void DeleteProject(GamebuinoProject project);
    }
}
