# Éditeur de code multi-langage — état des lieux

Ajouté : un écran « Éditeur de code » dans AKA-IDE, avec coloration syntaxique et envoi direct vers
la console AKA par port série. Conçu pour accueillir plusieurs langages (Lua/AKA-Love, MicroPython AKA,
un futur BASIC…) sans dupliquer l'éditeur ni le transport : seule une `LanguageDefinition` change.

## ⚠️ Non compilé, non testé sur ce poste (sauf le vérificateur de code, voir plus bas)

Ce dépôt de travail n'a pas d'accès au catalogue NuGet (`api.nuget.org` est bloqué) : impossible de lancer
`dotnet restore`/`build`/`test` ici. Tout ce qui suit a été écrit et relu avec soin contre les
conventions déjà en place dans le projet (mêmes patterns que `SoundBankViewModel`, `ISettingsService`…),
et contre la documentation publique d'AvaloniaEdit, mais **le premier `dotnet build` sur votre poste est
le vrai test**. Points à surveiller en premier si ça ne compile pas :

1. **Version du paquet `Avalonia.AvaloniaEdit`** (`GamebuinoAKA.App.csproj`) : mise à `11.2.0` par défaut,
   sans avoir pu vérifier qu'elle existe telle quelle sur NuGet — ajustez à la version disponible la
   plus proche de votre `Avalonia` (11.2.3).
2. **`System.IO.Ports` sous Linux** : le paquet compile et s'exécute sur Linux, mais l'utilisateur doit
   appartenir au groupe `dialout` pour accéder à `/dev/ttyUSB*`/`/dev/ttyACM*` — déjà noté dans le
   README pour ESP-IDF, vaut aussi pour l'éditeur.

## Ce qui a été ajouté (fichiers nouveaux ou modifiés)

**GamebuinoAKA.Core** (portable, testé — voir `tests/GamebuinoAKA.Core.Tests/`) :
- `Models/CodeLanguage.cs`, `Models/LanguageDefinition.cs` — les DONNÉES qui distinguent un langage
  (mots-clés, API, extension, racine SD, `lang_id` du protocole). Ajouter un langage = une entrée ici.
- `Services/ILanguageDefinitionService.cs` + `LanguageDefinitionService.cs` — catalogue Lua/MicroPython,
  avec les vraies listes de mots-clés et d'API (Lua/love.* depuis le dépôt AKA-Love ; aka.*/upygame.*
  depuis `components/micropython/modaka.c` et `sdcard_files/py/upygame.py` du dépôt MicroPython-AKA).
- `Models/CodeCheckResult.cs`, `Services/ICodeCheckService.cs` + `CodeCheckService.cs` — le
  vérificateur de code avant envoi (voir la section dédiée plus bas). Test : `CodeCheckServiceTests.cs`.
- `Services/ITransferService.cs` + `SerialTransferService.cs` — protocole **AKAT v1.1** (PING/PUT),
  CRC32 compatible bit à bit avec `zlib.crc32` (vérifié par test unitaire contre une valeur de référence
  calculée en Python) et avec l'implémentation côté appareil (`transfer.cpp` du dépôt AKA-Love, elle
  testée par 18 contrôles automatiques sur simulateur, écran interactif compris). Gère la réponse à
  longueur variable du statut `OkRenamed` (le joueur a choisi « Renommer » sur la console — voir plus
  bas) et un délai de 35 s (le temps que l'appareil laisse au joueur décider en cas de conflit). Voir
  `docs/PROTOCOLE_TRANSFERT.md` du dépôt AKA-Love pour la spécification complète.
- Nouveaux tests : `LanguageDefinitionServiceTests.cs`, `Crc32Tests.cs`, `TransferResultTests.cs`.
- `GamebuinoAKA.Core.csproj` : ajout de `System.IO.Ports`.

**GamebuinoAKA.App** (UI Avalonia — non testable ici, voir plus haut) :
- `ViewModels/CodeEditorViewModel.cs` — Nouveau/Ouvrir/Enregistrer/Envoyer vers l'AKA.
- `Views/CodeEditorView.axaml` + `.axaml.cs` — l'écran : sélecteur de langage, `AvaloniaEdit.TextEditor`,
  port série + chemin sur l'appareil + bouton d'envoi. Le code-behind charge la bonne grammaire `.xshd`
  selon le langage choisi (AvaloniaEdit ne fait pas ce lien tout seul).
- `Assets/Highlighting/Lua.xshd`, `Assets/Highlighting/Python.xshd` — grammaires de coloration,
  mots-clés du langage en bleu, API AKA (`love.*` / `aka.*`) en une couleur distincte.
- `MainViewModel.cs`, `MainWindow.axaml`, `Bootstrapper.cs` — nouvel écran câblé dans la navigation,
  comme les autres (bouton « 📝 Éditeur de code »).
- `App.axaml` : ajout du style AvaloniaEdit requis.
- `GamebuinoAKA.App.csproj` : ajout de `Avalonia.AvaloniaEdit` + les `.xshd` en `AvaloniaResource`.

## Vérificateur de code avant envoi (nouveau)

Un contrôle STRUCTUREL (pas un compilateur complet) tourne avant chaque envoi vers l'AKA — bouton
« ✓ Vérifier » pour le lancer sans envoyer, ou automatiquement au clic sur « Envoyer ».

- **Lua** : un vrai tokeniseur de la grammaire lexicale Lua 5.1 (chaînes courtes/longues, commentaires
  courts/longs avec le bon comptage des `=` des crochets `[==[...]==]`), plus l'équilibre des mots-clés
  de bloc (`if`/`do`/`function` → `end` ; `repeat` → `until`). Repère un `end` oublié, en trop, ou
  substitué à `until`, une parenthèse/chaîne non refermée.
- **MicroPython** : équilibre des parenthèses/crochets/accolades et chaînes (simples ou
  triple-guillemets) non refermées, **plus l'indentation** (mis à jour depuis la version précédente de
  cette note) — avec l'algorithme du vrai tokeniseur CPython : une désindentation qui ne retombe sur
  aucun niveau connu, ou un mélange de tabulations/espaces dont le résultat dépend de la largeur de
  tabulation supposée (TabError), sont signalés. Ne vérifie toujours pas la structure des blocs
  (if/def/while/for → nécessiterait de distinguer un ":" de bloc d'un ":" de tranche/dictionnaire, sans
  vrai analyseur), et ne reconnaît pas les continuations de ligne par "\" (rare en MicroPython).
