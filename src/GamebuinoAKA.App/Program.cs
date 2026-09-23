using System;
using Avalonia;

namespace GamebuinoAKA.App
{
    internal static class Program
    {
        // Point d'entrée. Ne touchez pas à Avalonia avant BuildAvaloniaApp.
        [STAThread]
        public static void Main(string[] args) =>
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
