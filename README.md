# Gamebuino AKA IDE — édition Linux (Avalonia)

IDE multiplateforme (.NET 10 + Avalonia) pour créer et gérer des jeux
**Gamebuino AKA** (ESP32-S3), en **PlatformIO** ou **ESP-IDF**. Port Linux de
l'IDE WPF d'origine, avec un cœur métier portable et testé.

## Architecture

```
legacy-wpf/                     Référence historique WPF (NON compilée)
src/
├── GamebuinoAKA.Core/          Métier PORTABLE — aucune dépendance UI
│   ├── Models/                 données (projets, assets, settings, snippets, sons…)
│   ├── Platform/               abstractions OS (IPlatformPaths, IProcessRunner,
│   │                           IToolLocator, IApplicationLauncher) + implémentations
│   └── Services/               settings, log, projets, templates, build (PIO+ESP-IDF),
│                               git, assets (SkiaSharp/BGR565), snippets, banque de sons
└── GamebuinoAKA.App/           UI Avalonia + détails de plateforme
    ├── Controls/               PixelCanvas, GridCanvas (dessin custom)
    ├── Services/               dialogues Avalonia, pont SkiaSharp→Avalonia
    ├── ViewModels/             VM (CommunityToolkit.Mvvm)
    └── Views/                  vues .axaml
tests/
└── GamebuinoAKA.Core.Tests/    tests unitaires (xUnit) — ~80 tests, sans UI
```

**Règle d'or :** `Core` ne référence jamais WPF, Avalonia, System.Drawing, ni de
chemin/outil Windows-only. La détection d'OS et les I/O système sont derrière des
interfaces, implémentées dans `App` (ou dans `Core` quand c'est du .NET pur portable).

## Prérequis

- **.NET SDK 10**
- Pour builder/flasher des jeux : **PlatformIO** (`pio`) et/ou **ESP-IDF** (`idf.py`)
- Optionnel : **VS Code** (`code`), **git**
- Linux : appartenir au groupe `dialout` pour l'accès aux ports série
  (`/dev/ttyUSB*`, `/dev/ttyACM*`)

## Build & exécution

```bash
dotnet restore GamebuinoAKA.sln
dotnet build   GamebuinoAKA.sln -c Release
dotnet test    GamebuinoAKA.sln -c Release        # ~80 tests
dotnet run --project src/GamebuinoAKA.App          # lance l'IDE
```

La CI GitHub Actions (`.github/workflows/ci.yml`) rejoue restore + build + test
sur Ubuntu à chaque push sur la branche `avalonia`.

## Fonctionnalités

- **Projets** : scan du workspace, Build / Flash / Monitor (PlatformIO **et**
  ESP-IDF), ouverture dans VS Code / l'explorateur de fichiers, suppression,
  clonage GitHub, sortie en direct.
- **Nouveau projet** : PlatformIO (Arduino) ou ESP-IDF (coquille + composants CMake).
- **Éditeur de sprites** : import image/planche, sélection au glisser, recadrage,
  réduction (proportions/lissage), transparence par couleur-clé, conversion C++,
  formats ré-éditables `.gbspr`. Couleurs **BGR565** (ordre lib AKA).
- **Éditeur de tilemaps** : tileset, palette de tuiles, calques fond/premier plan,
  peinture, export C++, `.gbmap`.
- **Banque de sons** : scan des projets, import, lecture (via lecteur système),
  classement (FX/musique) — `.wav`, `.pmf`, en-têtes `.h`.
- **Procédures (snippets)** : bibliothèque de blocs de code PlatformIO et ESP-IDF,
  injectables dans les fichiers générés.
- **Paramètres** : chemins outils, ESP-IDF (export.sh, port série), auto-détection.

## Notes de portage (WPF → Avalonia/Linux)

- **Images** : `System.Drawing` → **SkiaSharp** (multiplateforme, sans licence
  contraignante). Le format sprite reste du BGR565 truecolor (vérifié par tests).
- **Chemins** : `%APPDATA%` → **XDG** (`$XDG_CONFIG_HOME`, `$XDG_DATA_HOME`, …).
- **ESP-IDF** : `export.bat`/`cmd` → `export.sh`/`bash` ; ports `/dev/ttyUSB*`.
- **Processus** : `ProcessStartInfo.ArgumentList` (jamais de commande shell
  concaténée) ; outils localisés via `IToolLocator` (jamais de `.exe` en dur).
- **Audio** : `System.Media.SoundPlayer` (Win32) → ouverture via le lecteur système.
- **MVVM** : `Microsoft.Toolkit.Mvvm` → `CommunityToolkit.Mvvm`.

## Statut

Cœur métier **entièrement porté et testé**. UI Avalonia fonctionnelle pour tous
les écrans. La retouche pixel-par-pixel (palette/undo) de l'éditeur de sprites et
l'intégration du sélecteur de snippets à la création de projet sont des raffinements
prévus ; toute la logique sous-jacente est déjà dans `Core`.

## Licence

Voir le dépôt d'origine.