- Un échec n'empêche PAS l'envoi pour de bon : la personne clique une seconde fois sur « Envoyer » (avec
  le même code) pour l'envoyer quand même — le contrôle est heuristique, un faux positif ne doit pas
  bloquer.

### ⚠️ Vérifié différemment du reste de ce document

**Coup de chance en cours de route : ce sandbox a un accès réseau à `archive.ubuntu.com`, qui héberge
le paquet `dotnet-sdk-10.0` — installé avec succès.** `CodeCheckService.cs` (la logique du vérificateur
elle-même, sans dépendance à Avalonia/NuGet) a donc été **réellement compilé avec le vrai compilateur
.NET 10 et exécuté** contre 52 cas de test (dont du code réel extrait de `cassebriques/main.lua` et de
`sdcard_files/py/main.py`) — tous corrects. NuGet (`api.nuget.org`) reste bloqué par le réseau du
sandbox : impossible de restaurer Avalonia/AvaloniaEdit/CommunityToolkit.Mvvm/Newtonsoft.Json, donc
**l'intégration dans `CodeEditorViewModel.cs`/`CodeEditorView.axaml` (câblage du bouton, la logique
"cliquer deux fois pour forcer l'envoi") reste écrite mais non compilée**, comme le reste de cet écran.
La distinction compte : le CŒUR du vérificateur est prouvé correct, seul son branchage dans l'écran ne
l'est pas encore.

## Réception interactive côté console (nouveau)

AKA-Love a maintenant un **écran « Recevoir un code »** sur la console elle-même (pas seulement côté
PC) : entré en tenant le bouton **D** au démarrage (en attendant un vrai menu — phase P6, lanceur de
jeux, pas encore construite). Il attend indéfiniment, crée les dossiers manquants tout seul, et **si le
fichier existe déjà, demande au joueur** : A = Écraser, D = Renommer (ajoute « (2) », « (3) »… avant
l'extension), B = Annuler ce fichier (30 s pour répondre, sinon annulé par sécurité). MENU quitte l'écran
à tout moment. Rien à faire de plus côté AKA-IDE que d'attendre plus longtemps la réponse (déjà fait,
`ReplyTimeout` = 35 s) : la décision se prend entièrement sur la console.

## Pas fait (hors scope de ce premier passage)

- **BASIC** : aucun interpréteur BASIC ne tourne encore sur l'AKA (confirmé) — `CodeLanguage`/
  `LanguageDefinitionService` sont prêts à en recevoir un (`lang_id = 2`), sans autre changement.
- **Réception côté MicroPython** : le protocole réserve `lang_id = 1`, mais le firmware MicroPython AKA
  n'implémente pas encore sa réception (seul AKA-Love le fait, dans son propre dépôt).
- **Commandes `LIST`/`GET`** (parcourir les fichiers déjà sur l'appareil) : le protocole les anticipe
  mais seuls PING/PUT existent en v1.
- **Complétion automatique** dans l'éditeur (liste `ApiSymbols` déjà prête côté `LanguageDefinition`,
  mais pas encore branchée à un `CompletionWindow` d'AvaloniaEdit).
