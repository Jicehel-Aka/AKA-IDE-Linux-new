using System;
using System.Threading;
using System.Threading.Tasks;

namespace GamebuinoAKA.Core.Services
{
    public interface IGitService
    {
        /// <summary>Vrai si git est localisable (PATH ou emplacement connu).</summary>
        Task<bool> IsInstalledAsync();

        /// <summary>
        /// Clone repoUrl dans &lt;workspace&gt;/&lt;folderName&gt; et diffuse la sortie.
        /// Renvoie le chemin du dossier cloné.
        /// </summary>
        Task<string> CloneAsync(string repoUrl, string? folderName,
            Action<string>? onOutput, CancellationToken ct = default);
    }
}
