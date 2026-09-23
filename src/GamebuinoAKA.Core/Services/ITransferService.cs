using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>Résultat d'un envoi de fichier vers l'appareil (protocole AKAT).</summary>
    public enum TransferStatus
    {
        Ok = 0,
        BadLanguage = 1,
        BadPath = 2,
        CrcMismatch = 3,
        WriteError = 4,
        TooLarge = 5,

        /// <summary>
        /// Écrit, mais sous un autre nom : l'appareil était sur son écran interactif "Recevoir un code"
        /// et le joueur a choisi "Renommer" au lieu d'écraser un fichier existant. Le nom réellement
        /// utilisé est dans <see cref="TransferResult.FinalDevicePath"/>.
        /// </summary>
        OkRenamed = 6,

        /// <summary>Le joueur a choisi "Annuler" (ou n'a pas répondu à temps) sur l'écran interactif.</summary>
        Cancelled = 7,

        /// <summary>Pas de réponse dans le délai — port absent, câble débranché, ou appareil pas prêt.</summary>
        NoResponse = 100,
    }

    /// <summary>
    /// <paramref name="FinalDevicePath"/> n'est renseigné que pour <see cref="TransferStatus.OkRenamed"/>
    /// (le chemin réellement utilisé, différent de celui demandé — voir docs/PROTOCOLE_TRANSFERT.md,
    /// section "Réponse OK_RENAMED").
    /// </summary>
    public sealed record TransferResult(TransferStatus Status, byte? DeviceId, string? FinalDevicePath = null)
    {
        public bool Success => Status is TransferStatus.Ok or TransferStatus.OkRenamed;
    }

    /// <summary>
    /// Envoie un fichier vers une console AKA par le port série, avec le protocole « AKAT » v1 —
    /// voir docs/PROTOCOLE_TRANSFERT.md dans le dépôt AKA-Love pour la spécification complète et
    /// components/akalove/transfer.cpp pour l'implémentation de référence côté appareil.
    /// Interface volontairement minimale (Ping + Put) : LIST/GET restent pour une itération future,
    /// pas nécessaires pour un simple bouton « Envoyer vers l'AKA ».
    /// </summary>
    public interface ITransferService
    {
        /// <summary>Vérifie qu'un appareil AKA écoute sur ce port (sans toucher à la carte SD).</summary>
        Task<TransferResult> PingAsync(string portName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Envoie <paramref name="localFilePath"/> vers <paramref name="devicePath"/> (relatif à la
        /// racine du langage — voir <see cref="LanguageDefinition.TransferLangId"/>).
        /// </summary>
        Task<TransferResult> PutFileAsync(string portName, CodeLanguage language, string devicePath,
            string localFilePath, CancellationToken cancellationToken = default);
    }
}
