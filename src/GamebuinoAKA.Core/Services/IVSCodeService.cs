using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    public interface IVSCodeService
    {
        string DetectVSCodePath();
        bool IsInstalled();
        void OpenProject(GamebuinoProject project);
        void OpenFolder(string folderPath);
        string GetDisplayPath();
    }
}
