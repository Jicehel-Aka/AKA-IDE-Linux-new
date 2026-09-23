# Catalogue de procédures complet (PlatformIO + ESP-IDF) + tests

Remplace la version précédente. Écrase deux fichiers.

## Contenu du catalogue (39 procédures)
- **16 d'origine** (PlatformIO) : inchangées.
- **+16 PlatformIO** ré-ajoutées : `input_menu_pause`, `gfx_draw_line`,
  `gfx_healthbar`, `logic_animation`, `logic_random`, `logic_entities`,
  `physics_velocity`, `physics_bounce` (dépend de `physics_velocity`),
  `ui_title_screen`, `ui_simple_menu`, `sound_sfx_jump`, `sound_sfx_coin`,
  `sound_sfx_hurt`, `music_victory`, `music_gameover`, `sound_melody`.
- **+7 ESP-IDF** : `esp_input`, `esp_gfx_shapes`, `esp_gfx_text`, `esp_healthbar`,
  `esp_animation`, `esp_entities`, `esp_title`.

Soit **32 PlatformIO + 7 ESP-IDF**. Le sélecteur filtre par chaîne de build.

## Aussi
- `BuildAppMain` (génération ESP-IDF) enrichi : `g_core.pool()`, `playerX/playerY`
  + déplacement D-pad/joystick, zone `//@@SETUP@@`.
- Tests `CodeSnippetServiceTests` : filtrage robuste (cas vide) **et** vérifs
  réelles (procédures ESP-IDF présentes, injection dans `app_main.cpp`).

## Fichiers (à écraser)
```
src/GamebuinoAKA.Core/Services/CodeSnippetService.cs
tests/GamebuinoAKA.Core.Tests/CodeSnippetServiceTests.cs
```

## Notes
- Snippets PlatformIO : API Arduino `gb.*` (comme les 16 d'origine).
- Snippets ESP-IDF : API coquille `gfx` / `g_core` (calquée sur mAKArena).
  Si un symbole diffère dans ta version de lib, l'erreur apparaîtra à la
  **compilation du jeu généré**, pas de l'IDE — signale-le, j'ajuste.
