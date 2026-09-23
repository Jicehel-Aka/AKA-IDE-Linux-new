using GamebuinoAKA.Core.Models;
using Xunit;

namespace GamebuinoAKA.Core.Tests
{
    /// <summary>
    /// Premiers tests : figent les valeurs numériques des enums (elles sont
    /// sérialisées telles quelles dans les paramètres/projets — ne pas les casser).
    /// </summary>
    public class EnumsTests
    {
        [Fact]
        public void BuildSystem_HasStableNumericValues()
        {
            Assert.Equal(0, (int)BuildSystem.PlatformIO);
            Assert.Equal(1, (int)BuildSystem.EspIdf);
        }

        [Fact]
        public void ColorFormat_DefaultIsBgr565Aka()
        {
            // La valeur par défaut (0) doit rester l'ordre lib AKA.
            Assert.Equal(0, (int)ColorFormat.Bgr565Aka);
            Assert.Equal(1, (int)ColorFormat.Rgb565Std);
            Assert.Equal(ColorFormat.Bgr565Aka, default(ColorFormat));
        }
    }
}
