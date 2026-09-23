namespace GamebuinoAKA.Core.Platform
{
    /// <summary>
    /// Ouvre dossiers/fichiers/URL avec l'outil système :
    /// Linux → xdg-open ; Windows → explorer.exe / association système.
    /// </summary>
    public interface IApplicationLauncher
    {
        void OpenFolder(string folderPath);
        void OpenFile(string filePath);
        void OpenUrl(string url);
    }
}
