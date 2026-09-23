using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GamebuinoAKA.Core.Models;
using Newtonsoft.Json;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Fournit le catalogue complet de snippets (intégrés + utilisateur)
    /// et génère le code source final en combinant le squelette immuable
    /// avec les snippets sélectionnés.
    /// </summary>
    public class CodeSnippetService : ICodeSnippetService
    {
        private const string BankFileName = ".aka-snippets.json";
        private readonly ISettingsService _settings;

        public CodeSnippetService(ISettingsService settings)
        {
            _settings = settings;
        }

        // ── Banque utilisateur ────────────────────────────────────────────────────

        private string BankFilePath =>
            Path.Combine(_settings.Settings.WorkspaceFolder ?? string.Empty, BankFileName);

        public SnippetBank LoadUserBank()
        {
            try
            {
                if (File.Exists(BankFilePath))
                    return JsonConvert.DeserializeObject<SnippetBank>(
                               File.ReadAllText(BankFilePath)) ?? new SnippetBank();
            }
            catch { }
            return new SnippetBank();
        }

        public void SaveUserBank(SnippetBank bank)
        {
            try { File.WriteAllText(BankFilePath, JsonConvert.SerializeObject(bank, Formatting.Indented)); }
            catch { }
        }

        // ── Catalogue complet ─────────────────────────────────────────────────────

        /// <summary>
        /// Renvoie tous les snippets disponibles : intégrés + ceux de l'utilisateur.
        /// Filtre optionnel par système de build.
        /// </summary>
        public List<CodeSnippet> GetAll(BuildSystem? forBuild = null)
        {
            var all = new List<CodeSnippet>(BuiltinSnippets());
            all.AddRange(LoadUserBank().UserSnippets);

            if (forBuild == BuildSystem.PlatformIO)
                return all.Where(s => s.ForPlatformIO).ToList();
            if (forBuild == BuildSystem.EspIdf)
                return all.Where(s => s.ForEspIdf).ToList();
            return all;
        }

        // ── Génération de code ────────────────────────────────────────────────────

        /// <summary>
        /// Génère un dictionnaire filename → contenu à partir des snippets sélectionnés.
        /// Les fichiers retournés viennent COMPLÉTER (ou remplacer pour game.cpp/game.h)
        /// le squelette créé par TemplateService.
        /// </summary>
        public Dictionary<string, string> GenerateFiles(
            IEnumerable<CodeSnippet> selected,
            BuildSystem buildSystem,
            string projectName)
        {
            var snippets = ResolveDependencies(selected.ToList(), buildSystem);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (buildSystem == BuildSystem.PlatformIO)
                GeneratePlatformIO(snippets, projectName, result);
            else
                GenerateEspIdf(snippets, projectName, result);

            return result;
        }

        // ── PlatformIO ────────────────────────────────────────────────────────────

        private static void GeneratePlatformIO(
            List<CodeSnippet> snippets, string projectName,
            Dictionary<string, string> result)
        {
            // Regroupe par fichier cible
            var forGameCpp   = snippets.Where(s => s.TargetFile == SnippetTargetFile.GameCpp).ToList();
            var forGameH     = snippets.Where(s => s.TargetFile == SnippetTargetFile.GameH).ToList();
            var forMain      = snippets.Where(s => s.TargetFile == SnippetTargetFile.MainCpp).ToList();
            var forNewH      = snippets.Where(s => s.TargetFile == SnippetTargetFile.NewHeader).ToList();
            var forNewCpp    = snippets.Where(s => s.TargetFile == SnippetTargetFile.NewCpp).ToList();

            if (forGameCpp.Count > 0 || forGameH.Count > 0)
            {
                result["src/game.h"]   = BuildGameH(forGameH, projectName);
                result["src/game.cpp"] = BuildGameCpp(forGameCpp, projectName);
                // main.cpp qui inclut game.h
                result["src/main.cpp"] = BuildMainCppWithGame(forMain, projectName);
            }
            else if (forMain.Count > 0)
            {
                result["src/main.cpp"] = BuildMainCppInline(forMain, projectName);
            }

            foreach (var s in forNewH)
                result[$"src/{Sanitize(s.Name)}.h"] = s.Code;
            foreach (var s in forNewCpp)
                result[$"src/{Sanitize(s.Name)}.cpp"] = s.Code;
        }

        // ── ESP-IDF ───────────────────────────────────────────────────────────────

        private static void GenerateEspIdf(
            List<CodeSnippet> snippets, string projectName,
            Dictionary<string, string> result)
        {
            var forMain  = snippets.Where(s => s.TargetFile == SnippetTargetFile.MainCpp).ToList();
            var forNewH  = snippets.Where(s => s.TargetFile == SnippetTargetFile.NewHeader).ToList();
            var forNewCpp = snippets.Where(s => s.TargetFile == SnippetTargetFile.NewCpp).ToList();

            if (forMain.Count > 0)
                result["main/app_main.cpp"] = BuildAppMain(forMain, projectName);

            foreach (var s in forNewH)
                result[$"main/{Sanitize(s.Name)}.h"] = s.Code;
            foreach (var s in forNewCpp)
                result[$"main/{Sanitize(s.Name)}.cpp"] = s.Code;
        }

        // ── Constructeurs de fichiers ─────────────────────────────────────────────

        private static string BuildGameH(List<CodeSnippet> snippets, string project)
        {
            var decls = Collect(snippets, "//@@DECLARATIONS@@");
            return
$@"#pragma once
// {project} — game.h
// Généré par Gamebuino AKA IDE
#include <Gamebuino-Meta.h>

// ── Fonctions principales ──────────────────────────────────────────────────
void gameUpdate(Gamebuino& gb);
void gameRender(Gamebuino& gb);

{(decls.Length > 0 ? "// ── Déclarations des modules sélectionnés ──────────────────────────────\n" + decls : "")}
";
        }

        private static string BuildGameCpp(List<CodeSnippet> snippets, string project)
        {
            var includes  = Collect(snippets, "//@@INCLUDES@@");
            var globals   = Collect(snippets, "//@@GLOBALS@@");
            var update    = Collect(snippets, "//@@UPDATE@@");
            var render    = Collect(snippets, "//@@RENDER@@");
            var functions = Collect(snippets, "//@@FUNCTIONS@@");

            return
$@"#include ""game.h""
{(includes.Length > 0 ? includes + "\n" : "")}
// ── Variables d'état ───────────────────────────────────────────────────────
static int playerX = 160;
static int playerY = 120;
static const int PLAYER_SPEED = 2;
{(globals.Length > 0 ? "\n" + globals : "")}
// ── gameUpdate ──────────────────────────────────────────────────────────────
void gameUpdate(Gamebuino& gb) {{
    // Déplacement du joueur
    if (gb.buttons.repeat(BUTTON_LEFT,  1)) playerX -= PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_RIGHT, 1)) playerX += PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_UP,    1)) playerY -= PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_DOWN,  1)) playerY += PLAYER_SPEED;
    playerX = max(0, min(315, playerX));
    playerY = max(0, min(235, playerY));
{(update.Length > 0 ? "\n" + Indent(update, 4) : "")}
}}

