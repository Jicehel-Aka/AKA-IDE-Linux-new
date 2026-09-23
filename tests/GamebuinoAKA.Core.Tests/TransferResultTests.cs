using GamebuinoAKA.Core.Services;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    /// <summary>
    /// Vérifie le contrat de TransferResult (voir docs/PROTOCOLE_TRANSFERT.md du dépôt AKA-Love pour la
    /// spécification des statuts v1.1). N'exerce PAS SerialTransferService lui-même (nécessite un vrai
    /// port série) — juste la petite logique de TransferResult.Success, facile à faire mal (oublier
    /// OkRenamed dans le "ou", par exemple).
    /// </summary>
    public class TransferResultTests
    {
        [Theory]
        [InlineData(TransferStatus.Ok, true)]
        [InlineData(TransferStatus.OkRenamed, true)]
        [InlineData(TransferStatus.Cancelled, false)]
        [InlineData(TransferStatus.BadPath, false)]
        [InlineData(TransferStatus.NoResponse, false)]
        public void Success_MatchesStatus(TransferStatus status, bool expected)
        {
            var result = new TransferResult(status, 0);
            Assert.Equal(expected, result.Success);
        }

        [Fact]
        public void OkRenamed_CarriesFinalDevicePath()
        {
            var result = new TransferResult(TransferStatus.OkRenamed, 0, "cassebriques/main (2).lua");
            Assert.True(result.Success);
            Assert.Equal("cassebriques/main (2).lua", result.FinalDevicePath);
        }

        [Fact]
        public void Ok_HasNoFinalDevicePathByDefault()
        {
            var result = new TransferResult(TransferStatus.Ok, 0);
            Assert.Null(result.FinalDevicePath);
        }
    }
}
