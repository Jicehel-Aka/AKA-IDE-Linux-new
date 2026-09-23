using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using GamebuinoAKA.App.ViewModels;
using System.Xml;

namespace GamebuinoAKA.App.Views
{
    public partial class CodeEditorView : UserControl
    {
        private TextEditor? _editor;

        public CodeEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            _editor = this.FindControl<TextEditor>("Editor");
            DataContextChanged += (_, _) => ApplyHighlighting();
            AttachedToVisualTree += (_, _) => ApplyHighlighting();
        }

        // La grammaire (.xshd) dépend du langage choisi dans le ViewModel ; AvaloniaEdit ne fait pas ce
        // lien tout seul, donc on la (re)charge ici à chaque changement de contexte de données ou de
        // langage sélectionné (voir CodeEditorViewModel.SelectedLanguage).
        private void ApplyHighlighting()
        {
            if (_editor is null || DataContext is not CodeEditorViewModel vm) return;
            _editor.SyntaxHighlighting = LoadHighlighting(vm.SelectedLanguage.HighlightingName);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CodeEditorViewModel.SelectedLanguage))
                    _editor.SyntaxHighlighting = LoadHighlighting(vm.SelectedLanguage.HighlightingName);
            };
        }

        // Charge Assets/Highlighting/<name>.xshd (inclus comme AvaloniaResource, voir le .csproj) et
        // l'enregistre auprès du HighlightingManager (mis en cache : un seul chargement par nom).
        private static IHighlightingDefinition? LoadHighlighting(string name)
        {
            var existing = HighlightingManager.Instance.GetDefinition(name);
            if (existing is not null) return existing;

            var uri = new Uri($"avares://GamebuinoAKA.App/Assets/Highlighting/{name}.xshd");
            if (!AssetLoader.Exists(uri)) return null;

            using var stream = AssetLoader.Open(uri);
            using var reader = XmlReader.Create(stream);
            var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting(name, new[] { "." + name.ToLowerInvariant() }, definition);
            return definition;
        }
    }
}
