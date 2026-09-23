# Lot 3 — Journalisation portable

Migration de `Log` (statique, chemin %APPDATA% en dur) vers Core, écrivant dans
`IPlatformPaths.DataDirectory`, thread-safe, avec rotation, testable.

## Contenu
```
src/GamebuinoAKA.Core/Services/
├── ILogService.cs        # NEW : Info/Warn/Error(+Exception)/Clear + LogFolder/LogFilePath
├── FileLogService.cs     # NEW : journal fichier (DataDirectory), lock, rotation .old, erreurs avalées
└── Log.cs                # façade STATIQUE (API inchangée) déléguant à un ILogService configuré

tests/GamebuinoAKA.Core.Tests/
├── TempPlatformPaths.cs      # helper réutilisable (IPlatformPaths → dossier temp)
└── FileLogServiceTests.cs    # NEW : 9 tests
```

## Choix de conception
- **Injectable + statique** : `FileLogService` (testable, prend `IPlatformPaths`)
  et façade `Log` statique (appelable depuis les handlers d'exceptions globaux,
  API identique à l'ancienne : `Log.Info/Warn/Error/Clear`, `Log.LogFilePath`).
- **Linux** : le journal va dans `$XDG_DATA_HOME/GamebuinoAKA` (fallback
  `~/.local/share/GamebuinoAKA`). **Windows** : `%LOCALAPPDATA%\GamebuinoAKA`.
- Un logger ne doit jamais planter l'appli : les erreurs d'écriture sont avalées.
- Rotation : au-delà de ~1 Mo, le journal courant devient `.old` (seuil réglable
  au constructeur pour les tests).

## Tests (9)
Écriture + création du dossier ; niveau + horodatage présents ; exception
sérialisée ; 100 écritures = 100 lignes ; 200 écritures concurrentes bien
formées (thread-safety) ; rotation `.old` ; `Clear()` ; façade `Log` no-op avant
`Configure` et délégation après.

## À faire côté App (lot UI / composition root)
Au démarrage, une fois `IPlatformPaths` choisi :
```csharp
var paths = PlatformPathsFactory.Create();
Log.Configure(new FileLogService(paths));
```
Ainsi tous les `Log.Error(...)` des handlers globaux écriront au bon endroit.

## Vérif
`dotnet test` doit maintenant afficher 16 tests (2 enums + 5 settings + 9 log).
