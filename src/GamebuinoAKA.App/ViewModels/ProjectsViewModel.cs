using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Platform;
using GamebuinoAKA.Core.Services;

namespace GamebuinoAKA.App.ViewModels
{
    public sealed partial class ProjectsViewModel : ViewModelBase
    {
        private readonly IProjectService _projects;
        private readonly IBuildService _build;
        private readonly IGitService _git;
        private readonly IVSCodeService _vscode;
        private readonly IApplicationLauncher _launcher;
        private readonly ISettingsService _settings;
        private readonly INavigationService _nav;
        private readonly IDialogService _dialogs;

        public ObservableCollection<GamebuinoProject> Items { get; } = new();

        [ObservableProperty] private GamebuinoProject? _selectedProject;
        [ObservableProperty] private string _outputLog = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _cloneUrl = string.Empty;

        public ProjectsViewModel(IProjectService projects, IBuildService build, IGitService git,
            IVSCodeService vscode, IApplicationLauncher launcher, ISettingsService settings,
            INavigationService nav, IDialogService dialogs)
        {
            _projects = projects; _build = build; _git = git; _vscode = vscode;
            _launcher = launcher; _settings = settings; _nav = nav; _dialogs = dialogs;
        }

        public async Task RefreshAsync()
        {
            var list = await _projects.ScanWorkspaceAsync();
            Items.Clear();
            foreach (var p in list) Items.Add(p);
        }

        [RelayCommand] private Task Refresh() => RefreshAsync();
        [RelayCommand] private Task Build(GamebuinoProject? p) => RunOp(p, _build.BuildAsync);
        [RelayCommand] private Task Flash(GamebuinoProject? p) => RunOp(p, _build.FlashAsync);
        [RelayCommand] private Task Monitor(GamebuinoProject? p) => RunOp(p, _build.MonitorAsync);

        [RelayCommand]
        private void OpenVSCode(GamebuinoProject? p)
        {
            p ??= SelectedProject; if (p is null) return;
            try { _settings.AddRecentProject(p.FolderPath); _vscode.OpenProject(p); }
            catch (Exception ex) { AppendLine("Erreur : " + ex.Message); }
        }

        [RelayCommand]
        private void OpenFolder(GamebuinoProject? p)
        {
            p ??= SelectedProject; if (p is null) return;
            _launcher.OpenFolder(p.FolderPath);
        }

        [RelayCommand]
        private async Task Delete(GamebuinoProject? p)
        {
            p ??= SelectedProject; if (p is null) return;
            if (await _dialogs.ConfirmAsync("Supprimer", $"Supprimer le projet « {p.Name} » ?"))
            {
                _projects.DeleteProject(p);
                Items.Remove(p);
            }
        }

        [RelayCommand] private void NewProject() => _nav.NavigateToNewProject();

        [RelayCommand]
        private async Task Clone()
        {
            if (string.IsNullOrWhiteSpace(CloneUrl)) return;
            IsBusy = true; OutputLog = string.Empty;
            try { await _git.CloneAsync(CloneUrl, null, AppendLine); await RefreshAsync(); }
            catch (Exception ex) { AppendLine("Erreur : " + ex.Message); }
            finally { IsBusy = false; }
        }

        private async Task RunOp(GamebuinoProject? p,
            Func<GamebuinoProject, Action<string>?, CancellationToken, Task> op)
        {
            p ??= SelectedProject; if (p is null) return;
            IsBusy = true; OutputLog = string.Empty;
            try { await op(p, AppendLine, default); }
            catch (Exception ex) { AppendLine("Erreur : " + ex.Message); }
            finally { IsBusy = false; }
        }

        private void AppendLine(string line)
            => Dispatcher.UIThread.Post(() => OutputLog += line + "\n");
    }
}
