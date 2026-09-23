using GamebuinoAKA.App.Platform;
using GamebuinoAKA.App.Services;
using GamebuinoAKA.App.ViewModels;
using GamebuinoAKA.Core.Platform;
using GamebuinoAKA.Core.Services;

namespace GamebuinoAKA.App
{
    public static class Bootstrapper
    {
        public static MainViewModel CreateMainViewModel()
        {
            IPlatformPaths paths = PlatformPathsFactory.Create();
            var settings = new SettingsService(paths);
            Log.Configure(new FileLogService(paths));

            var runner    = new SystemProcessRunner();
            var tools     = new ToolLocator();
            var projects  = new ProjectService(settings);
            var templates = new TemplateService(settings);
            var pio       = new PlatformIOService(settings, runner, tools);
            var idf       = new EspIdfService(settings, runner);
            var build     = new BuildService(pio, idf);
            var git       = new GitService(settings, runner, tools);
            var vscode    = new VSCodeService(settings, runner, tools);
            var launcher  = new ApplicationLauncher(runner);
            var asset     = new AssetService(settings);
            var soundBank = new SoundBankService(settings);
            var snippets  = new CodeSnippetService(settings);
            var languages = new LanguageDefinitionService(settings);
            var transfer  = new SerialTransferService();
            var checker   = new CodeCheckService();

            var files     = new AvaloniaFileDialogService();
            var dialogs   = new AvaloniaDialogService();

            return new MainViewModel(settings, projects, templates, build, git,
                                     vscode, launcher, pio, idf, asset, soundBank, snippets,
                                     languages, transfer, checker, files, dialogs);
        }
    }
}
