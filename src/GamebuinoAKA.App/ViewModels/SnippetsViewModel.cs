using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;

namespace GamebuinoAKA.App.ViewModels
{
    public sealed partial class SnippetsViewModel : ViewModelBase
    {
        private readonly ICodeSnippetService _snippets;

        public ObservableCollection<CodeSnippet> Items { get; } = new();

        [ObservableProperty] private bool _isEspIdf;
        [ObservableProperty] private CodeSnippet? _selected;

        public string SelectedCode => Selected?.Code ?? string.Empty;
        public string SelectedExplanation => Selected?.Explanation ?? string.Empty;

        public SnippetsViewModel(ICodeSnippetService snippets)
        {
            _snippets = snippets;
            Reload();
        }

        partial void OnIsEspIdfChanged(bool value) => Reload();
        partial void OnSelectedChanged(CodeSnippet? value)
        {
            OnPropertyChanged(nameof(SelectedCode));
            OnPropertyChanged(nameof(SelectedExplanation));
        }

        private void Reload()
        {
            Items.Clear();
            foreach (var s in _snippets.GetAll(IsEspIdf ? BuildSystem.EspIdf : BuildSystem.PlatformIO))
                Items.Add(s);
        }
    }
}
