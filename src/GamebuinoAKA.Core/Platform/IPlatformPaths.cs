namespace GamebuinoAKA.Core.Platform
{
    /// <summary>
    /// Abstraction des emplacements de fichiers, résolue par plateforme dans App
    /// (Linux : XDG ; Windows : %APPDATA% / %LOCALAPPDATA% / Documents).
    /// Core ne code JAMAIS %APPDATA% ni C:\ en dur : il passe par cette interface.
    /// </summary>
    public interface IPlatformPaths
    {
        /// <summary>Configuration utilisateur (settings.json). Linux : $XDG_CONFIG_HOME/GamebuinoAKA.</summary>
        string ConfigDirectory { get; }

        /// <summary>Données persistantes (journaux…). Linux : $XDG_DATA_HOME/GamebuinoAKA.</summary>
        string DataDirectory { get; }

        /// <summary>Caches jetables. Linux : $XDG_CACHE_HOME/GamebuinoAKA.</summary>
        string CacheDirectory { get; }

        /// <summary>Dossier de projets proposé par défaut (workspace).</summary>
        string DefaultWorkspaceDirectory { get; }
    }
}
