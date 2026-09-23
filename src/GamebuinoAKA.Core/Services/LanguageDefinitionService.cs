using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GamebuinoAKA.Core.Models;
using Newtonsoft.Json;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Catalogue des langages connus de l'éditeur de code, chargé depuis un fichier <c>languages.json</c>
    /// MODIFIABLE (à côté des paramètres de l'application — voir <see cref="ConfigFilePath"/>), avec des
    /// valeurs par défaut intégrées si le fichier est absent ou invalide. Ajouter un langage (BASIC, un
    /// jour) ou changer le dossier d'un langage sur la carte SD : éditer ce fichier, aucune recompilation
    /// d'AKA-IDE ni d'un firmware nécessaire — voir aussi transfer.cpp (dépôt AKA-Love), qui relit sa
    /// propre copie exportée (<see cref="ExportLanguagesCsv"/>) à chaque démarrage de la console.
    ///
    /// Les mots-clés et l'API restent un sous-ensemble représentatif (assez pour une autocomplétion et
    /// une coloration utiles), pas une couverture exhaustive — les compléter au fil de l'eau ne casse
    /// jamais rien côté éditeur. Sources : dépôt AKA-Love pour Lua ; dépôt MicroPython-AKA
    /// (<c>components/micropython/modaka.c</c>, <c>sdcard_files/py/upygame.py</c>) pour Python.
    /// </summary>
    public class LanguageDefinitionService : ILanguageDefinitionService
    {
        // ── Valeurs par défaut (utilisées si languages.json est absent, invalide, ou vide) ──────────

        private static readonly string[] LuaKeywords =
        {
            "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "if", "in",
            "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while",
        };

        private static readonly string[] LuaApi =
        {
            "love.load", "love.update", "love.draw", "love.keypressed", "love.keyreleased",
            "love.gamepadpressed", "love.gamepadreleased", "love.mousepressed", "love.mousereleased",
            "love.conf",
            "love.graphics.setColor", "love.graphics.getColor", "love.graphics.setBackgroundColor",
            "love.graphics.clear", "love.graphics.present", "love.graphics.reset",
            "love.graphics.rectangle", "love.graphics.circle", "love.graphics.polygon",
            "love.graphics.line", "love.graphics.points", "love.graphics.print", "love.graphics.printf",
            "love.graphics.push", "love.graphics.pop", "love.graphics.translate", "love.graphics.rotate",
            "love.graphics.scale", "love.graphics.setScissor", "love.graphics.newImage",
            "love.graphics.newQuad", "love.graphics.draw", "love.graphics.newFont",
            "love.graphics.setFont", "love.graphics.getFont", "love.graphics.getWidth",
            "love.graphics.getHeight",
            "love.keyboard.isDown", "love.joystick.getJoysticks", "love.mouse.getPosition",
            "love.mouse.isDown", "love.mouse.setPosition",
            "love.timer.getDelta", "love.timer.getFPS", "love.timer.sleep", "love.math.random",
            "love.math.setRandomSeed", "love.event.quit", "love.window.setTitle", "love.system.getOS",
            "love.filesystem.read", "love.filesystem.write", "love.filesystem.append",
            "love.filesystem.newFile", "love.filesystem.getInfo", "love.filesystem.lines",
            "love.audio.newSource",
        };

        private static readonly string[] PyKeywords =
        {
            "and", "as", "assert", "break", "class", "continue", "def", "del", "elif", "else", "except",
            "False", "finally", "for", "from", "global", "if", "import", "in", "is", "lambda", "None",
            "nonlocal", "not", "or", "pass", "raise", "return", "True", "try", "while", "with", "yield",
        };

        private static readonly string[] PyApi =
        {
            "aka.display", "aka.clear", "aka.fill", "aka.pixel", "aka.line", "aka.hline", "aka.vline",
            "aka.rect", "aka.fill_rect", "aka.circle", "aka.fill_circle", "aka.triangle",
            "aka.fill_triangle", "aka.text", "aka.color", "aka.width", "aka.height", "aka.update",
            "aka.buttons", "aka.pressed", "aka.released", "aka.joystick",
            "aka.A", "aka.B", "aka.C", "aka.D", "aka.UP", "aka.DOWN", "aka.LEFT", "aka.RIGHT",
            "aka.L1", "aka.R1", "aka.MENU", "aka.RUN",
            "aka.play_pcm8", "aka.is_sound_playing", "aka.vibrate", "aka.is_vibrating",
            "aka.sleep_ms", "aka.ticks_ms", "aka.language", "aka.tr", "aka.screenshot",
            "aka.file_read", "aka.file_write", "aka.list_py", "aka.run_file",
            "upygame.Rect", "upygame.Surface", "upygame.Sound",
        };

        private static List<LanguageDefinition> BuildDefaults() => new()
        {
            new LanguageDefinition
            {
                Language = CodeLanguage.Lua,
                DisplayName = "Lua (AKA-Love)",
                FileExtension = "lua",
                HighlightingName = "Lua",
                TransferLangId = 0,
                SdScriptsRoot = "AKA_Love/games",
                Keywords = LuaKeywords,
                ApiSymbols = LuaApi,
                NewFileTemplate =
                    "function love.load()\nend\n\nfunction love.update(dt)\nend\n\nfunction love.draw()\nend\n",
            },
            new LanguageDefinition
            {
                Language = CodeLanguage.MicroPython,
                DisplayName = "MicroPython (AKA)",
                FileExtension = "py",
                HighlightingName = "Python",
                TransferLangId = 1,
                SdScriptsRoot = "py",   // a plat, a la racine de la carte SD (pas de sous-dossier par jeu)
                Keywords = PyKeywords,
                ApiSymbols = PyApi,
                NewFileTemplate = "import aka\n\nwhile True:\n    aka.clear(0)\n    aka.update()\n",
            },
        };

        // ── Chargement / sauvegarde ───────────────────────────────────────────────────────────────

        private readonly List<LanguageDefinition> _all;

        public string ConfigFilePath { get; }

        public LanguageDefinitionService(ISettingsService settings)
        {
            var settingsDir = Path.GetDirectoryName(settings.SettingsFilePath) ?? ".";
            ConfigFilePath = Path.Combine(settingsDir, "languages.json");
            _all = Load();
        }

        private List<LanguageDefinition> Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var loaded = JsonConvert.DeserializeObject<List<LanguageDefinition>>(json);
                    if (loaded is { Count: > 0 }) return loaded;
                }
            }
            catch (Exception)
            {
                // fichier illisible ou JSON invalide : on retombe sur les valeurs par défaut ci-dessous,
                // SANS écraser le fichier existant (l'utilisateur pourra le corriger à la main).
            }

            var defaults = BuildDefaults();
            TrySaveDefaults(defaults);
            return defaults;
        }

        private void TrySaveDefaults(List<LanguageDefinition> defaults)
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (!File.Exists(ConfigFilePath))
                    File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(defaults, Formatting.Indented));
            }
            catch (Exception)
            {
                // premier lancement en lecture seule, par exemple : tant pis, les valeurs par défaut
                // restent utilisables en mémoire pour cette session.
            }
        }

        public IReadOnlyList<LanguageDefinition> All => _all;

        public LanguageDefinition Get(CodeLanguage language) => _all.First(d => d.Language == language);

        public LanguageDefinition? FromFilePath(string path)
        {
            var ext = Path.GetExtension(path).TrimStart('.');
            return _all.FirstOrDefault(d => string.Equals(d.FileExtension, ext, StringComparison.OrdinalIgnoreCase));
        }

        public void ExportLanguagesCsv(string sdRootFolder)
        {
            var dir = Path.Combine(sdRootFolder, "AKA");
            Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("# Généré par AKA-IDE (LanguageDefinitionService.ExportLanguagesCsv) à partir de");
            sb.AppendLine("# " + ConfigFilePath);
            sb.AppendLine("# Modifiable directement ici ou dans AKA-IDE — voir docs/PROTOCOLE_TRANSFERT.md");
            sb.AppendLine("# (dépôt AKA-Love) pour le format : lang_id,dossier_scripts_sur_la_carte_sd,nom_affiche");
            foreach (var def in _all)
            {
                if (string.IsNullOrWhiteSpace(def.SdScriptsRoot)) continue;   // rien à router : on saute la ligne
                sb.AppendLine($"{def.TransferLangId},{def.SdScriptsRoot},{def.DisplayName}");
            }

            File.WriteAllText(Path.Combine(dir, "languages.csv"), sb.ToString());
        }
    }
}
