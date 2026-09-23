using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    public class SoundBankServiceTests
    {
        private static (SoundBankService svc, SettingsService settings, string ws) Make(TempPlatformPaths paths)
        {
            var settings = new SettingsService(paths);
            var ws = Path.Combine(paths.Root, "ws");
            Directory.CreateDirectory(ws);
            settings.Settings.WorkspaceFolder = ws;
            return (new SoundBankService(settings), settings, ws);
        }

        [Fact]
        public void LoadBank_NoFile_ReturnsEmpty()
        {
            using var paths = new TempPlatformPaths();
            var (svc, _, _) = Make(paths);
            var bank = svc.LoadBank();
            Assert.NotNull(bank);
            Assert.Empty(bank.Assets);
        }

        [Fact]
        public void SaveLoad_Bank_RoundTrips()
        {
            using var paths = new TempPlatformPaths();
            var (svc, _, _) = Make(paths);
            var bank = new SoundBank();
            bank.Assets.Add(new SoundAsset { Name = "boom", AssetType = SoundAssetType.SoundFx });
            svc.SaveBank(bank);

            var loaded = svc.LoadBank();
            Assert.Single(loaded.Assets);
            Assert.Equal("boom", loaded.Assets[0].Name);
        }

        [Fact]
        public void ImportFile_Wav_AddsSoundFx()
        {
            using var paths = new TempPlatformPaths();
            var (svc, _, ws) = Make(paths);
            var wav = Path.Combine(ws, "bip.wav");
            File.WriteAllBytes(wav, new byte[] { 0x52, 0x49, 0x46, 0x46 }); // "RIFF"
            var bank = new SoundBank();

            var asset = svc.ImportFile(wav, bank);

            Assert.Single(bank.Assets);
            Assert.Equal(SoundAssetType.SoundFx, asset.AssetType);
        }

        [Fact]
        public async Task ScanProject_ClassifiesWavAndHeaders()
        {
            using var paths = new TempPlatformPaths();
            var (svc, _, ws) = Make(paths);
            var proj = Path.Combine(ws, "jeu");
            Directory.CreateDirectory(proj);
            File.WriteAllBytes(Path.Combine(proj, "saut.wav"), new byte[] { 0x52, 0x49, 0x46, 0x46 });
            File.WriteAllText(Path.Combine(proj, "theme.h"), "const uint8_t PMF_DATA[] = {0};");
            File.WriteAllText(Path.Combine(proj, "fx.h"), "const uint16_t WAV_SYSTEM[] = {0};");

            var found = await svc.ScanProjectAsync(
                new GamebuinoProject { Name = "jeu", FolderPath = proj }, new SoundBank());

            Assert.Contains(found, a => a.FileExtension == ".wav" && a.AssetType == SoundAssetType.SoundFx);
            Assert.Contains(found, a => a.AssetType == SoundAssetType.Music);   // theme.h (PMF)
        }

        [Fact]
        public void RemoveAsset_RemovesById()
        {
            using var paths = new TempPlatformPaths();
            var (svc, _, _) = Make(paths);
            var bank = new SoundBank();
            var a = new SoundAsset { Name = "x" };
            bank.Assets.Add(a);
            svc.RemoveAsset(a, bank);
            Assert.Empty(bank.Assets);
        }
    }
}
