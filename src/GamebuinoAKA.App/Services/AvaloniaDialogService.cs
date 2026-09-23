using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using GamebuinoAKA.Core.Services;

namespace GamebuinoAKA.App.Services
{
    /// <summary>Messages/confirmations Avalonia (fenêtre construite en code).</summary>
    public sealed class AvaloniaDialogService : IDialogService
    {
        private static Window? MainWindow =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        public Task ShowMessageAsync(string title, string message)
            => ShowAsync(title, message, ("OK", true));

        public Task<bool> ConfirmAsync(string title, string message)
            => ShowAsync(title, message, ("Oui", true), ("Non", false));

        private static async Task<bool> ShowAsync(string title, string message, params (string label, bool result)[] buttons)
        {
            var tcs = new TaskCompletionSource<bool>();

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
            stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 });

            var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            var win = new Window
            {
                Title = title,
                Width = 420,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = stack
            };
            foreach (var (label, result) in buttons)
            {
                var btn = new Button { Content = label, MinWidth = 80 };
                btn.Click += (_, __) => { tcs.TrySetResult(result); win.Close(); };
                bar.Children.Add(btn);
            }
            stack.Children.Add(bar);
            win.Closed += (_, __) => tcs.TrySetResult(false);

            var owner = MainWindow;
            if (owner != null) await win.ShowDialog(owner);
            else win.Show();
            return await tcs.Task;
        }
    }
}
