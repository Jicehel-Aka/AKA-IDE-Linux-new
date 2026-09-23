using CommunityToolkit.Mvvm.Input;
using GamebuinoAKA.Core.Services;

namespace GamebuinoAKA.App.ViewModels
{
    public sealed partial class HomeViewModel : ViewModelBase
    {
        private readonly INavigationService _nav;
        public HomeViewModel(INavigationService nav) => _nav = nav;

        [RelayCommand] private void GoProjects() => _nav.NavigateToProjects();
        [RelayCommand] private void GoNewProject() => _nav.NavigateToNewProject();
        [RelayCommand] private void GoSettings() => _nav.NavigateToSettings();
    }
}
