using System.Collections.Generic;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    public interface ISoundBankService
    {
        string BankFilePath { get; }
        SoundBank LoadBank();
        void SaveBank(SoundBank bank);
        Task<List<SoundAsset>> ScanProjectAsync(GamebuinoProject project, SoundBank existingBank);
        SoundAsset ImportFile(string filePath, SoundBank bank);
        void RemoveAsset(SoundAsset asset, SoundBank bank);
        void UpdateAsset(SoundAsset updated, SoundBank bank);
    }
}
