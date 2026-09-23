using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class ProjectServiceTests
    {
        private static (SettingsService settings, string workspace) NewWorkspace(TempPlatformPaths paths)
        {
            var settings = new SettingsService(paths);
            var workspace = Path.Combine(paths.Root, "ws");
            Directory.CreateDirectory(workspace);
            settings.Settings.WorkspaceFolder = workspace;
            return (settings, workspace);
        }

        [Fact]
        public async Task ScanWorkspace_DetectsPioAndIdf_IgnoresOthers()
        {
            using var paths = new TempPlatformPaths();
            var (settings, ws) = NewWorkspace(paths);

            // Projet PlatformIO
            var pio = Path.Combine(ws, "proj_pio");
            Directory.CreateDirectory(Path.Combine(pio, "src"));
            File.WriteAllText(Path.Combine(pio, "platformio.ini"), "[env:aka]\n");
            File.WriteAllText(Path.Combine(pio, "src", "main.cpp"), "// Hello Gamebuino\n");

            // Projet ESP-IDF
            var idf = Path.Combine(ws, "proj_idf");
            Directory.CreateDirectory(Path.Combine(idf, "main"));
            File.WriteAllText(Path.Combine(idf, "CMakeLists.txt"), "include($ENV{IDF_PATH}/tools/cmake/project.cmake)\n");
            File.WriteAllText(Path.Combine(idf, "main", "app_main.cpp"), "extern \"C\" void app_main(){}\n");

            // Dossier non-projet
            Directory.CreateDirectory(Path.Combine(ws, "rien"));

            var svc = new ProjectService(settings);
            var projects = await svc.ScanWorkspaceAsync();

            Assert.Equal(2, projects.Count);

            var p = projects.Single(x => x.Name == "proj_pio");
            Assert.Equal(BuildSystem.PlatformIO, p.BuildSystem);
            Assert.Equal("hello-world", p.Template);

            var e = projects.Single(x => x.Name == "proj_idf");
            Assert.Equal(BuildSystem.EspIdf, e.BuildSystem);
            Assert.Equal("esp-idf", e.Template);
        }

        [Fact]
        public async Task ScanWorkspace_EmptyOrMissing_ReturnsEmpty()
        {
            using var paths = new TempPlatformPaths();
            var settings = new SettingsService(paths);
            settings.Settings.WorkspaceFolder = Path.Combine(paths.Root, "inexistant");

            var svc = new ProjectService(settings);
            var projects = await svc.ScanWorkspaceAsync();

            Assert.Empty(projects);
        }

        [Fact]
        public void DeleteProject_RemovesFolderAndRecentEntry()
        {
            using var paths = new TempPlatformPaths();
            var (settings, ws) = NewWorkspace(paths);
            var dir = Path.Combine(ws, "todelete");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "platformio.ini"), "x");
            settings.Settings.RecentProjects.Add(dir);

            var svc = new ProjectService(settings);
            svc.DeleteProject(new GamebuinoProject { Name = "todelete", FolderPath = dir });

            Assert.False(Directory.Exists(dir));
            Assert.DoesNotContain(dir, settings.Settings.RecentProjects);
        }
    }
}
