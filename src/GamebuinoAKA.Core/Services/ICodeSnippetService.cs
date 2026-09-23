using System.Collections.Generic;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    public interface ICodeSnippetService
    {
        List<CodeSnippet> GetAll(BuildSystem? forBuild = null);
        Dictionary<string, string> GenerateFiles(IEnumerable<CodeSnippet> selected, BuildSystem buildSystem, string projectName);
        SnippetBank LoadUserBank();
        void SaveUserBank(SnippetBank bank);
    }
}
