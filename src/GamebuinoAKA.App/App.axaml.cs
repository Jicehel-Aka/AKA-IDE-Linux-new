using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace GamebuinoAKA.App
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var main = Bootstrapper.CreateMainViewModel();
                desktop.MainWindow = new MainWindow { DataContext = main };
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
