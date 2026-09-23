namespace GamebuinoAKA.Core.Models
{
    /// <summary>
    /// Chaîne de build d'un projet.
    ///
    /// PlatformIO  : framework Arduino + lib jmp42/Gamebuino_AKA_lib, build via `pio`.
    /// EspIdf      : ESP-IDF natif, composants CMake, coquille + core + shell,
    ///               build via `idf.py` (sous Linux : export.sh, bash, /dev/ttyUSB*).
    /// </summary>
    public enum BuildSystem
    {
        PlatformIO = 0,
        EspIdf = 1
    }
}
