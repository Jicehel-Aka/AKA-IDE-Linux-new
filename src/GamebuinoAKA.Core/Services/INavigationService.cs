namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Navigation entre pages, abstraite (fini App.Services.GetRequiredService).
    /// L'implémentation (hôte de navigation) vit dans App.
    /// </summary>
    public interface INavigationService
    {
        void NavigateToHome();
        void NavigateToProjects();
        void NavigateToNewProject();
        void NavigateToSettings();
        void NavigateToSpriteEditor();
        void NavigateToTilemapEditor();
    }
}
