using System;
using System.IO;
using System.Text;
using GamebuinoAKA.Core.Platform;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Journal fichier thread-safe, écrit dans IPlatformPaths.DataDirectory
    /// (Linux : $XDG_DATA_HOME/GamebuinoAKA ; Windows : %LOCALAPPDATA%\GamebuinoAKA).
    /// Rotation basique : au-delà de maxBytes, l'ancien devient « .old ».
    /// Les erreurs d'écriture sont avalées : journaliser ne doit jamais planter l'appli.
    /// </summary>
    public sealed class FileLogService : ILogService
    {
        private const string FileName = "gamebuino-ide.log";
        private readonly object _lock = new object();
        private readonly IPlatformPaths _paths;
        private readonly long _maxBytes;

        /// <param name="maxBytes">Seuil de rotation (défaut ~1 Mo ; réglable pour les tests).</param>
        public FileLogService(IPlatformPaths paths, long maxBytes = 1_000_000)
        {
            _paths = paths;
            _maxBytes = maxBytes > 0 ? maxBytes : 1_000_000;
        }

        public string LogFolder => _paths.DataDirectory;
        public string LogFilePath => Path.Combine(LogFolder, FileName);

        public void Info(string message) => Write("INFO", message, null);
        public void Warn(string message) => Write("WARN", message, null);
        public void Error(string message, Exception? ex = null) => Write("ERREUR", message, ex);

        private void Write(string level, string message, Exception? ex)
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(LogFolder);
                    var path = LogFilePath;

                    if (File.Exists(path) && new FileInfo(path).Length > _maxBytes)
                    {
                        var old = path + ".old";
                        try { if (File.Exists(old)) File.Delete(old); File.Move(path, old); }
                        catch { try { File.Delete(path); } catch { /* ignore */ } }
                    }

                    var sb = new StringBuilder();
                    sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                      .Append(" [").Append(level).Append("] ")
                      .Append(message);
                    if (ex != null)
                        sb.Append(Environment.NewLine).Append(ex);
                    sb.Append(Environment.NewLine);

                    File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
                }
                catch { /* la journalisation ne doit jamais faire échouer l'appli */ }
            }
        }

        public bool Clear()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(LogFilePath)) File.Delete(LogFilePath);
                    var old = LogFilePath + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    return true;
                }
                catch { return false; }
            }
        }
    }
}
