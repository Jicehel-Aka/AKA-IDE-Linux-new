using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Platform;
using GamebuinoAKA.Core.Services;

namespace GamebuinoAKA.App.ViewModels
{
    public sealed partial class SoundBankViewModel : ViewModelBase
    {
        private readonly ISoundBankService _bank;
        private readonly IProjectService _projects;
        private readonly IApplicationLauncher _launcher;
        private readonly IFileDialogService _files;
        private SoundBank _data;

        public ObservableCollection<SoundAsset> Assets { get; } = new();

        [ObservableProperty] private SoundAsset? _selected;
        [ObservableProperty] private string _status = "Banque de sons.";
        [ObservableProperty] private bool _isBusy;

        public SoundBankViewModel(ISoundBankService bank, IProjectService projects,
            IApplicationLauncher launcher, IFileDialogService files)
        {
            _bank = bank; _projects = projects; _launcher = launcher; _files = files;
            _data = _bank.LoadBank();
            Reload();
        }

        private void Reload()
        {
            Assets.Clear();
            foreach (var a in _data.Assets) Assets.Add(a);
        }

        [RelayCommand]
        private async Task ScanProjects()
        {
            IsBusy = true;
            try
            {
                int added = 0;
                foreach (var p in await _projects.ScanWorkspaceAsync())
                {
                    var found = await _bank.ScanProjectAsync(p, _data);
                    foreach (var a in found) { _data.Assets.Add(a); added++; }
                }
                _bank.SaveBank(_data);
                Reload();
                Status = $"{added} son(s) ajouté(s) depuis les projets.";
            }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task Import()
        {
            var path = await _files.OpenFileAsync("Importer un son",
                new[] { new FileFilter("Audio AKA", new[] { "wav", "pmf", "h" }) });
            if (path is null) return;
            try { _bank.ImportFile(path, _data); Reload(); Status = "Son importé."; }
            catch (Exception ex) { Status = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        private void Play(SoundAsset? a)
        {
            a ??= Selected; if (a is null) return;
            var target = string.IsNullOrEmpty(a.PreviewWavPath) ? a.FilePath : a.PreviewWavPath;
            if (!string.IsNullOrEmpty(target)) _launcher.OpenFile(target);
        }

        [RelayCommand]
        private void Remove(SoundAsset? a)
        {
            a ??= Selected; if (a is null) return;
            _bank.RemoveAsset(a, _data);
            Assets.Remove(a);
        }
    }
}
