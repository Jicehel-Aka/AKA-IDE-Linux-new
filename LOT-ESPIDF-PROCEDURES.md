# Procédures ESP-IDF ajoutées + tests protégés

## Le problème
Le `CodeSnippetService` du dépôt Linux avait la **génération** ESP-IDF
(`GenerateEspIdf`/`BuildAppMain`) mais **aucune procédure** `ForEspIdf` dans le
catalogue → `GetAll(EspIdf)` renvoyait une liste vide. La partie Espressif du
sélecteur était donc inutile.

## Corrections
1. **7 procédures ESP-IDF** ajoutées à `BuiltinSnippets` (API coquille `gfx`/`g_core`) :
   `esp_input`, `esp_gfx_shapes`, `esp_gfx_text`, `esp_healthbar`,
   `esp_animation` (cadence via `g_core.get_millis()`), `esp_entities`, `esp_title`.
2. **`BuildAppMain` enrichi** : appelle `g_core.pool()`, définit `playerX/playerY`
   + déplacement D-pad/joystick, et ajoute une zone `//@@SETUP@@` — sans quoi les
   snippets d'entrée/joueur ne fonctionneraient pas.
3. **Tests protégés ET réels** (`CodeSnippetServiceTests`) :
   - `GetAll_EspIdf_ReturnsOnlyEspIdfSnippets` : filtrage correct, **robuste au cas
     vide** (ne casse pas si le catalogue n'a pas d'ESP-IDF).
   - `GetAll_EspIdf_ContainsBuiltinProcedures` : vérifie que les procédures ESP-IDF
     existent bien maintenant.
   - `GenerateFiles_EspIdf_InjectsIntoAppMain` : injection réelle dans `app_main.cpp`.
   - `GenerateFiles_InjectsSnippetMarkerContent` : moteur testé via snippet synthétique
     (indépendant du catalogue).

## Fichiers (à écraser)
```
src/GamebuinoAKA.Core/Services/CodeSnippetService.cs        # +7 snippets ESP-IDF, BuildAppMain enrichi
tests/GamebuinoAKA.Core.Tests/CodeSnippetServiceTests.cs   # tests robustes + réels
```

## Vérif
`dotnet test` : les 2 tests qui échouaient passent, plus les nouveaux. ~76 tests au vert.
