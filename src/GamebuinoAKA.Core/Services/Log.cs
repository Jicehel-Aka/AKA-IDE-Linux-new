using System;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Façade statique de journalisation, appelable de partout (y compris les
    /// gestionnaires d'exceptions globaux) sans passer par la DI. Elle délègue à
    /// un ILogService configuré une seule fois au démarrage via Configure().
    /// Tant qu'elle n'est pas configurée, les appels sont sans effet (no-op) —
    /// jamais d'exception.
    /// </summary>
    public static class Log
    {
        private static ILogService? _service;

        /// <summary>À appeler une fois au démarrage d'App (racine de composition).</summary>
        public static void Configure(ILogService service) => _service = service;

        public static bool IsConfigured => _service != null;

        public static string LogFolder => _service?.LogFolder ?? string.Empty;
        public static string LogFilePath => _service?.LogFilePath ?? string.Empty;

        public static void Info(string message) => _service?.Info(message);
        public static void Warn(string message) => _service?.Warn(message);
        public static void Error(string message, Exception? ex = null) => _service?.Error(message, ex);

        public static bool Clear() => _service?.Clear() ?? false;
    }
}
