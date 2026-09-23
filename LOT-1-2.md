# Lot 1-2 — IPlatformPaths + SettingsService portables (+ CI à jour)

À déposer sur la branche `avalonia` (écrase/ajoute ces fichiers), puis :

```bash
git add .
git commit -m "Lot 1-2: IPlatformPaths + SettingsService portables + tests; CI actions v7/v6"
git push origin avalonia
```

## Contenu

```
src/GamebuinoAKA.Core/
├── Models/AppSettings.cs            # migré : données pures (plus de chemin en dur)
├── Platform/IPlatformPaths.cs       # NEW : Config/Data/Cache/DefaultWorkspace
└── Services/
    ├── ISettingsService.cs          # NEW
    └── SettingsService.cs           # migré : portable, via IPlatformPaths, fallback défauts

src/GamebuinoAKA.App/Platform/
├── LinuxPlatformPaths.cs            # NEW : XDG (XDG_CONFIG_HOME → ~/.config/GamebuinoAKA)
├── WindowsPlatformPaths.cs          # NEW : %APPDATA% / Documents (comportement WPF conservé)
└── PlatformPathsFactory.cs          # NEW : choisit l'impl selon l'OS (détection OS côté App)

tests/GamebuinoAKA.Core.Tests/
└── SettingsServiceTests.cs          # NEW : défauts, round-trip JSON, JSON invalide→fallback,
                                     #        création du dossier, récents (dédup + plafond)

.github/workflows/ci.yml             # actions montées en v7/v6 (fin du warning Node 20)
```

## Points clés

- **Core ne connaît aucun chemin OS.** Il passe par `IPlatformPaths`. Sous Linux,
  la config va dans `$XDG_CONFIG_HOME/GamebuinoAKA` (fallback `~/.config/...`).
- **Comportement Windows inchangé** : `%APPDATA%\GamebuinoAKA\settings.json`,
  workspace par défaut sous `Documents\GamebuinoAKA`.
- **Robuste** : fichier absent ou JSON corrompu ⇒ valeurs par défaut, sans exception.
- **Testé sans UI** : les tests injectent un `IPlatformPaths` pointant un dossier
  temporaire ; aucun accès à la vraie config utilisateur.

Le branchement dans la DI d'App (`PlatformPathsFactory.Create()` →
`SettingsService`) se fera au moment où App aura sa racine de composition
(lot UI). D'ici là, `SettingsService` est déjà pleinement testable.

## Vérifs

```bash
dotnet build GamebuinoAKA.sln -c Release
dotnet test  GamebuinoAKA.sln -c Release   # doit afficher les 5 tests SettingsService + 2 enums
```

## Lot suivant proposé

Lot 3 : journalisation portable (`Log`) écrivant dans `IPlatformPaths.DataDirectory`
(thread-safe, création auto du dossier, rotation), avec tests. Puis Lot 4
(Project/Template services).
