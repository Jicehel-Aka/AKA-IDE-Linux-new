using System;
using System.IO;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    public class TemplateService : ITemplateService
    {
        private readonly ISettingsService _settings;

        public TemplateService(ISettingsService settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Crée un projet. La chaîne de build décide de toute la structure :
        /// PlatformIO (Arduino + lib jmp42) ou ESP-IDF (composants CMake + coquille).
        /// </summary>
        public async Task CreateProjectAsync(string projectName, string template,
            string destinationFolder, BuildSystem buildSystem, bool withAudio = false)
        {
            var projectDir = Path.Combine(destinationFolder, projectName);
            Directory.CreateDirectory(projectDir);

            if (buildSystem == BuildSystem.EspIdf)
                await CreateEspIdfProjectAsync(projectName, projectDir, withAudio);
            else
                await CreatePlatformIOProjectAsync(projectName, template, projectDir);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PlatformIO (Arduino) — comportement d'origine conservé
        // ═══════════════════════════════════════════════════════════════════════

        private async Task CreatePlatformIOProjectAsync(string projectName, string template, string projectDir)
        {
            Directory.CreateDirectory(Path.Combine(projectDir, "src"));
            Directory.CreateDirectory(Path.Combine(projectDir, ".vscode"));

            await Write(projectDir, "platformio.ini", PlatformIni());
            await Write(Path.Combine(projectDir, "src"), "main.cpp", PioMainCpp(template));

            if (template == "game-template")
            {
                await Write(Path.Combine(projectDir, "src"), "game.h", PioGameH());
                await Write(Path.Combine(projectDir, "src"), "game.cpp", PioGameCpp());
            }

            await Write(Path.Combine(projectDir, ".vscode"), "c_cpp_properties.json", PioCppProps());
            await Write(Path.Combine(projectDir, ".vscode"), "extensions.json", ExtensionsJson(false));
        }

        private string PlatformIni() =>
@"[env:gamebuino_aka]
platform = espressif32
board = esp32-s3-devkitc-1
framework = arduino
monitor_speed = 115200
board_build.flash_size = 16MB
board_build.psram_type = opi
lib_deps =
    " + _settings.Settings.GamebuinoLibRepoUrl + @"
build_flags =
    -DBOARD_HAS_PSRAM
    -mfix-esp32-psram-cache-issue
upload_protocol = esptool";

        private static string PioMainCpp(string template) => template switch
        {
            "hello-world" =>
@"#include <Gamebuino-Meta.h>

Gamebuino gb;

void setup() {
    gb.begin();
}

void loop() {
    gb.waitForUpdate();
    gb.display.clear();
    gb.display.print(""Hello Gamebuino AKA!"");
}",
            "game-template" =>
@"#include <Gamebuino-Meta.h>
#include ""game.h""

Gamebuino gb;

void setup() {
    gb.begin();
}

void loop() {
    gb.waitForUpdate();
    gb.display.clear();
    gameUpdate(gb);
    gameRender(gb);
}",
            _ =>
@"#include <Gamebuino-Meta.h>

Gamebuino gb;

void setup() {
    gb.begin();
}

void loop() {
    gb.waitForUpdate();
    gb.display.clear();
}"
        };

        private static string PioGameH() =>
@"#pragma once
#include <Gamebuino-Meta.h>

void gameUpdate(Gamebuino& gb);
void gameRender(Gamebuino& gb);";

        private static string PioGameCpp() =>
@"#include ""game.h""

void gameUpdate(Gamebuino& gb) {
    // Logique de jeu ici
}

void gameRender(Gamebuino& gb) {
    // Rendu ici
}";

        private static string PioCppProps() =>
@"{
    ""configurations"": [
        {
            ""name"": ""PlatformIO"",
            ""includePath"": [
                ""${workspaceFolder}/**"",
                ""${env:USERPROFILE}/.platformio/packages/framework-arduinoespressif32/cores/esp32"",
                ""${env:USERPROFILE}/.platformio/lib/**""
            ],
            ""defines"": [ ""ARDUINO"", ""BOARD_HAS_PSRAM"", ""ESP32"", ""ESP_PLATFORM"" ],
            ""cStandard"": ""c11"",
            ""cppStandard"": ""c++17"",
            ""intelliSenseMode"": ""gcc-x64""
        }
    ],
    ""version"": 4
}";

        // ═══════════════════════════════════════════════════════════════════════
        //  ESP-IDF (composants CMake + coquille)
        // ═══════════════════════════════════════════════════════════════════════

        private async Task CreateEspIdfProjectAsync(string projectName, string projectDir, bool withAudio)
        {
            var mainDir = Path.Combine(projectDir, "main");
            var vscodeDir = Path.Combine(projectDir, ".vscode");
            var compGb = Path.Combine(projectDir, "components", "gamebuino");
            Directory.CreateDirectory(mainDir);
            Directory.CreateDirectory(vscodeDir);
            Directory.CreateDirectory(Path.Combine(projectDir, "components"));

            await Write(projectDir, "CMakeLists.txt", IdfRootCMake(projectName));
            await Write(projectDir, ".aka-build", "espidf");
            await Write(projectDir, "sdkconfig.defaults", IdfSdkconfigDefaults());
            await Write(projectDir, "partitions.csv", IdfPartitions());
            await Write(projectDir, "README.md", IdfProjectReadme(projectName));

            await Write(mainDir, "CMakeLists.txt", IdfMainCMake(withAudio));
            await Write(mainDir, "app_main.cpp", IdfAppMain(projectName, withAudio));
            await Write(mainDir, "game_module.h", IdfGameModuleH());
            if (withAudio)
            {
                await Write(mainDir, "audio.h", IdfAudioHeader());
                await Write(mainDir, "audio.cpp", IdfAudioSource());
            }

            await Write(vscodeDir, "settings.json", IdfVsCodeSettings());
            await Write(vscodeDir, "extensions.json", ExtensionsJson(true));

            // Composant gamebuino : priorité au dossier de référence configuré,
            // sinon la lib EMBARQUÉE dans l'IDE (Lib/gamebuino), sinon placeholder.
            var refPath = _settings.Settings.ReferenceGamebuinoComponentPath;
            string? source =
                (!string.IsNullOrWhiteSpace(refPath) && Directory.Exists(refPath))
                    ? refPath
                    : GetBundledComponentPath();

            if (source != null)
            {
                await Task.Run(() => CopyDirectory(source, compGb));
            }
            else
            {
                Directory.CreateDirectory(compGb);
                await Write(compGb, "AJOUTER_LA_LIB.md", IdfComponentPlaceholder());
            }
        }

        private static string IdfRootCMake(string projectName) =>
@"cmake_minimum_required(VERSION 3.16)

include($ENV{IDF_PATH}/tools/cmake/project.cmake)

project(" + SanitizeCmake(projectName) + @")
";

        // Aligné sur gb_config.h : PSRAM OPI, 16 Mo, FAT en 8.3 (LFN_NONE).
        private static string IdfSdkconfigDefaults() =>
@"# Cible
CONFIG_IDF_TARGET=""esp32s3""

# Flash 16 Mo
CONFIG_ESPTOOLPY_FLASHSIZE_16MB=y
CONFIG_ESPTOOLPY_FLASHSIZE=""16MB""

# PSRAM octal (opi)
CONFIG_SPIRAM=y
CONFIG_SPIRAM_MODE_OCT=y
CONFIG_SPIRAM_SPEED_80M=y

# Table de partitions personnalisée
CONFIG_PARTITION_TABLE_CUSTOM=y
CONFIG_PARTITION_TABLE_CUSTOM_FILENAME=""partitions.csv""

# FreeRTOS à 1 kHz (tâches SysTask 10 ms, GameTask 30 fps)
CONFIG_FREERTOS_HZ=1000

# Noms 8.3 obligatoires sur la SD (CFG.DAT, SHOTxxxx.BMP)
CONFIG_FATFS_LFN_NONE=y
";

        private static string IdfPartitions() =>
@"# Name,   Type, SubType, Offset,   Size
nvs,      data, nvs,      ,         0x6000
phy_init, data, phy,      ,         0x1000
factory,  app,  factory,  ,         0x300000
storage,  data, fat,      ,         0x200000
";

        // main/CMakeLists.txt — RAPPEL convention : les composants dans REQUIRES,
        // jamais dans INCLUDE_DIRS.
        private static string IdfMainCMake(bool withAudio) =>
@"idf_component_register(
    SRCS
        app_main.cpp" + (withAudio ? @"
        audio.cpp" : "") + @"

    INCLUDE_DIRS
        .

    # RAPPEL : les composants vont dans REQUIRES, PAS dans INCLUDE_DIRS.
    REQUIRES
        gamebuino nvs_flash app_update esp_partition esp_system fatfs
)
";

        private static string IdfAppMain(string projectName, bool withAudio) =>
@"/*
===============================================================================
  app_main.cpp — " + projectName + @" (Gamebuino AKA, ESP-IDF)
-------------------------------------------------------------------------------
  Squelette MINIMAL mono-tâche : init du core + boucle d'affichage.
===============================================================================
*/
#include ""freertos/FreeRTOS.h""
#include ""freertos/task.h""
#include ""gamebuino.h""
" + (withAudio ? "#include \"\"audio.h\"\"   // support audio (activé à la création)\n" : "") + @"
gb_core     g_core;
gb_graphics gfx;

extern ""C"" void app_main(void)
{
    g_core.init();
" + (withAudio ? "    audio_init();   // démarre la tâche audio (mixeur)\n" : "") + @"
    const uint16_t bg = gfx.makeColor(20, 16, 40);
    const uint16_t fg = gfx.makeColor(255, 255, 255);

    while (true) {
        g_core.pool();
" + (withAudio ? "        if (g_core.buttons.pressed(gb_buttons::KEY_A)) audio_beep(880, 80);\n" : "") + @"
        gfx.clear(bg);
        gfx.setColor(fg);
        gfx.move_cursor(16, 16);
        gfx.print_str(""" + projectName + @""");
        gfx.move_cursor(16, 40);
        gfx.print_str(""Hello Gamebuino AKA!"");
" + (withAudio ? "        gfx.move_cursor(16, 64);\n        gfx.print_str(\"\"Appuyez sur A pour un son\"\");\n" : "") + @"
        gfx.update();
        vTaskDelay(pdMS_TO_TICKS(16));
    }
}
";

        // Module audio minimal — mixeur SDK (4 pistes) + tâche FreeRTOS dédiée.
        // Fidèle à main/core/audio.cpp de mAKArena (play_tone, add_track, pool).
        private static string IdfAudioHeader() =>
@"#pragma once
#include <cstdint>

// Support audio minimal. audio_init() est appelé une fois dans app_main.
void audio_init();
void audio_set_master_volume(uint8_t v);            // 0..255

// Effet : glissando f0->f1 (Hz), volumes v0->v1 (0..1), durée ms.
void audio_sfx(float f0, float f1, float v0, float v1, uint16_t ms);
void audio_beep(float freq, uint16_t ms);           // bip simple
void audio_music_note(float freq, uint16_t ms);     // note (piste musique)
";

        private static string IdfAudioSource() =>
@"/*
  audio.cpp — plomberie audio minimale (Gamebuino AKA, ESP-IDF).
  Le mixeur (gb_audio_player) doit être pompé en continu : une tâche FreeRTOS
  dédiée appelle g_player.pool(). Deux pistes : musique + effets.
*/
#include ""audio.h""
#include ""gb_audio_player.h""
#include ""gb_audio_track_tone.h""
#include ""gb_ll_audio.h""
#include ""freertos/FreeRTOS.h""
#include ""freertos/task.h""

static gb_audio_player     g_player;
static gb_audio_track_tone g_music;
static gb_audio_track_tone g_sfx;

static void audio_task(void*)
{
    while (true) {
        g_player.pool();
        vTaskDelay(pdMS_TO_TICKS(2));
    }
}

void audio_init()
{
    gb_ll_audio_set_volume(200);
    g_player.add_track(&g_music);
    g_player.add_track(&g_sfx);
    g_music.set_track_volume(1.0f);
    g_sfx.set_track_volume(1.0f);
    // Le mixeur tourne dans sa propre tâche (CPU 0), en continu.
    xTaskCreatePinnedToCore(audio_task, ""AudioTask"", 4096, nullptr, 5, nullptr, 0);
}

void audio_set_master_volume(uint8_t v) { gb_ll_audio_set_volume(v); }

void audio_sfx(float f0, float f1, float v0, float v1, uint16_t ms)
{
    g_sfx.play_tone(f0, f1, v0, v1, ms, gb_audio_track_tone::SQUARE);
}

void audio_beep(float freq, uint16_t ms)
{
    g_sfx.play_tone(freq, freq, 0.60f, 0.10f, ms, gb_audio_track_tone::SQUARE);
}

void audio_music_note(float freq, uint16_t ms)
{
    g_music.play_tone(freq, freq, 0.45f, 0.05f, ms, gb_audio_track_tone::TRIANGLE);
}
";

        // Interface coquille — fidèle à main/shell/game_module.h de mAKArena.
        private static string IdfGameModuleH() =>
@"/*
  game_module.h — Interface commune des jeux (coquille AKA).
  Un jeu = ces quelques fonctions ; la coquille gère menu, i18n, options,
  son, sauvegarde et retour-loader. Ajouter un jeu = écrire ces fonctions.

  NB : dans une vraie coquille, ce fichier vit dans main/shell/ et l'enum
  i18n::GameId + les couleurs viennent de la coquille copiée depuis un projet
  de référence. Gardé ici comme rappel de la structure cible.
*/
#pragma once
#include <cstdint>

namespace shell {

enum Outcome : int8_t { OUT_ONGOING = 0, OUT_WIN = 1, OUT_LOSS = -1, OUT_DRAW = 2 };

struct GameModule {
    void        (*start)(int level, bool human_first);
    void        (*update)(uint32_t now_ms);
    void        (*draw)(uint32_t now_ms);
    Outcome     (*outcome)();
    void        (*undo)();
    void        (*redo)();
    const char* (*status)();
};

} // namespace shell
";

        private static string IdfVsCodeSettings() =>
@"{
    ""idf.adapterTargetName"": ""esp32s3"",
    ""idf.flashType"": ""UART"",
    ""C_Cpp.intelliSenseEngine"": ""default"",
    ""files.associations"": {
        ""*.h"": ""cpp"",
        ""*.hpp"": ""cpp""
    }
}";

        private static string IdfProjectReadme(string projectName) =>
@"# " + projectName + @" — Gamebuino AKA (ESP-IDF)

Projet ESP-IDF à base de composants CMake, structure « coquille ».

## Structure
```
" + projectName + @"/
├── CMakeLists.txt            ← projet ESP-IDF (inclut project.cmake)
├── sdkconfig.defaults        ← PSRAM opi, flash 16 Mo, FAT 8.3, FreeRTOS 1 kHz
├── partitions.csv            ← nvs / phy_init / factory / storage(fat)
├── components/
│   └── gamebuino/            ← LA LIB (composant). Voir ci-dessous.
└── main/
    ├── CMakeLists.txt        ← REQUIRES gamebuino nvs_flash app_update ...
    ├── app_main.cpp          ← init core + boucle d'affichage
    └── game_module.h         ← interface coquille (rappel)
```

## Le composant `components/gamebuino`
- Si tu avais renseigné un **dossier de référence** dans les Paramètres de
  l'IDE, la lib a été copiée automatiquement ici.
- Sinon, copie le dossier `components/gamebuino` depuis un de tes projets
  existants (mAKArena, pAKAman…) dans `components/`.

## Build / Flash / Monitor
Depuis l'IDE (boutons Build/Flash/Monitor) ou en ligne de commande :
```
idf.py build
idf.py -p COMx flash monitor
```
";

        private static string IdfComponentPlaceholder() =>
@"# Ajouter la bibliothèque Gamebuino AKA ici

Ce dossier doit contenir le **composant** `gamebuino` :

```
components/gamebuino/
├── CMakeLists.txt
├── gb_lib/        (gb_core.cpp, gb_graphics.cpp, gb_audio_*.cpp, pmf_player/…)
├── gb_ll/         (gb_ll_lcd.c, gb_ll_audio.c, gb_ll_sdcard.c…)
├── include_lib/   (gamebuino.h, gb_core.h, gb_graphics.h, gb_config.h…)
└── include_ll/    (gb_ll_lcd.h…)
```

Copie-le depuis un de tes projets existants (ex. `mAKArena/components/gamebuino`),
ou renseigne le champ « Composant gamebuino de référence » dans les Paramètres
de l'IDE pour que la copie soit automatique aux prochains projets.
";

        // ═══════════════════════════════════════════════════════════════════════
        //  Commun
        // ═══════════════════════════════════════════════════════════════════════

        private static string ExtensionsJson(bool espIdf) => espIdf
            ? @"{ ""recommendations"": [ ""espressif.esp-idf-extension"", ""ms-vscode.cpptools"" ] }"
            : @"{ ""recommendations"": [ ""platformio.platformio-ide"", ""ms-vscode.cpptools"" ] }";

        private static async Task Write(string dir, string file, string content)
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, file), content);
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
            foreach (var sub in Directory.GetDirectories(source))
            {
                // Évite de recopier des artefacts de build volumineux.
                var name = Path.GetFileName(sub);
                if (name is "build" or ".git" or "managed_components") continue;
                CopyDirectory(sub, Path.Combine(dest, name));
            }
        }

        /// <summary>
        /// Chemin de la lib embarquée avec l'IDE (Lib/gamebuino, copiée dans le
        /// dossier de sortie). Renvoie null si absente.
        /// </summary>
        public static string? GetBundledComponentPath()
        {
            var p = Path.Combine(AppContext.BaseDirectory, "Lib", "gamebuino");
            return (Directory.Exists(p) && File.Exists(Path.Combine(p, "CMakeLists.txt")))
                ? p : null;
        }

        private static string SanitizeCmake(string name)
        {
            var s = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
            return string.IsNullOrEmpty(s) ? "aka_game" : s;
        }
    }
}
