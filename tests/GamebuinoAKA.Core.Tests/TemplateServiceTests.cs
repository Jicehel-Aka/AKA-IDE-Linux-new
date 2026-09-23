using System.IO;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class TemplateServiceTests
    {
        [Fact]
        public async Task Create_PlatformIO_HelloWorld_WritesIniAndMain()
        {
            using var paths = new TempPlatformPaths();
            var settings = new SettingsService(paths);
            var dest = Path.Combine(paths.Root, "dest");
            Directory.CreateDirectory(dest);

            var svc = new TemplateService(settings);
            await svc.CreateProjectAsync("MonJeu", "hello-world", dest, BuildSystem.PlatformIO);

            var projDir = Path.Combine(dest, "MonJeu");
            Assert.True(File.Exists(Path.Combine(projDir, "platformio.ini")));
            Assert.True(File.Exists(Path.Combine(projDir, "src", "main.cpp")));
        }

        [Fact]
        public async Task Create_EspIdf_WritesCoquilleSkeleton_AndBuildMarker()
        {
            using var paths = new TempPlatformPaths();
            var settings = new SettingsService(paths);
            settings.Settings.ReferenceGamebuinoComponentPath = string.Empty;
            var dest = Path.Combine(paths.Root, "dest");
            Directory.CreateDirectory(dest);

            var svc = new TemplateService(settings);
            await svc.CreateProjectAsync("MonIdf", "esp-idf", dest, BuildSystem.EspIdf);

            var projDir = Path.Combine(dest, "MonIdf");
            Assert.True(File.Exists(Path.Combine(projDir, "CMakeLists.txt")));
            Assert.True(File.Exists(Path.Combine(projDir, "main", "app_main.cpp")));
            var marker = Path.Combine(projDir, GamebuinoProject.BuildMarkerFile);
            Assert.True(File.Exists(marker));
            Assert.Contains("espidf", File.ReadAllText(marker));
        }

        [Fact]
        public async Task Create_EspIdf_WithAudio_WritesAudioModule_AndWiresIt()
        {
            using var paths = new TempPlatformPaths();
            var settings = new SettingsService(paths);
            var dest = Path.Combine(paths.Root, "dest");
            Directory.CreateDirectory(dest);

            var svc = new TemplateService(settings);
            await svc.CreateProjectAsync("Sonore", "esp-idf", dest, BuildSystem.EspIdf, withAudio: true);

            var main = Path.Combine(dest, "Sonore", "main");
            Assert.True(File.Exists(Path.Combine(main, "audio.h")));
            Assert.True(File.Exists(Path.Combine(main, "audio.cpp")));
            Assert.Contains("audio.cpp", File.ReadAllText(Path.Combine(main, "CMakeLists.txt")));
            var appMain = File.ReadAllText(Path.Combine(main, "app_main.cpp"));
            Assert.Contains("audio_init", appMain);
            Assert.Contains("audio.h", appMain);
        }

        [Fact]
        public async Task Create_EspIdf_WithoutAudio_HasNoAudioModule()
        {
            using var paths = new TempPlatformPaths();
            var settings = new SettingsService(paths);
            var dest = Path.Combine(paths.Root, "dest");
            Directory.CreateDirectory(dest);

            var svc = new TemplateService(settings);
            await svc.CreateProjectAsync("Muet", "esp-idf", dest, BuildSystem.EspIdf, withAudio: false);

            var main = Path.Combine(dest, "Muet", "main");
            Assert.False(File.Exists(Path.Combine(main, "audio.cpp")));
            Assert.DoesNotContain("audio.cpp", File.ReadAllText(Path.Combine(main, "CMakeLists.txt")));
        }
    }
}
