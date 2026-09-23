using System;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Journalisation portable. Aucune UI, aucune boîte de dialogue : un logger ne
    /// doit jamais faire échouer l'application.
    /// </summary>
    public interface ILogService
    {
        /// <summary>Dossier contenant le journal.</summary>
        string LogFolder { get; }

        /// <summary>Chemin complet du fichier journal.</summary>
        string LogFilePath { get; }

        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception? ex = null);

        /// <summary>Supprime le journal et son historique (.old). true si OK.</summary>
        bool Clear();
    }
}
