namespace GamebuinoAKA.Core.Models
{
    /// <summary>
    /// Ordre d'empaquetage des composantes couleur dans le uint16 exporté.
    ///
    /// La lib Gamebuino AKA (lcd_color_rgb / graphics_make_color) empaquette :
    ///     u16 = (R>>3) | ((G>>2)<<5) | ((B>>3)<<11)
    /// c.-à-d. ROUGE en bits de poids faible, BLEU en bits de poids fort =
    /// ordre « BGR565 ». C'est donc le format PAR DÉFAUT : un tableau exporté en
    /// Bgr565Aka se blitte tel quel avec graphics_draw_bitmap565() sans inversion.
    ///
    /// Rgb565Std : ordre standard (R en bits hauts), laissé en option.
    /// </summary>
    public enum ColorFormat
    {
        /// <summary>Ordre lib AKA : R bits 0-4, G bits 5-10, B bits 11-15. (défaut)</summary>
        Bgr565Aka = 0,

        /// <summary>Ordre standard : R bits 11-15, G bits 5-10, B bits 0-4.</summary>
        Rgb565Std = 1
    }
}
