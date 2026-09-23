using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using GamebuinoAKA.Core.Services;

namespace GamebuinoAKA.App.Services
{
    /// <summary>Dialogues fichier Avalonia (StorageProvider) — implémente IFileDialogService.</summary>
    public sealed class AvaloniaFileDialogService : IFileDialogService
    {
        private static Window? MainWindow =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        public async Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter>? filters = null)
        {
            var top = MainWindow;
            if (top is null) return null;
            var opts = new FilePickerOpenOptions { Title = title, AllowMultiple = false };
            if (filters != null) opts.FileTypeFilter = ToTypes(filters);
            var res = await top.StorageProvider.OpenFilePickerAsync(opts);
            return res.Count > 0 ? res[0].Path.LocalPath : null;
        }

        public async Task<string?> SaveFileAsync(string title, string? suggestedName = null, IReadOnlyList<FileFilter>? filters = null)
        {
            var top = MainWindow;
            if (top is null) return null;
            var opts = new FilePickerSaveOptions { Title = title, SuggestedFileName = suggestedName };
            if (filters != null) opts.FileTypeChoices = ToTypes(filters);
            var file = await top.StorageProvider.SaveFilePickerAsync(opts);
            return file?.Path.LocalPath;
        }

        public async Task<string?> OpenFolderAsync(string title, string? startPath = null)
        {
            var top = MainWindow;
            if (top is null) return null;
            var res = await top.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            return res.Count > 0 ? res[0].Path.LocalPath : null;
        }

        private static List<FilePickerFileType> ToTypes(IReadOnlyList<FileFilter> filters) =>
            filters.Select(f => new FilePickerFileType(f.Name)
            {
                Patterns = f.Extensions.Select(e => "*." + e).ToArray()
            }).ToList();
    }
}
