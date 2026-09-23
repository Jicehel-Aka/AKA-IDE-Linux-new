using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Platform;

namespace GamebuinoAKA.Core.Tests
{
    /// <summary>IProcessRunner factice : enregistre chaque requête, renvoie un résultat réglable.</summary>
    public sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = new();
        public ProcessRequest? Last => Requests.Count > 0 ? Requests[^1] : null;
        public int ExitToReturn { get; set; }
        public string StdOut { get; set; } = string.Empty;
        public string StdErr { get; set; } = string.Empty;

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(new ProcessResult(ExitToReturn, StdOut, StdErr));
        }

        public Task<int> RunStreamingAsync(ProcessRequest request, Action<string>? onOutput, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(ExitToReturn);
        }
    }

    /// <summary>IToolLocator factice : table nom → chemin (ou absent = introuvable).</summary>
    public sealed class FakeToolLocator : IToolLocator
    {
        public Dictionary<string, string?> Map { get; } = new();

        public string? Locate(string toolName, string? configuredPath = null)
            => Map.TryGetValue(toolName, out var v) ? v : null;
    }
}