// ── gameRender ──────────────────────────────────────────────────────────────
void gameRender(Gamebuino& gb) {{
    gb.display.setColor(BLACK);
    gb.display.fill();
    gb.display.setColor(0x7C5C);
    gb.display.fillRect(playerX, playerY, 5, 5);
    gb.display.setColor(WHITE);
    gb.display.setCursor(4, 4);
    gb.display.print(""{project}"");
{(render.Length > 0 ? "\n" + Indent(render, 4) : "")}
}}
{(functions.Length > 0 ? "\n// ── Fonctions helper ─────────────────────────────────────────────────\n" + functions : "")}
";
        }

        private static string BuildMainCppWithGame(List<CodeSnippet> snippets, string project)
        {
            var extra = Collect(snippets, "//@@UPDATE@@") + Collect(snippets, "//@@RENDER@@");
            _ = extra; // les injections vont dans game.cpp
            return
$@"#include <Gamebuino-Meta.h>
#include ""game.h""
// {project} — généré par Gamebuino AKA IDE

Gamebuino gb;

void setup() {{
    gb.begin();
}}

void loop() {{
    gb.waitForUpdate();
    gb.display.clear();
    gameUpdate(gb);
    gameRender(gb);
}}
";
        }

        private static string BuildMainCppInline(List<CodeSnippet> snippets, string project)
        {
            var includes  = Collect(snippets, "//@@INCLUDES@@");
            var globals   = Collect(snippets, "//@@GLOBALS@@");
            var update    = Collect(snippets, "//@@UPDATE@@");
            var render    = Collect(snippets, "//@@RENDER@@");
            var functions = Collect(snippets, "//@@FUNCTIONS@@");

            return
$@"#include <Gamebuino-Meta.h>
{(includes.Length > 0 ? includes + "\n" : "")}
// {project} — généré par Gamebuino AKA IDE
Gamebuino gb;
{(globals.Length > 0 ? "\n" + globals : "")}
void setup() {{
    gb.begin();
}}

void loop() {{
    gb.waitForUpdate();
    gb.display.clear();
{(update.Length > 0 ? Indent(update, 4) + "\n" : "")}
{(render.Length > 0 ? Indent(render, 4) + "\n" : "")}
}}
{(functions.Length > 0 ? "\n" + functions : "")}
";
        }

        private static string BuildAppMain(List<CodeSnippet> snippets, string project)
        {
            var includes  = Collect(snippets, "//@@INCLUDES@@");
            var globals   = Collect(snippets, "//@@GLOBALS@@");
            var setup     = Collect(snippets, "//@@SETUP@@");
            var update    = Collect(snippets, "//@@UPDATE@@");
            var render    = Collect(snippets, "//@@RENDER@@");
            var functions = Collect(snippets, "//@@FUNCTIONS@@");

            return
$@"/*
 * app_main.cpp — {project} (Gamebuino AKA, ESP-IDF)
 * Généré par Gamebuino AKA IDE
 */
#include ""freertos/FreeRTOS.h""
#include ""freertos/task.h""
#include ""gamebuino.h""
{(includes.Length > 0 ? includes + "\n" : "")}
gb_core     g_core;
gb_graphics gfx;

// État de base (déplacement du joueur)
static int playerX = 160;
static int playerY = 120;
static const int PLAYER_SPEED = 2;
{(globals.Length > 0 ? "\n" + globals : "")}
{(functions.Length > 0 ? functions + "\n" : "")}
extern ""C"" void app_main(void)
{{
    g_core.init();
{(setup.Length > 0 ? "\n" + Indent(setup, 4) + "\n" : "")}
    while (true) {{
        g_core.pool();   // met à jour boutons + joystick

        gfx.clear(gfx.makeColor(20, 16, 40));
{(update.Length > 0 ? Indent(update, 8) + "\n" : "")}
{(render.Length > 0 ? Indent(render, 8) + "\n" : "")}
        gfx.update();
        vTaskDelay(pdMS_TO_TICKS(16));
    }}
}}
";
        }

        // ── Utilitaires ───────────────────────────────────────────────────────────

        /// <summary>
        /// Extrait la section d'un snippet délimitée par un marqueur @@.
        /// Si le code ne contient PAS le marqueur, retourne tout le code (compatibilité).
        /// </summary>
        private static string Collect(IEnumerable<CodeSnippet> snippets, string marker)
        {
            var lines = new System.Text.StringBuilder();
            foreach (var s in snippets)
            {
                var section = ExtractSection(s.Code, marker);
                if (section.Length > 0)
                {
                    lines.AppendLine($"    // --- {s.Name} ---");
                    lines.AppendLine(section);
                }
            }
            return lines.ToString().TrimEnd();
        }

        /// <summary>
        /// Extrait la zone entre //@@MARKER@@ et le prochain //@@...@@ ou fin de code.
        /// </summary>
        private static string ExtractSection(string code, string marker)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            var start = code.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return string.Empty;

            start = code.IndexOf('\n', start);
            if (start < 0) return string.Empty;
            start++;

            // Cherche le prochain marqueur @@
            var nextMarker = code.IndexOf("//@@", start, StringComparison.Ordinal);
            var section = nextMarker < 0
                ? code[start..]
                : code[start..nextMarker];

            return section.TrimEnd();
        }

        private static string Indent(string code, int spaces)
        {
            var pad = new string(' ', spaces);
            return string.Join('\n',
                code.Split('\n').Select(l => string.IsNullOrWhiteSpace(l) ? l : pad + l));
        }

        private static string Sanitize(string name) =>
            System.Text.RegularExpressions.Regex.Replace(
                string.IsNullOrEmpty(name) ? "module" : name, @"[^a-zA-Z0-9_]", "_");

        /// <summary>
        /// Résout les dépendances entre snippets et déduplique.
        /// </summary>
        private List<CodeSnippet> ResolveDependencies(List<CodeSnippet> selected, BuildSystem build)
        {
            var all   = GetAll(build).ToDictionary(s => s.Id);
            var ids   = new HashSet<string>(selected.Select(s => s.Id));
            var queue = new Queue<CodeSnippet>(selected);

            while (queue.Count > 0)
            {
                var s = queue.Dequeue();
                foreach (var dep in s.RequiresSnippetIds)
                {
                    if (!ids.Contains(dep) && all.TryGetValue(dep, out var depSnippet))
                    {
                        ids.Add(dep);
                        queue.Enqueue(depSnippet);
                        selected.Add(depSnippet);
                    }
                }
            }
            return selected;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  CATALOGUE INTÉGRÉ
        //  Les marqueurs //@@ZONE@@ délimitent les sections injectées dans le squelette.
        // ════════════════════════════════════════════════════════════════════════

        private static List<CodeSnippet> BuiltinSnippets() => new()
        {
            // ── ENTRÉES ──────────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "input_buttons",
                Name = "Lecture des boutons",
                Summary = "Lit A, B, directions et Menu à chaque frame.",
                Explanation =
@"gb.buttons fournit trois méthodes essentielles :
• pressed(btn)  → vrai UNE seule frame au moment de l'appui
• repeat(btn,n) → vrai à l'appui puis toutes les n frames (auto-repeat)
• released(btn) → vrai UNE seule frame au relâchement

Boutons disponibles : BUTTON_A, BUTTON_B, BUTTON_LEFT, BUTTON_RIGHT,
BUTTON_UP, BUTTON_DOWN, BUTTON_MENU.

Exemple typique : utiliser pressed() pour tirer et repeat() pour se déplacer.",
                Category = "Entrées",
                Tags = new() { "input", "boutons", "contrôles" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@UPDATE@@
    // --- Lecture des boutons ---
    if (gb.buttons.pressed(BUTTON_A)) {
        // Action au premier appui sur A
    }
    if (gb.buttons.pressed(BUTTON_B)) {
        // Action au premier appui sur B
    }
    if (gb.buttons.repeat(BUTTON_LEFT,  1)) playerX -= PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_RIGHT, 1)) playerX += PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_UP,    1)) playerY -= PLAYER_SPEED;
    if (gb.buttons.repeat(BUTTON_DOWN,  1)) playerY += PLAYER_SPEED;
"
            },

            // ── GRAPHISMES ────────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "gfx_fill_background",
                Name = "Fond coloré",
                Summary = "Efface l'écran avec une couleur de fond.",
                Explanation =
@"gb.display.fill() remplit tout l'écran avec la couleur courante
(setColor / setFillColor).

Couleurs prédéfinies : BLACK, WHITE, RED, GREEN, BLUE, YELLOW, ORANGE,
PINK, PURPLE, BROWN, GRAY, DARKGRAY, LIGHTGRAY, CYAN.

