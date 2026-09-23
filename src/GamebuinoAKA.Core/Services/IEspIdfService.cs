using System.Threading.Tasks;

namespace GamebuinoAKA.Core.Services
{
    public interface IEspIdfService : IBuildBackend
    {
        Task<string> GetVersionAsync();
        /// <summary>Ports série candidats (Linux : /dev/ttyUSB*, /dev/ttyACM*).</summary>
        string[] DetectSerialPorts();
    }
}
