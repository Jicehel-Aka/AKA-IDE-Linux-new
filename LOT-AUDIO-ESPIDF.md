# Option « support audio » à la création (ESP-IDF)

Écrase 5 fichiers. Ajoute une case à cocher (projets ESP-IDF uniquement) qui
génère la **plomberie audio minimale** pour que le son fonctionne.

## Ce qui est généré quand l'option est cochée
```
main/audio.h     # API : audio_init, audio_beep, audio_sfx, audio_music_note, master volume
main/audio.cpp   # mixeur gb_audio_player + 2 pistes gb_audio_track_tone
                 #   + TÂCHE FreeRTOS dédiée qui appelle g_player.pool() en continu
main/CMakeLists.txt   # audio.cpp ajouté aux SRCS
main/app_main.cpp     # #include "audio.h" + audio_init() + démo (A = bip)
```
Fidèle au câblage réel de mAKArena (`main/core/audio.cpp`) : `add_track`,
`set_track_volume`, `play_tone(f0,f1,v0,v1,ms,type)`, `gb_ll_audio_set_volume`,
tâche épinglée CPU 0. Sans cette tâche, le mixeur ne serait jamais pompé et
aucun son ne sortirait.

## Utilisation dans le jeu généré
```cpp
audio_beep(880, 80);                       // bip
audio_sfx(520, 780, 0.5f, 0.3f, 60);       // glissando
audio_music_note(440, 200);                // note (piste musique)
```

## Fichiers (à écraser)
```
src/GamebuinoAKA.Core/Services/TemplateService.cs     # génération audio conditionnelle
src/GamebuinoAKA.Core/Services/ITemplateService.cs    # + bool withAudio = false
src/GamebuinoAKA.App/ViewModels/NewProjectViewModel.cs # + AddAudio
src/GamebuinoAKA.App/Views/NewProjectView.axaml        # case « Ajouter le support audio »
tests/GamebuinoAKA.Core.Tests/TemplateServiceTests.cs  # + 2 tests audio (avec / sans)
```

## Vérif
`dotnet test` : + 2 tests (audio.h/.cpp générés & câblés ; absents si non coché).
La case n'apparaît que pour ESP-IDF (le son PlatformIO passe déjà par gb.sound.*).

## Note
Le module utilise l'API réelle de ta lib. Si un symbole diffère
(`gb_audio_track_tone::SQUARE`, `play_tone`, `gb_ll_audio_set_volume`…),
l'erreur apparaîtra à la **compilation du jeu généré** — signale-le, j'ajuste.
