# Lot 0 — Socle Avalonia + CI (branche `avalonia`)

Objectif : mettre en place l'architecture cible Core/App/Tests, une CI Ubuntu
verte, et migrer les 2 premiers modèles 100 % portables (enums). C'est le filet
sur lequel s'appuieront tous les lots suivants.

## Étapes (une seule fois)

Sur la branche `avalonia`, depuis la racine du dépôt :

```bash
git checkout -b avalonia        # si elle n'existe pas encore
# 1) Ranger le WPF actuel comme référence historique (non compilé)
mkdir -p legacy-wpf
git mv src legacy-wpf/src
git mv GamebuinoAKA.IDE.sln legacy-wpf/ 2>/dev/null || true
```

Puis **déposer le contenu de ce zip à la racine du dépôt** (il crée `src/`,
`tests/`, `GamebuinoAKA.sln`, `.github/workflows/ci.yml`).

```bash
git add .
git commit -m "Lot 0: socle Avalonia (Core/App/Tests) + CI Ubuntu + enums portables"
git push origin avalonia
```

## Vérifier en local (si tu as le SDK .NET 10)

```bash
dotnet restore GamebuinoAKA.sln
dotnet build   GamebuinoAKA.sln -c Release
dotnet test    GamebuinoAKA.sln -c Release
```

La CI GitHub Actions rejoue exactement ces 3 commandes sur Ubuntu à chaque push.

## Ce que contient ce lot

```
src/
├── GamebuinoAKA.Core/            # métier portable (aucune UI)
│   ├── GamebuinoAKA.Core.csproj  # CommunityToolkit.Mvvm + Newtonsoft
│   └── Models/
│       ├── BuildSystem.cs        # enum (migré, namespace GamebuinoAKA.Core.Models)
│       └── ColorFormat.cs        # enum (idem)
└── GamebuinoAKA.App/             # Avalonia (net10.0, PAS -windows)
    ├── GamebuinoAKA.App.csproj
    ├── Program.cs / App.axaml(.cs) / MainWindow.axaml(.cs) / app.manifest
tests/
└── GamebuinoAKA.Core.Tests/      # xUnit
    └── EnumsTests.cs             # fige les valeurs d'enums
GamebuinoAKA.sln                  # référence UNIQUEMENT Core/App/Tests
.github/workflows/ci.yml          # restore + build + test (Release) sur Ubuntu
```

## Notes

- **Versions de paquets** : Avalonia est épinglé en 11.2.3 et xUnit/Test.Sdk en
  versions stables connues. Si le restore réclame plus récent, bumpe avec
  `dotnet add package Avalonia --version <x>` (idem Desktop/Themes.Fluent/Inter).
- **`legacy-wpf/` n'est PAS dans la solution** : il n'est jamais compilé par la
  CI (il est Windows-only). Il reste comme source de vérité à porter, lot par lot.
- **Namespaces** : le code migré passe de `GamebuinoAKA.IDE.*` à
  `GamebuinoAKA.Core.*` / `GamebuinoAKA.App.*`, conformément à l'architecture cible.
- **MVVM** : `CommunityToolkit.Mvvm` (et non l'ancien `Microsoft.Toolkit.Mvvm`).
  Au moment de porter les ViewModels, ce sera surtout un remplacement de
  namespace `using`.

## Lot suivant proposé

Lot 1-2 : `IPlatformPaths` (+ implémentations Linux/Windows dans App) et
`SettingsService` portable dans Core, avec les tests (dossier config XDG,
save/load JSON, valeurs par défaut, JSON invalide → fallback).
