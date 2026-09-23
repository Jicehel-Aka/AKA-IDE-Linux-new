using System.Text;
using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    /// <summary>
    /// Vecteurs de référence calculés avec zlib.crc32 (Python) — le même algorithme que le protocole
    /// AKAT attend côté appareil (transfer.cpp) et côté outil PC (tools/akatransfer.py). Vérifie que
    /// les trois implémentations calculeraient bien le même CRC pour les mêmes octets.
    /// </summary>
    public class Crc32Tests
    {
        [Fact]
        public void Compute_EmptyData_IsZero()
        {
            Assert.Equal(0x00000000u, Crc32.Compute(System.Array.Empty<byte>()));
        }

        [Fact]
        public void Compute_KnownString_MatchesPythonZlib()
        {
            var data = Encoding.UTF8.GetBytes("AKA-Love");
            Assert.Equal(0xEDB82EDAu, Crc32.Compute(data));
        }
    }
}
