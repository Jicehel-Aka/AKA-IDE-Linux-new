# Lot 14 — Éditeur de tilemaps Avalonia

⚠️ UI Avalonia non compilée de mon côté — à valider au build (le Lot 12 est passé
du premier coup, bon signe). Cumulatif sur lots 0-12. **Écrase** `MainViewModel.cs`
et `MainWindow.axaml`.

## Fichiers (src/GamebuinoAKA.App/)
```
Controls/GridCanvas.cs               # NEW : grille cliquable (tileset + carte), zoom nearest, drag-paint
ViewModels/TilemapEditorViewModel.cs # NEW : import tileset, sélection tuile, peinture, calques, export, .gbmap
Views/TilemapEditorView.axaml(.cs)   # NEW : tileset (palette) + carte + outils
ViewModels/MainViewModel.cs          # écrase : + éditeur de tilemaps
MainWindow.axaml                     # écrase : bouton « Éditeur de tilemaps »
```

## Flux
Régler la taille de tuile → Importer un tileset → cliquer une tuile dans la palette
(surbrillance jaune) → choisir le calque (Fond / Premier plan) → peindre la carte
(clic/glisser) → régler colonnes/lignes (Appliquer) → Exporter C++ (bg/fg) /
Enregistrer/Ouvrir `.gbmap`. La carte est composée à la volée avec SkiaSharp
(chaque tuile blittée depuis le tileset), le premier plan laisse passer la tuile 0.

## Réutilise le Core testé
`AssetService.ImportTilesetAsync / ExportTilemapToCpp / Save/LoadTilemap /
UnpackToBitmap`, modèle `TilemapAsset`.

## Points chauds au build
- `GridCanvas` (même famille que PixelCanvas, qui a compilé) : Render + pointer,
  `CellCommand` reçoit un `Point(colonne, ligne)`.
- Composition SkiaSharp (`SKSurface.Create`, `canvas.DrawBitmap(src,dst)`).

## Reste pour finir le portage
**Lot 15** — banque de sons + snippets : services Core (testables) puis vues
Avalonia. C'est la dernière brique. Je l'enchaîne quand tu veux.
