# Catalogue aligné PlatformIO / ESP-IDF (53 procédures)

Remplace la version précédente. Écrase 2 fichiers.

## Répartition
- **32 PlatformIO** (API Arduino `gb.*`).
- **21 ESP-IDF** (API coquille `gfx` / `g_core`) — alignées sur les concepts PlatformIO :
  entrées, pause, fond, formes, texte, sprite, jauge de vie, score+record, chrono,
  collision AABB, machine à états, animation, aléatoire, entités, gravité, vélocité,
  rebond, caméra, overlay debug, écran titre, menu.

## Changements de cohérence
- **`BuildAppMain`** ne déplace plus le joueur automatiquement : le déplacement est
  désormais fourni par `esp_input` (comme `input_buttons` côté PlatformIO). Ainsi
  `esp_velocity` / `esp_gravity` sont de vraies alternatives sans conflit.
- `esp_gfx_text` n'occupe plus la variable `score` (c'est `esp_score` qui la possède).

## Non portés en ESP-IDF (et pourquoi)
- **Audio** (beep, wav, effets, jingles, mélodie) : le squelette `app_main` minimal
  ne câble pas la sortie audio (le mixeur `gb_audio_player` doit être alimenté par une
  tâche, comme dans la coquille complète `core/audio.cpp`). À faire quand on intègrera
  cette plomberie — sinon les snippets ne produiraient aucun son.
- **Sauvegarde** : côté ESP-IDF elle passe par NVS (`nvs_flash`), API assez verbeuse ;
  à ajouter séparément.

## Fichiers (à écraser)
```
src/GamebuinoAKA.Core/Services/CodeSnippetService.cs
tests/GamebuinoAKA.Core.Tests/CodeSnippetServiceTests.cs
```

## Vérif
`dotnet test` : les tests snippets passent (filtrage robuste + présence ESP-IDF ≥ 15
+ injection dans app_main).
