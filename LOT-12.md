# Lot 12 — Éditeur de sprites Avalonia

⚠️ **UI Avalonia non compilée de mon côté.** À valider au build ; on corrige ensemble.
Cumulatif sur lots 0-11. **Écrase** `Bootstrapper.cs`, `MainViewModel.cs`, `MainWindow.axaml`.

## Fichiers (src/GamebuinoAKA.App/)
```
Controls/PixelCanvas.cs             # NEW : affichage zoom nearest + sélection rectangulaire au glisser
Services/SkiaImageBridge.cs         # NEW : SKBitmap (Core) → Bitmap Avalonia (via PNG)
ViewModels/SpriteEditorViewModel.cs # NEW : import / planche / sélection / rogner / réduire / convertir / export / .gbspr
Views/SpriteEditorView.axaml(.cs)   # NEW : outils + canvas + panneau code
ViewModels/MainViewModel.cs         # écrase : ajoute l'éditeur + navigation
Bootstrapper.cs                     # écrase : construit AssetService
MainWindow.axaml                    # écrase : bouton « Éditeur de sprites »
```

## Flux couvert
Importer image / planche → **glisser** pour sélectionner → **Rogner** → régler la
taille cible (garder proportions / lissage) → **Réduire** → **Convertir** (aperçu
code, garde de taille écran 320×240, hors thread UI) → **Exporter C++** →
**Enregistrer/Ouvrir .gbspr**. Transparence par couleur-clé.

## Volontairement reporté (raffinement)
Retouche pixel par pixel + palette + undo (grosse surface non testable). Le flux
« découper une planche → réduire → exporter », qui est l'essentiel, est là.

## Points chauds au build
1. **PixelCanvas** : contrôle custom (Render + pointer). API Avalonia 11.2
   (DrawingContext.DrawImage(src, srcRect, destRect), RenderOptions.SetBitmapInterpolationMode,
   StyledProperty two-way). C'est le fichier le plus susceptible d'avoir un détail à ajuster.
2. **SkiaImageBridge** : encode PNG puis Bitmap Avalonia — ok, mais coûteux (acceptable pour un sprite).
3. Générateurs CommunityToolkit (`OnTargetWidthChanged`, `[RelayCommand]`).

## La suite (finalisation)
- **Lot 14** : éditeur de tilemaps (contrôle TilemapGrid + VM + vue) — même recette.
- **Lot 15** : banque de sons + snippets (services Core testables, puis vues).
Je les enchaîne dès que le 12 build. Empiler 14+15 non testés ici serait risqué.
