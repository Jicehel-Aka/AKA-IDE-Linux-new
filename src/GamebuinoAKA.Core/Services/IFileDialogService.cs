using System.Collections.Generic;
using System.Threading.Tasks;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>Filtre de dialogue fichier (nom + extensions sans le point).</summary>
    public sealed record FileFilter(string Name, IReadOnlyList<string> Extensions);

    /// <summary>
    /// Abstraction des dialogues fichier. L'implémentation Avalonia (StorageProvider)
    /// vit dans App ; les ViewModels ne dépendent que de cette interface.
    /// </summary>
    public interface IFileDialogService
    {
        Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter>? filters = null);
        Task<string?> SaveFileAsync(string title, string? suggestedName = null, IReadOnlyList<FileFilter>? filters = null);
        Task<string?> OpenFolderAsync(string title, string? startPath = null);
    }
}
