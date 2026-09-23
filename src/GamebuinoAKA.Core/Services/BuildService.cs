using System;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>Route chaque commande vers PlatformIO ou ESP-IDF selon project.IsEspIdf.</summary>
    public sealed class BuildService : IBuildService
    {
        private readonly IPlatformIOService _pio;
        private readonly IEspIdfService _idf;

        public BuildService(IPlatformIOService pio, IEspIdfService idf)
        {
            _pio = pio;
            _idf = idf;
        }

        public Task BuildAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => p.IsEspIdf ? _idf.BuildAsync(p, o, ct) : _pio.BuildAsync(p, o, ct);

        public Task FlashAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => p.IsEspIdf ? _idf.FlashAsync(p, o, ct) : _pio.FlashAsync(p, o, ct);

        public Task MonitorAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => p.IsEspIdf ? _idf.MonitorAsync(p, o, ct) : _pio.MonitorAsync(p, o, ct);

        public Task CleanAsync(GamebuinoProject p, Action<string>? o, CancellationToken ct = default)
            => p.IsEspIdf ? _idf.CleanAsync(p, o, ct) : _pio.CleanAsync(p, o, ct);
    }
}
