using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebuinoAKA.Core.Platform;
using GamebuinoAKA.Core.Services;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.App.ViewModels
{
    public sealed partial class MainViewModel : ViewModelBase, INavigationService
    {
        public HomeViewModel Home { get; }
        public ProjectsViewModel Projects { get; }
        public NewProjectViewModel NewProject { get; }
        public SettingsViewModel Settings { get; }
        public SpriteEditorViewModel SpriteEditor { get; }
        public TilemapEditorViewModel TilemapEditor { get; }
        public SoundBankViewModel SoundBank { get; }
        public SnippetsViewModel Snippets { get; }
        public CodeEditorViewModel CodeEditor { get; }

        [ObservableProperty] private object? _currentPage;

        public MainViewModel(
            ISettingsService settings, IProjectService projects, ITemplateService templates,
            IBuildService build, IGitService git, IVSCodeService vscode,
            IApplicationLauncher launcher, IPlatformIOService pio, IEspIdfService idf,
            AssetService asset, ISoundBankService soundBank, ICodeSnippetService snippets,
            ILanguageDefinitionService languages, ITransferService transfer, ICodeCheckService checker,
            IFileDialogService files, IDialogService dialogs)
        {
            Home = new HomeViewModel(this);
            Projects = new ProjectsViewModel(projects, build, git, vscode, launcher, settings, this, dialogs);
            NewProject = new NewProjectViewModel(templates, settings, this, files, dialogs);
            Settings = new SettingsViewModel(settings, pio, idf, vscode, files);
            SpriteEditor = new SpriteEditorViewModel(asset, settings, files, dialogs);
            TilemapEditor = new TilemapEditorViewModel(asset, files);
            SoundBank = new SoundBankViewModel(soundBank, projects, launcher, files);
            Snippets = new SnippetsViewModel(snippets);
            CodeEditor = new CodeEditorViewModel(languages, transfer, checker, files, settings);

            NavigateToProjects();
        }

        [RelayCommand] public void NavigateToHome() => CurrentPage = Home;
        [RelayCommand] public void NavigateToProjects() { _ = Projects.RefreshAsync(); CurrentPage = Projects; }
        [RelayCommand] public void NavigateToNewProject() => CurrentPage = NewProject;
        [RelayCommand] public void NavigateToSettings() => CurrentPage = Settings;
        [RelayCommand] public void NavigateToSpriteEditor() => CurrentPage = SpriteEditor;
        [RelayCommand] public void NavigateToTilemapEditor() => CurrentPage = TilemapEditor;
        [RelayCommand] public void NavigateToSoundBank() => CurrentPage = SoundBank;
        [RelayCommand] public void NavigateToSnippets() => CurrentPage = Snippets;
        [RelayCommand] public void NavigateToCodeEditor() => CurrentPage = CodeEditor;
    }
}
