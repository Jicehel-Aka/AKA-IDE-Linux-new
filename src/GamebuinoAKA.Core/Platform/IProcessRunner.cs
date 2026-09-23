using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Platform
{
    /// <summary>
    /// Requête d'exécution. Les arguments sont SÉPARÉS (jamais une ligne shell
    /// concaténée) : ils passent par ProcessStartInfo.ArgumentList, ce qui gère
    /// correctement les espaces et évite toute injection shell.
    /// </summary>
    public sealed record ProcessRequest(
        string FileName,
        IReadOnlyList<string> Arguments,
        string? WorkingDirectory = null);

    public interface IProcessRunner
    {
        /// <summary>Exécute et renvoie le résultat complet (sortie bufferisée).</summary>
        Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exécute en diffusant stdout/stderr ligne par ligne à onOutput.
        /// Renvoie le code de sortie.
        /// </summary>
        Task<int> RunStreamingAsync(ProcessRequest request, Action<string>? onOutput,
            CancellationToken cancellationToken = default);
    }
}
