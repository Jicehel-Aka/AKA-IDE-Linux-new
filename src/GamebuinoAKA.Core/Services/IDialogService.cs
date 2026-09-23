using System.Threading.Tasks;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>Messages et confirmations, abstraits de l'UI (fini MessageBox WPF).</summary>
    public interface IDialogService
    {
        Task ShowMessageAsync(string title, string message);
        Task<bool> ConfirmAsync(string title, string message);
    }
}