Vous pouvez aussi utiliser une valeur RGB565 16 bits directement :
gb.display.setColor(0x7C5C); // violet Gamebuino AKA",
                Category = "Graphismes",
                Tags = new() { "fond", "couleur", "effacer" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@RENDER@@
    // Fond coloré
    gb.display.setColor(BLACK);
    gb.display.fill();
"
            },

            new CodeSnippet
            {
                Id = "gfx_draw_rect",
                Name = "Rectangle / carré",
                Summary = "Dessine un rectangle plein ou en contour.",
                Explanation =
@"gb.display.fillRect(x, y, w, h) → rectangle plein
gb.display.drawRect(x, y, w, h) → contour seulement

L'écran Gamebuino AKA fait 320×240 pixels.
Attention : les coordonnées dépassant les bords sont simplement clampées
(pas de crash), mais pensez à vérifier les collisions avec les bords.",
                Category = "Graphismes",
                Tags = new() { "rectangle", "forme", "dessin" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@RENDER@@
    // Rectangle plein
    gb.display.setColor(BLUE);
    gb.display.fillRect(playerX, playerY, 16, 16);

    // Contour seulement
    gb.display.setColor(WHITE);
    gb.display.drawRect(playerX - 1, playerY - 1, 18, 18);
"
            },

            new CodeSnippet
            {
                Id = "gfx_draw_circle",
                Name = "Cercle",
                Summary = "Dessine un cercle plein ou en contour.",
                Explanation =
@"gb.display.fillCircle(x, y, r) → cercle plein
gb.display.drawCircle(x, y, r) → contour

x, y = centre du cercle ; r = rayon en pixels.

Utile pour des balles, explosions, ou n'importe quel objet rond.",
                Category = "Graphismes",
                Tags = new() { "cercle", "forme", "dessin" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@RENDER@@
    // Cercle plein (balle, projectile…)
    gb.display.setColor(YELLOW);
    gb.display.fillCircle(playerX + 8, playerY + 8, 6);
"
            },

            new CodeSnippet
            {
                Id = "gfx_draw_text",
                Name = "Affichage de texte",
                Summary = "Place du texte à l'écran avec setCursor + print.",
                Explanation =
@"Workflow classique :
1. gb.display.setColor(couleur)
2. gb.display.setCursor(x, y)
3. gb.display.print(valeur)   ← accepte char*, int, float, String…

La police par défaut fait 5×7 pixels (grossie ×2 = 10×14 px en pratique).
Vous pouvez afficher des variables avec printf-style :
    char buf[32];
    snprintf(buf, sizeof(buf), ""Score: %d"", score);
    gb.display.print(buf);",
                Category = "Graphismes",
                Tags = new() { "texte", "print", "HUD", "score" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static int score = 0;

//@@RENDER@@
    // Affichage texte / HUD
    gb.display.setColor(WHITE);
    gb.display.setCursor(4, 4);
    char hud[32];
    snprintf(hud, sizeof(hud), ""Score: %d"", score);
    gb.display.print(hud);
"
            },

            new CodeSnippet
            {
                Id = "gfx_draw_sprite",
                Name = "Affichage sprite 16 bits",
                Summary = "Dessine un sprite RGB565 avec couleur-clé de transparence.",
                Explanation =
@"graphics_draw_bitmap565(gfx, x, y, data, w, h, transparent_key, use_transparency)

data est un tableau uint16_t[] PROGMEM déclaré dans un .h d'assets.
La couleur-clé par défaut est 0xF81F (magenta).

Pour générer le tableau, utilisez l'éditeur de sprites intégré à l'IDE
(menu Sprites → exporter en C++), puis incluez le .h généré.

Conseil : nommez votre sprite SNAKE_CASE (player_idle, enemy_run…)
et utilisez player_idle_width / _height / _frames pour les dimensions.",
                Category = "Graphismes",
                Tags = new() { "sprite", "bitmap", "image", "animation" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@INCLUDES@@
// #include ""player_sprite.h""   // ← décommentez et remplacez par votre fichier d'asset

//@@RENDER@@
    // Affichage sprite (décommentez après avoir inclus le fichier d'asset)
    // graphics_draw_bitmap565(gb.display, playerX, playerY,
    //     player_sprite, player_sprite_width, player_sprite_height,
    //     0xF81F, true);
"
            },

            // ── SON ────────────────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "sound_beep",
                Name = "Son simple (tone)",
                Summary = "Joue un bip ou une note à une fréquence donnée.",
                Explanation =
@"gb.sound.tone(fréquenceHz, duréeMs) joue une note simple.

Fréquences courantes (octave 4) :
  DO  = 262 Hz   RÉ  = 294 Hz   MI  = 330 Hz
  FA  = 349 Hz   SOL = 392 Hz   LA  = 440 Hz   SI  = 494 Hz

Le son est non-bloquant ; la durée est gérée en arrière-plan.

Astuce : appelez gb.sound.tone(0, 0) pour arrêter un son en cours.",
                Category = "Son",
                Tags = new() { "son", "bip", "tone", "audio" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@UPDATE@@
    // Son simple au premier appui sur A (bip 440 Hz, 100 ms)
    if (gb.buttons.pressed(BUTTON_A)) {
        gb.sound.tone(440, 100);  // LA 4, 100 ms
    }
"
            },

            new CodeSnippet
            {
                Id = "sound_wav",
                Name = "Effet sonore WAV",
                Summary = "Joue un fichier WAV converti en tableau C (via WAV_SYSTEM).",
                Explanation =
@"Workflow :
1. Préparez votre WAV (8 kHz mono recommandé pour l'ESP32-S3).
2. Utilisez la banque sonore de l'IDE pour l'importer dans le projet.
3. Incluez le .h généré (qui contient la table WAV_SYSTEM).
4. Appelez gb.sound.playWav(nom_du_son, sizeof(nom_du_son)).

Ou, pour la lib AKA basse consommation :
    gb_audio_play_wav_system(&WAV_DATA, sizeof(WAV_DATA));",
                Category = "Son",
                Tags = new() { "son", "wav", "effet sonore", "audio" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@INCLUDES@@
// #include ""explode.h""  // ← votre fichier WAV_SYSTEM

//@@UPDATE@@
    // Joue le son WAV à l'appui sur A
    if (gb.buttons.pressed(BUTTON_A)) {
        // gb.sound.playWav(explode_data, sizeof(explode_data));
    }
"
            },

            // ── LOGIQUE JEUX ──────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "logic_score",
                Name = "Système de score",
                Summary = "Variable de score avec affichage et réinitialisation.",
                Explanation =
@"Implémentation minimaliste d'un score :
• Variable globale statique incrémentée à chaque événement de jeu.
• Affiché dans gameRender() via snprintf + display.print().
• Remis à zéro quand le joueur perd / restart.

Pour sauvegarder le highscore entre les parties :
  gb.save.set(0, highScore);
  highScore = gb.save.get(0);",
                Category = "Logique",
                Tags = new() { "score", "compteur", "highscore" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static int score     = 0;
static int highScore = 0;

//@@UPDATE@@
    // Incrémente le score (remplacez la condition par votre événement)
    // if (événement) { score += 10; if (score > highScore) highScore = score; }

//@@RENDER@@
    // Affiche le score
    gb.display.setColor(WHITE);
    gb.display.setCursor(4, 4);
    char scoreBuf[32];
    snprintf(scoreBuf, sizeof(scoreBuf), ""Score: %d  Best: %d"", score, highScore);
    gb.display.print(scoreBuf);
"
            },

            new CodeSnippet
            {
                Id = "logic_timer",
                Name = "Timer / cooldown",
                Summary = "Minuteur basé sur les frames pour des cooldowns ou délais.",
                Explanation =
@"gb.frameCount est incrémenté à chaque frame (tourne à ~30 FPS par défaut).

Patterns courants :
• Cooldown :
    if (frameCount - lastShot >= 20) { /* tir autorisé */ }
• Compte à rebours :
    int remaining = max(0, 300 - (int)(gb.frameCount - startFrame));
• Animation par frame :
    int frame = (gb.frameCount / 8) % frameCount;

Rappel : 30 frames ≈ 1 seconde.",
                Category = "Logique",
                Tags = new() { "timer", "cooldown", "frame", "délai" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static uint32_t lastActionFrame = 0;
static const uint32_t ACTION_COOLDOWN = 20; // frames (~0.67 s à 30 fps)

//@@UPDATE@@
    // Cooldown : action possible toutes les ACTION_COOLDOWN frames
    if (gb.frameCount - lastActionFrame >= ACTION_COOLDOWN) {
        if (gb.buttons.pressed(BUTTON_A)) {
            lastActionFrame = gb.frameCount;
            // Votre action ici
        }
    }
"
            },

            new CodeSnippet
            {
                Id = "logic_collision_rect",
                Name = "Collision AABB (rectangle)",
                Summary = "Détection de collision entre deux rectangles axis-aligned.",
                Explanation =
@"AABB = Axis-Aligned Bounding Box : la forme de collision la plus simple.

La fonction checkCollision(ax, ay, aw, ah, bx, by, bw, bh) retourne true
si les deux rectangles se chevauchent.

Pour une liste d'ennemis, parcourez le tableau avec une boucle et appelez
checkCollision pour chaque ennemi contre le joueur.",
                Category = "Logique",
                Tags = new() { "collision", "AABB", "physique", "overlap" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@FUNCTIONS@@
// Collision AABB : retourne true si les deux rectangles se chevauchent
static bool checkCollision(int ax, int ay, int aw, int ah,
                            int bx, int by, int bw, int bh) {
    return ax < bx + bw && ax + aw > bx
        && ay < by + bh && ay + ah > by;
}

//@@UPDATE@@
    // Exemple d'utilisation de checkCollision :
    // if (checkCollision(playerX, playerY, 8, 8, enemyX, enemyY, 8, 8)) {
    //     // collision détectée
    // }
"
            },

            new CodeSnippet
            {
                Id = "logic_state_machine",
                Name = "Machine à états",
                Summary = "Enum + switch pour gérer les états du jeu (menu, jeu, game over).",
                Explanation =
@"Une machine à états simple gère les différentes phases du jeu :
  MENU → PLAYING → GAME_OVER → MENU (cycle)

Chaque état a sa propre logique de mise à jour et son propre rendu.
Ajoutez des états selon vos besoins : PAUSE, LEVEL_COMPLETE, CREDITS…

Conseil : définissez une fonction update et render par état pour garder
le code lisible.",
                Category = "Logique",
                Tags = new() { "état", "FSM", "menu", "game over", "architecture" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
enum GameState { STATE_MENU, STATE_PLAYING, STATE_GAMEOVER };
static GameState currentState = STATE_MENU;

//@@UPDATE@@
    switch (currentState) {
        case STATE_MENU:
            if (gb.buttons.pressed(BUTTON_A)) currentState = STATE_PLAYING;
            break;
        case STATE_PLAYING:
            // Logique de jeu ici
            // if (playerDead) currentState = STATE_GAMEOVER;
            break;
        case STATE_GAMEOVER:
            if (gb.buttons.pressed(BUTTON_A)) { currentState = STATE_MENU; }
            break;
    }

//@@RENDER@@
    switch (currentState) {
        case STATE_MENU:
            gb.display.setColor(WHITE);
            gb.display.setCursor(100, 110);
            gb.display.print(""Appuyez sur A"");
            break;
        case STATE_GAMEOVER:
            gb.display.setColor(RED);
            gb.display.setCursor(116, 110);
            gb.display.print(""GAME OVER"");
            gb.display.setColor(WHITE);
            gb.display.setCursor(96, 130);
            gb.display.print(""A = Recommencer"");
            break;
        default: break;
    }
"
            },

            new CodeSnippet
            {
                Id = "logic_save",
                Name = "Sauvegarde / chargement",
                Summary = "Lit et écrit des entiers dans la mémoire persistante (NVS).",
                Explanation =
@"gb.save est une mini-base de données à cases numérotées (index 0…N).
Chaque case stocke un int32_t.

gb.save.set(index, valeur)   → écrit
gb.save.get(index)           → lit (renvoie 0 si jamais écrit)

Indices recommandés (convention libre) :
  0 = highscore    1 = niveau débloqué    2 = volume

Les données survivent aux redémarrages de la console.",
                Category = "Logique",
                Tags = new() { "sauvegarde", "highscore", "persistance", "nvs" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static int highScore = 0;

//@@UPDATE@@
    // Charger le highscore au démarrage (appelable une seule fois)
    // Placez cet appel dans une fonction d'initialisation ou en haut de gameUpdate.
    // highScore = gb.save.get(0);

    // Sauvegarde quand on bat le record
    // if (score > highScore) {
    //     highScore = score;
    //     gb.save.set(0, highScore);
    // }
"
            },

            // ── PHYSIQUE ──────────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "physics_gravity",
                Name = "Gravité simple",
                Summary = "Velocity + gravité pour des sauts réalistes.",
                Explanation =
@"Simulation de gravité en deux lignes :
  velocityY += GRAVITY;   // accélération vers le bas
  playerY   += velocityY; // déplacement

Saut : velocityY = -JUMP_FORCE  (valeur négative = vers le haut)

Valeurs typiques pour 30 fps, écran 240 px :
  GRAVITY    = 0.5f
  JUMP_FORCE = 8.0f
  MAX_FALL   = 12.0f (vitesse de chute maximale)

Ajoutez un sol en testant playerY >= FLOOR_Y.",
                Category = "Physique",
                Tags = new() { "gravité", "saut", "platformer", "velocity" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static float velocityY    = 0.0f;
static bool  isOnGround   = false;
static const float GRAVITY    = 0.5f;
static const float JUMP_FORCE = 8.0f;
static const float MAX_FALL   = 12.0f;
static const int   FLOOR_Y    = 220; // Y du sol

//@@UPDATE@@
    // Saut
    if (gb.buttons.pressed(BUTTON_A) && isOnGround) {
        velocityY  = -JUMP_FORCE;
        isOnGround = false;
    }

    // Gravité
    velocityY += GRAVITY;
    if (velocityY >  MAX_FALL) velocityY =  MAX_FALL;
    playerY += (int)velocityY;

    // Sol
    if (playerY >= FLOOR_Y) {
        playerY    = FLOOR_Y;
        velocityY  = 0;
        isOnGround = true;
    }
"
            },

            // ── CAMÉRA / SCROLL ────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "camera_scroll",
                Name = "Caméra scrollante",
                Summary = "Décalage de caméra pour un niveau plus grand que l'écran.",
                Explanation =
@"Une caméra simple est un décalage (cameraX, cameraY) soustrait à toutes
les positions avant le rendu.

Formule : screenX = worldX - cameraX

La caméra suit le joueur avec un offset pour le centrer :
  cameraX = playerX - 160;  // centré sur l'écran 320px
  cameraX = max(0, min(WORLD_WIDTH - 320, cameraX)); // clampe

Dessinez uniquement les objets dont screenX ∈ [-16, 336] (petite marge).",
                Category = "Caméra",
                Tags = new() { "caméra", "scroll", "monde", "niveau" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
static int cameraX = 0;
static int cameraY = 0;
static const int WORLD_WIDTH  = 1280; // largeur totale du monde
static const int WORLD_HEIGHT = 240;

//@@UPDATE@@
    // La caméra suit le joueur (centrage horizontal)
    cameraX = playerX - 160;
    cameraX = max(0, min(WORLD_WIDTH - 320, cameraX));

//@@RENDER@@
    // Rendu d'un objet avec décalage caméra
    // int screenX = worldObjX - cameraX;
    // int screenY = worldObjY - cameraY;
    // if (screenX > -16 && screenX < 336)
    //     gb.display.fillRect(screenX, screenY, 8, 8);
"
            },

            // ── DÉBOGAGE ──────────────────────────────────────────────────────────

            new CodeSnippet
            {
                Id = "debug_overlay",
                Name = "Overlay de débogage",
                Summary = "Affiche FPS, position et valeurs debug à l'écran.",
                Explanation =
@"Affiche des informations de débogage en superposition.

gb.getCpuLoad() renvoie le pourcentage CPU utilisé (0–100).
gb.frameCount est le numéro de frame courant.

Conseil : désactivez l'overlay en production avec un #define DEBUG_MODE.",
                Category = "Débogage",
                Tags = new() { "debug", "fps", "overlay", "développement" },
                ForPlatformIO = true, ForEspIdf = false,
                TargetFile = SnippetTargetFile.GameCpp,
                Code =
@"//@@GLOBALS@@
#define DEBUG_MODE 1  // Mettez 0 pour désactiver en production

//@@RENDER@@
#if DEBUG_MODE
    // Overlay de débogage (coin supérieur droit)
    gb.display.setColor(YELLOW);
    gb.display.setCursor(240, 4);
    char dbgBuf[40];
    snprintf(dbgBuf, sizeof(dbgBuf), ""CPU:%d%% X:%d Y:%d"",
             gb.getCpuLoad(), playerX, playerY);
    gb.display.print(dbgBuf);
#endif
"
            },


            // ── ENTRÉES (suite) ────────────────────────────────────────────────
            new CodeSnippet
            {
                Id = "input_menu_pause", Name = "Pause (bouton Menu)",
                Summary = "Met le jeu en pause avec BUTTON_MENU.",
                Explanation = @"Bascule un état pause à chaque appui sur BUTTON_MENU (gèle la logique + affiche PAUSE). À placer en premier dans //@@UPDATE@@.",
                Category = "Entrées", Tags = new() { "pause", "menu" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@GLOBALS@@
static bool paused = false;

//@@UPDATE@@
    if (gb.buttons.pressed(BUTTON_MENU)) paused = !paused;
    if (paused) return;

//@@RENDER@@
    if (paused) {
        gb.display.setColor(WHITE);
        gb.display.setCursor(140, 112);
        gb.display.print(""PAUSE"");
    }
"
            },

            // ── GRAPHISMES (suite) ─────────────────────────────────────────────
            new CodeSnippet
            {
                Id = "gfx_draw_line", Name = "Ligne",
                Summary = "Trace une ligne entre deux points.",
                Explanation = @"gb.display.drawLine(x0, y0, x1, y1) avec la couleur courante.",
                Category = "Graphismes", Tags = new() { "ligne", "dessin" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@RENDER@@
    gb.display.setColor(GREEN);
    gb.display.drawLine(10, 10, playerX, playerY);
"
            },
            new CodeSnippet
            {
                Id = "gfx_healthbar", Name = "Jauge de vie",
                Summary = "Barre de vie proportionnelle.",
                Explanation = @"Fond gris, remplissage proportionnel (vert, rouge sous 30 %), contour blanc.",
                Category = "Graphismes", Tags = new() { "vie", "jauge", "hud" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@GLOBALS@@
static int playerHP = 100;
static const int PLAYER_HP_MAX = 100;

//@@RENDER@@
    const int barX = 4, barY = 14, barW = 80, barH = 6;
    gb.display.setColor(DARKGRAY); gb.display.fillRect(barX, barY, barW, barH);
    int hpW = (barW * playerHP) / PLAYER_HP_MAX; if (hpW < 0) hpW = 0;
    gb.display.setColor(playerHP > 30 ? GREEN : RED); gb.display.fillRect(barX, barY, hpW, barH);
    gb.display.setColor(WHITE); gb.display.drawRect(barX, barY, barW, barH);
"
            },

            // ── LOGIQUE (suite) ────────────────────────────────────────────────
            new CodeSnippet
            {
                Id = "logic_animation", Name = "Animation de sprite",
                Summary = "Fait défiler les frames au fil du temps.",
                Explanation = @"animFrame = (gb.frameCount / ANIM_SPEED) % ANIM_FRAME_COUNT. Utilisez animFrame au rendu.",
                Category = "Logique", Tags = new() { "animation", "frame", "sprite" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@GLOBALS@@
static const uint8_t ANIM_FRAME_COUNT = 4;
static const uint8_t ANIM_SPEED       = 6;
static uint8_t        animFrame        = 0;

//@@UPDATE@@
    animFrame = (gb.frameCount / ANIM_SPEED) % ANIM_FRAME_COUNT;
"
            },
            new CodeSnippet
            {
                Id = "logic_random", Name = "Aléatoire",
                Summary = "Tirage de nombres aléatoires.",
                Explanation = @"random(min, max) renvoie un entier dans [min, max[. Initialisez avec randomSeed(analogRead(0)) au setup.",
                Category = "Logique", Tags = new() { "aléatoire", "random" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@UPDATE@@
    if (gb.buttons.pressed(BUTTON_B)) {
        int spawnX = random(0, 320 - 16);
        (void)spawnX;   // utilisez cette valeur (apparition, etc.)
    }
"
            },
            new CodeSnippet
            {
                Id = "logic_entities", Name = "Tableau d'entités",
                Summary = "Pool fixe (ennemis/projectiles) spawn/update/draw.",
                Explanation = @"Pool sans allocation dynamique ; spawnEntity() active un emplacement libre.",
                Category = "Logique", Tags = new() { "entités", "ennemis", "pool" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@GLOBALS@@
struct Entity { int x, y; int8_t vx, vy; bool active; };
static const int MAX_ENTITIES = 16;
static Entity entities[MAX_ENTITIES];

//@@FUNCTIONS@@
static void spawnEntity(int x, int y, int8_t vx, int8_t vy) {
    for (int i = 0; i < MAX_ENTITIES; i++)
        if (!entities[i].active) { entities[i] = { x, y, vx, vy, true }; return; }
}

//@@UPDATE@@
    for (int i = 0; i < MAX_ENTITIES; i++) {
        if (!entities[i].active) continue;
        entities[i].x += entities[i].vx; entities[i].y += entities[i].vy;
        if (entities[i].x < -16 || entities[i].x > 320 ||
            entities[i].y < -16 || entities[i].y > 240) entities[i].active = false;
    }

//@@RENDER@@
    gb.display.setColor(RED);
    for (int i = 0; i < MAX_ENTITIES; i++)
        if (entities[i].active) gb.display.fillRect(entities[i].x, entities[i].y, 8, 8);
"
            },

            // ── PHYSIQUE (suite) ───────────────────────────────────────────────
            new CodeSnippet
            {
                Id = "physics_velocity", Name = "Vélocité + friction",
                Summary = "Déplacement inertiel (accélération + friction).",
                Explanation = @"Accumule une vitesse (velX/velY) appliquée à la position, avec friction. Contrôle glissant.",
                Category = "Physique", Tags = new() { "vélocité", "inertie", "mouvement" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@GLOBALS@@
static float velX = 0.0f, velY = 0.0f;
static const float MOVE_ACCEL    = 0.4f;
static const float MOVE_FRICTION = 0.85f;

//@@UPDATE@@
    if (gb.buttons.repeat(BUTTON_LEFT,  1)) velX -= MOVE_ACCEL;
    if (gb.buttons.repeat(BUTTON_RIGHT, 1)) velX += MOVE_ACCEL;
    if (gb.buttons.repeat(BUTTON_UP,    1)) velY -= MOVE_ACCEL;
    if (gb.buttons.repeat(BUTTON_DOWN,  1)) velY += MOVE_ACCEL;
    velX *= MOVE_FRICTION;  velY *= MOVE_FRICTION;
    playerX += (int)velX;   playerY += (int)velY;
"
            },
            new CodeSnippet
            {
                Id = "physics_bounce", Name = "Rebond sur les bords",
                Summary = "Inverse la vitesse aux bords (nécessite la vélocité).",
                Explanation = @"Clampe la position (320x240) et inverse velX/velY. À combiner avec « Vélocité + friction ».",
                Category = "Physique", Tags = new() { "rebond", "bord", "bounce" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                RequiresSnippetIds = new() { "physics_velocity" },
                Code = @"//@@UPDATE@@
    if (playerX < 0)        { playerX = 0;        velX = -velX; }
    if (playerY < 0)        { playerY = 0;        velY = -velY; }
    if (playerX > 320 - 16) { playerX = 320 - 16; velX = -velX; }
    if (playerY > 240 - 16) { playerY = 240 - 16; velY = -velY; }
"
            },

            // ── INTERFACE ──────────────────────────────────────────────────────
            new CodeSnippet
            {
                Id = "ui_title_screen", Name = "Écran titre",
                Summary = "Accueil ; le jeu démarre sur A.",
                Explanation = @"Tant que gameStarted est faux, gèle le jeu et affiche l'écran titre. À placer en premier dans //@@UPDATE@@.",
                Category = "Interface", Tags = new() { "titre", "accueil", "start" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@GLOBALS@@
static bool gameStarted = false;

//@@UPDATE@@
    if (!gameStarted) {
        if (gb.buttons.pressed(BUTTON_A)) gameStarted = true;
        return;
    }

//@@RENDER@@
    if (!gameStarted) {
        gb.display.setColor(BLACK); gb.display.fill();
        gb.display.setColor(WHITE);
        gb.display.setCursor(110, 100); gb.display.print(""MON JEU AKA"");
        gb.display.setCursor(104, 130); gb.display.print(""Appuyez sur A"");
    }
"
            },
            new CodeSnippet
            {
                Id = "ui_simple_menu", Name = "Menu vertical",
                Summary = "Menu HAUT/BAS + validation A.",
                Explanation = @"Sélection au clavier directionnel avec bouclage ; A valide menuIndex.",
                Category = "Interface", Tags = new() { "menu", "navigation" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@GLOBALS@@
static const char* menuItems[] = { ""Jouer"", ""Options"", ""Quitter"" };
static const int   MENU_COUNT  = 3;
static int         menuIndex   = 0;

//@@UPDATE@@
    if (gb.buttons.pressed(BUTTON_UP))   menuIndex = (menuIndex + MENU_COUNT - 1) % MENU_COUNT;
    if (gb.buttons.pressed(BUTTON_DOWN)) menuIndex = (menuIndex + 1) % MENU_COUNT;
    if (gb.buttons.pressed(BUTTON_A)) {
        switch (menuIndex) { case 0: break; case 1: break; case 2: break; }
    }

//@@RENDER@@
    for (int i = 0; i < MENU_COUNT; i++) {
        gb.display.setColor(i == menuIndex ? YELLOW : WHITE);
        gb.display.setCursor(120, 90 + i * 16);
        gb.display.print(menuItems[i]);
    }
"
            },

            // ── SON / MUSIQUE (suite) ──────────────────────────────────────────
            new CodeSnippet
            {
                Id = "sound_sfx_jump", Name = "Effet : saut",
                Summary = "Bip court au saut (A).",
                Explanation = @"gb.sound.tone(fréquenceHz, duréeMs), non bloquant.",
                Category = "Son", Tags = new() { "effet", "saut" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@UPDATE@@
    if (gb.buttons.pressed(BUTTON_A)) gb.sound.tone(660, 60);
"
            },
            new CodeSnippet
            {
                Id = "sound_sfx_coin", Name = "Effet : pièce",
                Summary = "Cling aigu de ramassage.",
                Explanation = @"Note aiguë brève ; déclenchez sur collision joueur/objet.",
                Category = "Son", Tags = new() { "effet", "pièce", "bonus" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@UPDATE@@
    if (gb.buttons.pressed(BUTTON_B)) gb.sound.tone(988, 50);
"
            },
            new CodeSnippet
            {
                Id = "sound_sfx_hurt", Name = "Effet : dégât",
                Summary = "Note grave courte de dégât.",
                Explanation = @"Appelez sfxHurt() quand le joueur perd de la vie.",
                Category = "Son", Tags = new() { "effet", "dégât" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@FUNCTIONS@@
static inline void sfxHurt() { gb.sound.tone(120, 120); }

//@@UPDATE@@
    // Appelez sfxHurt(); au moment d'un dégât.
"
            },
            new CodeSnippet
            {
                Id = "music_victory", Name = "Jingle : victoire",
                Summary = "Fanfare ascendante jouée une fois.",
                Explanation = @"Séquenceur sur gb.frameCount. Déclenchez avec playVictory().",
                Category = "Son", Tags = new() { "musique", "jingle", "victoire" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@GLOBALS@@
static const uint16_t victoryNotes[][2] = { {392,6},{523,6},{659,6},{784,14} };
static const int VICTORY_LEN = 4;
static int      victoryStep = -1;
static uint32_t victoryNext = 0;

//@@FUNCTIONS@@
static void playVictory() { victoryStep = 0; victoryNext = gb.frameCount; }

//@@UPDATE@@
    if (victoryStep >= 0 && gb.frameCount >= victoryNext) {
        if (victoryStep < VICTORY_LEN) {
            gb.sound.tone(victoryNotes[victoryStep][0], (victoryNotes[victoryStep][1] * 1000) / 30);
            victoryNext = gb.frameCount + victoryNotes[victoryStep][1];
            victoryStep++;
        } else { victoryStep = -1; }
    }
"
            },
            new CodeSnippet
            {
                Id = "music_gameover", Name = "Jingle : game over",
                Summary = "Descente sombre jouée une fois.",
                Explanation = @"Déclenchez avec playGameOver().",
                Category = "Son", Tags = new() { "musique", "jingle", "défaite" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@GLOBALS@@
static const uint16_t goNotes[][2] = { {392,10},{311,10},{262,20} };
static const int GO_LEN = 3;
static int      goStep = -1;
static uint32_t goNext = 0;

//@@FUNCTIONS@@
static void playGameOver() { goStep = 0; goNext = gb.frameCount; }

//@@UPDATE@@
    if (goStep >= 0 && gb.frameCount >= goNext) {
        if (goStep < GO_LEN) {
            gb.sound.tone(goNotes[goStep][0], (goNotes[goStep][1] * 1000) / 30);
            goNext = gb.frameCount + goNotes[goStep][1];
            goStep++;
        } else { goStep = -1; }
    }
"
            },
            new CodeSnippet
            {
                Id = "sound_melody", Name = "Mélodie (séquence de notes)",
                Summary = "Joue une suite de notes en boucle.",
                Explanation = @"Tableau {fréquence, durée en frames} joué via gb.sound.tone (0 Hz = silence).",
                Category = "Son", Tags = new() { "musique", "mélodie", "jingle" },
                ForPlatformIO = true, ForEspIdf = false, TargetFile = SnippetTargetFile.GameCpp,
                Code = @"//@@GLOBALS@@
static const uint16_t melody[][2] = { {262,8}, {330,8}, {392,8}, {523,16}, {0,8} };
static const int MELODY_LEN      = 5;
static int       melodyStep      = 0;
static uint32_t  melodyNextFrame = 0;

//@@UPDATE@@
    if (gb.frameCount >= melodyNextFrame) {
        uint16_t freq = melody[melodyStep][0];
        uint16_t dur  = melody[melodyStep][1];
        if (freq > 0) gb.sound.tone(freq, (dur * 1000) / 30);
        melodyNextFrame = gb.frameCount + dur;
        melodyStep = (melodyStep + 1) % MELODY_LEN;
    }
"
            },

            // ══════════════════════════════════════════════════════════════════
            //  ESP-IDF (coquille : gfx / g_core). Le squelette app_main appelle
            //  g_core.pool() et gère playerX/playerY (D-pad + stick).
            // ══════════════════════════════════════════════════════════════════

            new CodeSnippet
            {
                Id = "esp_input", Name = "Entrées coquille (ESP-IDF)",
                Summary = "Boutons (événements) et joystick analogique.",
                Explanation = @"Le squelette appelle déjà g_core.pool() et déplace playerX/playerY.
Événements : g_core.buttons.pressed(gb_buttons::KEY_A). Maintenu : g_core.buttons.state() & KEY_x.
Stick : g_core.joystick.get_x()/get_y() (-1000..1000).",
                Category = "Entrées", Tags = new() { "input", "boutons", "joystick", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@UPDATE@@
        // Déplacement (D-pad + joystick émulé)
        uint16_t held = g_core.buttons.state() | g_core.joystick.state();
        if (held & gb_buttons::KEY_LEFT)  playerX -= PLAYER_SPEED;
        if (held & gb_buttons::KEY_RIGHT) playerX += PLAYER_SPEED;
        if (held & gb_buttons::KEY_UP)    playerY -= PLAYER_SPEED;
        if (held & gb_buttons::KEY_DOWN)  playerY += PLAYER_SPEED;
        if (playerX < 0) playerX = 0; else if (playerX > 315) playerX = 315;
        if (playerY < 0) playerY = 0; else if (playerY > 235) playerY = 235;
        // Événement
        if (g_core.buttons.pressed(gb_buttons::KEY_A)) {
            // action A
        }
"
            },

            new CodeSnippet
            {
                Id = "esp_gfx_shapes", Name = "Formes (ESP-IDF)",
                Summary = "Rectangle, contour, ligne, cercle avec gb_graphics.",
                Explanation = @"gfx.fillRect / drawRect / drawLine / fillCircle. Couleur :
gfx.setColor(gfx.makeColor(r,g,b)) (0..255, BGR565 automatique).",
                Category = "Graphismes", Tags = new() { "formes", "cercle", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@RENDER@@
        gfx.setColor(gfx.makeColor(0, 120, 255));
        gfx.fillRect(playerX, playerY, 16, 16);
        gfx.setColor(gfx.makeColor(255, 255, 255));
        gfx.drawRect(playerX - 1, playerY - 1, 18, 18);
        gfx.setColor(gfx.makeColor(255, 200, 0));
        gfx.drawLine(10, 10, playerX, playerY);
        gfx.fillCircle(40, 200, 8);
"
            },

            new CodeSnippet
            {
                Id = "esp_gfx_text", Name = "Texte / score (ESP-IDF)",
                Summary = "Texte formaté avec gfx.printf.",
                Explanation = @"gfx.move_cursor(x, y) puis gfx.printf(format, ...) ou gfx.print_str(""texte"").",
                Category = "Graphismes", Tags = new() { "texte", "score", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@RENDER@@
        gfx.setColor(gfx.makeColor(255, 255, 255));
        gfx.move_cursor(4, 4);
        gfx.print_str(""Bonjour Gamebuino AKA!"");
"
            },

            new CodeSnippet
            {
                Id = "esp_healthbar", Name = "Jauge de vie (ESP-IDF)",
                Summary = "Barre de vie proportionnelle (gfx.fillRect).",
                Explanation = @"Fond gris, remplissage proportionnel (vert, rouge sous 30 %), contour blanc.",
                Category = "Graphismes", Tags = new() { "vie", "jauge", "hud", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static int hp = 100;
static const int HP_MAX = 100;

//@@RENDER@@
        const int bx = 4, by = 16, bw = 80, bh = 6;
        gfx.setColor(gfx.makeColor(60, 60, 60));  gfx.fillRect(bx, by, bw, bh);
        int w = (bw * hp) / HP_MAX; if (w < 0) w = 0;
        gfx.setColor(hp > 30 ? gfx.makeColor(0, 200, 0) : gfx.makeColor(220, 0, 0));
        gfx.fillRect(bx, by, w, bh);
        gfx.setColor(gfx.makeColor(255, 255, 255)); gfx.drawRect(bx, by, bw, bh);
"
            },

            new CodeSnippet
            {
                Id = "esp_animation", Name = "Animation de sprite (ESP-IDF)",
                Summary = "Défilement de frames via g_core.get_millis().",
                Explanation = @"Pas de frameCount en ESP-IDF : on cadence avec le temps réel.
animFrame = (g_core.get_millis() / ANIM_MS) % ANIM_FRAMES.",
                Category = "Logique", Tags = new() { "animation", "frame", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static const uint8_t  ANIM_FRAMES = 4;
static const uint32_t ANIM_MS     = 120;

//@@RENDER@@
        uint8_t animFrame = (uint8_t)((g_core.get_millis() / ANIM_MS) % ANIM_FRAMES);
        (void)animFrame; // choisir la sous-image du sprite selon animFrame
"
            },

            new CodeSnippet
            {
                Id = "esp_entities", Name = "Tableau d'entités (ESP-IDF)",
                Summary = "Pool fixe (spawn/update/draw) rendu avec gfx.",
                Explanation = @"Pool sans allocation dynamique. spawnEntity() active un emplacement libre ;
la boucle déplace et désactive hors écran ; rendu via gfx.fillRect.",
                Category = "Logique", Tags = new() { "entités", "ennemis", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
struct Entity { int x, y; int8_t vx, vy; bool active; };
static const int MAX_ENTITIES = 16;
static Entity entities[MAX_ENTITIES];

//@@FUNCTIONS@@
static void spawnEntity(int x, int y, int8_t vx, int8_t vy) {
    for (int i = 0; i < MAX_ENTITIES; i++)
        if (!entities[i].active) { entities[i] = { x, y, vx, vy, true }; return; }
}

//@@UPDATE@@
        for (int i = 0; i < MAX_ENTITIES; i++) {
            if (!entities[i].active) continue;
            entities[i].x += entities[i].vx;
            entities[i].y += entities[i].vy;
            if (entities[i].x < -16 || entities[i].x > 320 ||
                entities[i].y < -16 || entities[i].y > 240) entities[i].active = false;
        }

//@@RENDER@@
        gfx.setColor(gfx.makeColor(220, 40, 40));
        for (int i = 0; i < MAX_ENTITIES; i++)
            if (entities[i].active) gfx.fillRect(entities[i].x, entities[i].y, 8, 8);
"
            },

            new CodeSnippet
            {
                Id = "esp_title", Name = "Écran titre (ESP-IDF)",
                Summary = "Écran d'accueil ; A démarre la partie.",
                Explanation = @"Tant que gameStarted est faux, un écran titre est affiché et A démarre.",
                Category = "Interface", Tags = new() { "titre", "start", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static bool gameStarted = false;

//@@UPDATE@@
        if (!gameStarted && g_core.buttons.pressed(gb_buttons::KEY_A)) gameStarted = true;

//@@RENDER@@
        if (!gameStarted) {
            gfx.clear(gfx.makeColor(0, 0, 0));
            gfx.setColor(gfx.makeColor(255, 255, 255));
            gfx.move_cursor(110, 100); gfx.print_str(""MON JEU AKA"");
            gfx.move_cursor(104, 130); gfx.print_str(""Appuyez sur A"");
        }
"
            },

            new CodeSnippet
            {
                Id = "esp_pause", Name = "Pause (ESP-IDF)",
                Summary = "Bascule pause avec MENU.",
                Explanation = @"gb_buttons::KEY_MENU bascule ""paused"". Gardez votre logique avec if (!paused). (Pas de return : le bloc est injecté dans la boucle.)",
                Category = "Entrées", Tags = new() { "pause", "menu", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static bool paused = false;

//@@UPDATE@@
        if (g_core.buttons.pressed(gb_buttons::KEY_MENU)) paused = !paused;

//@@RENDER@@
        if (paused) {
            gfx.setColor(gfx.makeColor(255, 255, 255));
            gfx.move_cursor(140, 112);
            gfx.print_str(""PAUSE"");
        }
"
            },
            new CodeSnippet
            {
                Id = "esp_fill_background", Name = "Fond (ESP-IDF)",
                Summary = "Remplit l'écran d'une couleur.",
                Explanation = @"gfx.clear(gfx.makeColor(r,g,b)).",
                Category = "Graphismes", Tags = new() { "fond", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@RENDER@@
        gfx.clear(gfx.makeColor(10, 10, 30));
"
            },
            new CodeSnippet
            {
                Id = "esp_draw_sprite", Name = "Dessiner un sprite (ESP-IDF)",
                Summary = "Blit d'un sprite BGR565 avec transparence.",
                Explanation = @"Helper drawSprite565(x,y,data,w,h,key) : dessine pixel par pixel en ignorant la couleur-clé. Exportez un sprite depuis l'éditeur puis appelez-le.",
                Category = "Graphismes", Tags = new() { "sprite", "blit", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@FUNCTIONS@@
static void drawSprite565(int x, int y, const uint16_t* data, int w, int h, uint16_t key) {
    for (int sy = 0; sy < h; sy++)
        for (int sx = 0; sx < w; sx++) {
            uint16_t px = data[sy * w + sx];
            if (px != key) gfx.drawPixel(x + sx, y + sy, px);
        }
}

//@@RENDER@@
        // drawSprite565(playerX, playerY, monSprite, monSprite_width, monSprite_height, 0xF81F);
"
            },
            new CodeSnippet
            {
                Id = "esp_score", Name = "Score + record (ESP-IDF)",
                Summary = "Compteur de score et meilleur score.",
                Explanation = @"Incrémente score (A), suit highScore, et l'affiche.",
                Category = "Logique", Tags = new() { "score", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static int score = 0;
static int highScore = 0;

//@@UPDATE@@
        if (g_core.buttons.pressed(gb_buttons::KEY_A)) { score += 10; if (score > highScore) highScore = score; }

//@@RENDER@@
        gfx.setColor(gfx.makeColor(255, 255, 255));
        gfx.move_cursor(4, 4);
        gfx.printf(""Score: %d  Best: %d"", score, highScore);
"
            },
            new CodeSnippet
            {
                Id = "esp_timer", Name = "Chrono (ESP-IDF)",
                Summary = "Compte à rebours via g_core.get_millis().",
                Explanation = @"Décompte de TIMER_MS millisecondes depuis le démarrage.",
                Category = "Logique", Tags = new() { "timer", "chrono", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static uint32_t timerStartMs = 0;
static const uint32_t TIMER_MS = 30000;

//@@UPDATE@@
        if (timerStartMs == 0) timerStartMs = g_core.get_millis();

//@@RENDER@@
        uint32_t elapsed = g_core.get_millis() - timerStartMs;
        int remaining = (int)((TIMER_MS > elapsed ? TIMER_MS - elapsed : 0) / 1000);
        gfx.setColor(gfx.makeColor(255, 255, 255));
        gfx.move_cursor(260, 4);
        gfx.printf(""T:%d"", remaining);
"
            },
            new CodeSnippet
            {
                Id = "esp_collision_rect", Name = "Collision AABB (ESP-IDF)",
                Summary = "Test de collision rectangle/rectangle.",
                Explanation = @"Helper aabb(...) : vrai si deux rectangles se chevauchent.",
                Category = "Logique", Tags = new() { "collision", "aabb", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@FUNCTIONS@@
static bool aabb(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh) {
    return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
}

//@@UPDATE@@
        // if (aabb(playerX, playerY, 16, 16, ex, ey, 16, 16)) { /* collision */ }
"
            },
            new CodeSnippet
            {
                Id = "esp_state_machine", Name = "Machine à états (ESP-IDF)",
                Summary = "Menu / Jeu / Game Over.",
                Explanation = @"enum GameState + switch. A fait avancer les états.",
                Category = "Logique", Tags = new() { "état", "machine", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
enum GameState { GS_MENU, GS_PLAY, GS_OVER };
static GameState gameState = GS_MENU;

//@@UPDATE@@
        switch (gameState) {
            case GS_MENU: if (g_core.buttons.pressed(gb_buttons::KEY_A)) gameState = GS_PLAY; break;
            case GS_PLAY: /* logique de jeu */ break;
            case GS_OVER: if (g_core.buttons.pressed(gb_buttons::KEY_A)) gameState = GS_MENU; break;
        }
"
            },
            new CodeSnippet
            {
                Id = "esp_random", Name = "Aléatoire (ESP-IDF)",
                Summary = "Générateur matériel esp_random().",
                Explanation = @"esp_random() renvoie un uint32 matériel. Modulo pour borner.",
                Category = "Logique", Tags = new() { "aléatoire", "random", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@INCLUDES@@
#include ""esp_random.h""

//@@UPDATE@@
        if (g_core.buttons.pressed(gb_buttons::KEY_B)) {
            int spawnX = (int)(esp_random() % (320 - 16));
            (void)spawnX;
        }
"
            },
            new CodeSnippet
            {
                Id = "esp_gravity", Name = "Gravité + saut (ESP-IDF)",
                Summary = "Chute + saut avec A.",
                Explanation = @"Applique une gravité à vy, saute quand au sol. Alternative à la vélocité.",
                Category = "Physique", Tags = new() { "gravité", "saut", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static float vy = 0.0f;
static const float GRAVITY = 0.3f;

//@@UPDATE@@
        vy += GRAVITY;
        playerY += (int)vy;
        if (playerY >= 235) { playerY = 235; vy = 0.0f; }
        if (g_core.buttons.pressed(gb_buttons::KEY_A) && playerY >= 235) vy = -6.0f;
"
            },
            new CodeSnippet
            {
                Id = "esp_velocity", Name = "Vélocité + friction (ESP-IDF)",
                Summary = "Déplacement inertiel (alternative à esp_input).",
                Explanation = @"Accumule velX/velY selon les directions, avec friction. N'utilisez PAS esp_input en même temps (les deux déplacent le joueur).",
                Category = "Physique", Tags = new() { "vélocité", "inertie", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static float velX = 0.0f, velY = 0.0f;
static const float MOVE_ACCEL = 0.4f;
static const float MOVE_FRICTION = 0.85f;

//@@UPDATE@@
        uint16_t vheld = g_core.buttons.state() | g_core.joystick.state();
        if (vheld & gb_buttons::KEY_LEFT)  velX -= MOVE_ACCEL;
        if (vheld & gb_buttons::KEY_RIGHT) velX += MOVE_ACCEL;
        if (vheld & gb_buttons::KEY_UP)    velY -= MOVE_ACCEL;
        if (vheld & gb_buttons::KEY_DOWN)  velY += MOVE_ACCEL;
        velX *= MOVE_FRICTION; velY *= MOVE_FRICTION;
        playerX += (int)velX; playerY += (int)velY;
"
            },
            new CodeSnippet
            {
                Id = "esp_bounce", Name = "Rebond sur les bords (ESP-IDF)",
                Summary = "Inverse la vitesse aux bords.",
                Explanation = @"Clampe (320x240) et inverse velX/velY. À combiner avec esp_velocity.",
                Category = "Physique", Tags = new() { "rebond", "bounce", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                RequiresSnippetIds = new() { "esp_velocity" },
                Code = @"//@@UPDATE@@
        if (playerX < 0)        { playerX = 0;        velX = -velX; }
        if (playerY < 0)        { playerY = 0;        velY = -velY; }
        if (playerX > 320 - 16) { playerX = 320 - 16; velX = -velX; }
        if (playerY > 240 - 16) { playerY = 240 - 16; velY = -velY; }
"
            },
            new CodeSnippet
            {
                Id = "esp_camera_scroll", Name = "Caméra (ESP-IDF)",
                Summary = "Décalage de caméra centré sur le joueur.",
                Explanation = @"camX/camY suivent le joueur ; dessinez le monde décalé de (-camX,-camY).",
                Category = "Caméra", Tags = new() { "caméra", "scroll", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static int camX = 0, camY = 0;

//@@UPDATE@@
        camX = playerX - 160;
        camY = playerY - 120;

//@@RENDER@@
        // Dessinez chaque élément décalé : gfx.fillRect(worldX - camX, worldY - camY, w, h);
"
            },
            new CodeSnippet
            {
                Id = "esp_debug", Name = "Overlay debug (ESP-IDF)",
                Summary = "FPS + position affichés en bas.",
                Explanation = @"Compte les frames par seconde via g_core.get_millis() et affiche FPS/position.",
                Category = "Débogage", Tags = new() { "debug", "fps", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static uint32_t fpsLast = 0;
static int fpsCount = 0, fps = 0;

//@@UPDATE@@
        fpsCount++;
        if (g_core.get_millis() - fpsLast >= 1000) { fps = fpsCount; fpsCount = 0; fpsLast = g_core.get_millis(); }

//@@RENDER@@
        gfx.setColor(gfx.makeColor(255, 255, 0));
        gfx.move_cursor(4, 230);
        gfx.printf(""FPS:%d X:%d Y:%d"", fps, playerX, playerY);
"
            },
            new CodeSnippet
            {
                Id = "esp_menu", Name = "Menu vertical (ESP-IDF)",
                Summary = "Menu HAUT/BAS + validation A.",
                Explanation = @"Sélection avec bouclage ; A valide menuIndex.",
                Category = "Interface", Tags = new() { "menu", "esp-idf" },
                ForPlatformIO = false, ForEspIdf = true, TargetFile = SnippetTargetFile.MainCpp,
                Code = @"//@@GLOBALS@@
static const char* menuItems[] = { ""Jouer"", ""Options"", ""Quitter"" };
static const int MENU_COUNT = 3;
static int menuIndex = 0;

//@@UPDATE@@
        if (g_core.buttons.pressed(gb_buttons::KEY_UP))   menuIndex = (menuIndex + MENU_COUNT - 1) % MENU_COUNT;
        if (g_core.buttons.pressed(gb_buttons::KEY_DOWN)) menuIndex = (menuIndex + 1) % MENU_COUNT;
        if (g_core.buttons.pressed(gb_buttons::KEY_A)) { /* valider menuIndex */ }

//@@RENDER@@
        for (int i = 0; i < MENU_COUNT; i++) {
            gfx.setColor(i == menuIndex ? gfx.makeColor(255, 255, 0) : gfx.makeColor(255, 255, 255));
            gfx.move_cursor(120, 90 + i * 16);
            gfx.print_str(menuItems[i]);
        }
"
            },
        };
    }
}
