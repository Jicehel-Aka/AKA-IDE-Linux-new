# Lot 15 — Sons + snippets (Core testé) + doc + finalisation

Cumulatif sur lots 0-14. Écrase `MainViewModel.cs`, `Bootstrapper.cs`, `MainWindow.axaml`.

## Core (portable, testé)
- Models : `CodeSnippet` (+SnippetBank/SnippetTargetFile), `SoundAsset`, `SoundBank` — migrés.
- Services : `CodeSnippetService` (+ `ICodeSnippetService`), `SoundBankService`
  (+ `ISoundBankService`) — migrés. **SoundPlayer Win32 retiré** (lecture déléguée
  à l'UI via IApplicationLauncher).
- Tests : `CodeSnippetServiceTests` (5) + `SoundBankServiceTests` (5).

## UI Avalonia
- `SoundBankViewModel` + `SoundBankView` : scan projets, import, lecture (ouverture
  système), suppression.
- `SnippetsViewModel` + `SnippetsView` : bibliothèque de procédures (PlatformIO /
  ESP-IDF), code affiché.
- Navigation : boutons « Banque de sons » et « Procédures ».

## Documentation
- `README.md` (racine) : architecture, build/run, fonctionnalités, notes de portage.

## Vérif
`dotnet test` : ~**80 tests**. `dotnet run --project src/GamebuinoAKA.App`.

## Raffinements restants (logique déjà dans Core)
- Retouche pixel + palette + undo dans l'éditeur de sprites.
- Sélecteur de snippets à la création de projet (injection via
  CodeSnippetService.GenerateFiles).
