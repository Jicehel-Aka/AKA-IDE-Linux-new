# Lot 10 — Première interface Avalonia utilisable (tableau de bord)

⚠️ **UI Avalonia non compilée de mon côté** (pas d'Avalonia dans mon environnement).
À valider au build ; on corrigera les erreurs éventuelles ensemble via la CI.

Cumulatif sur les lots 0-9 + 11/13. **Écrase** les fichiers du squelette (lot 0) :
`App.csproj`, `App.axaml`, `App.axaml.cs`, `MainWindow.axaml(.cs)`.

## Contenu (tout dans src/GamebuinoAKA.App/)
```
GamebuinoAKA.App.csproj        # + CommunityToolkit.Mvvm (écrase)
App.axaml / App.axaml.cs       # ViewLocator + composition au démarrage (écrase)
ViewLocator.cs                 # VM → Vue par convention de nom
Bootstrapper.cs                # racine de composition (branche TOUT le Core)
MainWindow.axaml(.cs)          # coquille : barre de navigation + ContentControl (écrase)
ViewModels/
  ViewModelBase.cs, MainViewModel.cs (INavigationService),
  HomeViewModel, ProjectsViewModel, NewProjectViewModel, SettingsViewModel
Views/
  HomeView, ProjectsView, NewProjectView, SettingsView (.axaml + .axaml.cs)
Services/
  AvaloniaFileDialogService.cs (IFileDialogService via StorageProvider)
  AvaloniaDialogService.cs     (IDialogService, fenêtre construite en code)
```

## Ce que ça fait
- **Projets** : scan du workspace, Build / Flash / Monitor (sortie en direct dans
  un panneau log), Ouvrir dans VS Code, Ouvrir le dossier, Supprimer (confirmation),
  Cloner depuis une URL GitHub.
- **Nouveau projet** : nom, chaîne (ESP-IDF/PlatformIO), template, dossier
  (sélecteur natif), création via TemplateService.
- **Paramètres** : workspace, chemins pio/code, script export ESP-IDF, port série,
  composant de référence, chaîne par défaut ; Auto-détecter + Enregistrer.
- Sprite/Tilemap : message « à venir » (lots 12/14).

Tout est branché sur le Core testé (SettingsService, ProjectService, TemplateService,
BuildService, GitService, VSCodeService, ApplicationLauncher, ToolLocator, ProcessRunner).

## Points à surveiller au build (là où ça peut coincer)
1. **Générateurs CommunityToolkit** (`[ObservableProperty]`/`[RelayCommand]`) : ils
   créent `XxxCommand` et les propriétés. Si un nom bindé ne correspond pas, c'est ici.
2. **ViewLocator** : mappe `App.ViewModels.XxxViewModel` → `App.Views.XxxView`.
3. **Bindings par réflexion** (`x:CompileBindings="False"`) : plus tolérants ; les
   erreurs de binding seront des warnings au runtime, pas des erreurs de compil.
4. **API Avalonia 11.2** (StorageProvider, RelativeSource) : si tu montes de version,
   quelques signatures peuvent bouger.
5. `Program.cs` et `Platform/*` (lots 0 et 1-2) sont **conservés** (non inclus ici).

## Vérif
`dotnet build` doit produire l'exécutable ; `dotnet run --project src/GamebuinoAKA.App`
ouvre la fenêtre. Les ~64 tests Core restent verts (rien touché côté Core).
