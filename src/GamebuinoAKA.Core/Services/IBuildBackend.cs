using System;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>Opérations communes à une chaîne de build (PlatformIO ou ESP-IDF).</summary>
    public interface IBuildBackend
    {
        Task BuildAsync(GamebuinoProject project, Action<string>? onOutput, CancellationToken ct = default);
        Task FlashAsync(GamebuinoProject project, Action<string>? onOutput, CancellationToken ct = default);
        Task MonitorAsync(GamebuinoProject project, Action<string>? onOutput, CancellationToken ct = default);
        Task CleanAsync(GamebuinoProject project, Action<string>? onOutput, CancellationToken ct = default);
    }
}
