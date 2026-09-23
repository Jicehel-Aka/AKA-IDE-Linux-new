using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Platform
{
    /// <summary>
    /// Implémentation via System.Diagnostics.Process (portable Linux/Windows).
    /// UseShellExecute = false + ArgumentList : pas de shell, espaces gérés.
    /// </summary>
    public sealed class SystemProcessRunner : IProcessRunner
    {
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using var process = Start(request);
            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
        }

        public async Task<int> RunStreamingAsync(ProcessRequest request, Action<string>? onOutput,
            CancellationToken cancellationToken = default)
        {
            using var process = Start(request);
            process.OutputDataReceived += (_, e) => { if (e.Data != null) onOutput?.Invoke(e.Data); };
            process.ErrorDataReceived  += (_, e) => { if (e.Data != null) onOutput?.Invoke(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode;
        }

        private static Process Start(ProcessRequest request)
        {
            var psi = new ProcessStartInfo
            {
                FileName = request.FileName,
                WorkingDirectory = request.WorkingDirectory ?? string.Empty,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var arg in request.Arguments)
                psi.ArgumentList.Add(arg);   // espaces préservés, pas d'injection shell

            var process = new Process { StartInfo = psi };
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                process.Dispose();
                throw new InvalidOperationException(
                    $"Impossible de lancer « {request.FileName} » : {ex.Message}", ex);
            }
            return process;
        }
    }
}
