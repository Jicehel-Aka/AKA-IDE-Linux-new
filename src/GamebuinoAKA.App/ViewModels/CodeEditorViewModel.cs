using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebuinoAKA.Core.Models;
using GamebuinoAKA.Core.Services;

namespace GamebuinoAKA.App.ViewModels
{
    /// <summary>
    /// Éditeur de code multi-langage (Lua / MicroPython, voir LanguageDefinitionService) avec envoi
    /// direct vers l'AKA par port série (protocole AKAT, voir ITransferService). La coloration
    /// syntaxique elle-même (AvaloniaEdit) vit dans la vue — ce ViewModel ne connaît que des chaînes.
    ///
    /// NON TESTÉ : ce dépôt sandbox n'a ni SDK .NET ni port série pour compiler/exécuter cet écran.
    /// Écrit et relu contre les conventions déjà en place dans les autres ViewModels (SoundBankViewModel
    /// notamment) et contre ITransferService (lui testé côté GamebuinoAKA.Core.Tests).
    /// </summary>
    public sealed partial class CodeEditorViewModel : ViewModelBase
    {
        private readonly ILanguageDefinitionService _languages;
        private readonly ITransferService _transfer;
        private readonly ICodeCheckService _checker;
        private readonly IFileDialogService _files;
        private readonly ISettingsService _settings;
        // Code déjà vérifié (et jugé invalide) au moment du dernier clic sur "Envoyer" -- permet un
        // second clic "j'envoie quand même" SANS ouvrir un vrai dialogue, tant que le code n'a pas
        // changé entre-temps (voir SendToDeviceAsync). Le vérificateur est heuristique (voir
        // CodeCheckService) : bloquer sans recours serait pénalisant sur un faux positif.
        private string? _lastRejectedCode;

        public ObservableCollection<LanguageDefinition> Languages { get; }

        [ObservableProperty] private LanguageDefinition _selectedLanguage;
        [ObservableProperty] private string _code = string.Empty;
        [ObservableProperty] private string? _filePath;
        [ObservableProperty] private string _devicePath = string.Empty;
        [ObservableProperty] private string _portName = string.Empty;
        [ObservableProperty] private string _status = "Prêt.";
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private CodeCheckResult? _lastCheckResult;

        // Document AvaloniaEdit lié par la vue (Document="{Binding EditorDocument}") -- AvaloniaEdit
        // 11.2.0 n'expose pas TextEditor.Text comme propriété liable par le compilateur XAML
        // (erreur AVLN3000 constatée à la compilation) ; Document, lui, l'est. Instance FIXE (jamais
        // réassignée) : on ne synchronise que son .Text, dans les deux sens, avec Code -- qui reste la
        // source de vérité utilisée partout ailleurs dans ce ViewModel (envoi, enregistrement, vérif).
        // Les deux gardes "if (... != ...)" ci-dessous évitent la boucle infinie entre les deux
        // évènements de changement qui, sinon, se redéclencheraient l'un l'autre indéfiniment.
        public TextDocument EditorDocument { get; } = new();

        public string Title => FilePath is null ? "(nouveau)" : Path.GetFileName(FilePath);

        // Couleur du texte de statut : rouge si le dernier contrôle a trouvé un problème, violet sinon
        // (couleur "neutre" déjà utilisée ailleurs dans l'IDE). Recalculée via OnLastCheckResultChanged.
        public string StatusColor => LastCheckResult is { Ok: false } ? "#E05252" : "#8A63D2";

        public CodeEditorViewModel(ILanguageDefinitionService languages, ITransferService transfer,
            ICodeCheckService checker, IFileDialogService files, ISettingsService settings)
        {
            _languages = languages;
            _transfer = transfer;
            _checker = checker;
            _files = files;
            _settings = settings;

            Languages = new ObservableCollection<LanguageDefinition>(languages.All);
            _selectedLanguage = Languages[0];
            _code = _selectedLanguage.NewFileTemplate;
            _portName = settings.Settings.IdfSerialPort;   // même port que Flash/Monitor par défaut

            EditorDocument.Text = _code;
            EditorDocument.TextChanged += (_, _) =>
            {
                if (Code != EditorDocument.Text) Code = EditorDocument.Text;
            };
        }

        partial void OnLastCheckResultChanged(CodeCheckResult? value) => OnPropertyChanged(nameof(StatusColor));

        // Sens Code -> éditeur : NewFile()/OpenFileAsync()/changement de langage modifient Code
        // directement (pas par la frappe de l'utilisateur) -- il faut répercuter dans EditorDocument.
        partial void OnCodeChanged(string value)
        {
            if (EditorDocument.Text != value) EditorDocument.Text = value;
        }

        partial void OnSelectedLanguageChanged(LanguageDefinition value)
        {
            // Change juste la grammaire proposée pour un NOUVEAU fichier ; un fichier déjà ouvert garde
            // son contenu (l'utilisateur peut vouloir forcer la coloration d'un fichier à l'extension
            // inhabituelle sans perdre ce qu'il a tapé).
            if (FilePath is null && string.IsNullOrWhiteSpace(Code))
                Code = value.NewFileTemplate;
        }

        [RelayCommand]
        public void NewFile()
        {
            FilePath = null;
            Code = SelectedLanguage.NewFileTemplate;
            DevicePath = string.Empty;
            Status = "Nouveau fichier.";
            OnPropertyChanged(nameof(Title));
        }

        [RelayCommand]
        public async Task OpenFileAsync()
        {
            var filters = new[]
            {
                new FileFilter("Fichiers de code", new[] { "lua", "py" }),
                new FileFilter("Tous les fichiers", new[] { "*" }),
            };
            var path = await _files.OpenFileAsync("Ouvrir un fichier de code", filters).ConfigureAwait(true);
            if (path is null) return;

            Code = await File.ReadAllTextAsync(path).ConfigureAwait(true);
            FilePath = path;
            var detected = _languages.FromFilePath(path);
            if (detected is not null) SelectedLanguage = detected;
            DevicePath = SuggestDevicePath(path);
            Status = $"Ouvert : {path}";
            OnPropertyChanged(nameof(Title));
        }

        [RelayCommand]
        public async Task SaveFileAsync()
        {
            var path = FilePath;
            if (path is null)
            {
                var filters = new[] { new FileFilter(SelectedLanguage.DisplayName, new[] { SelectedLanguage.FileExtension }) };
                path = await _files.SaveFileAsync("Enregistrer le fichier de code",
                    $"nouveau.{SelectedLanguage.FileExtension}", filters).ConfigureAwait(true);
                if (path is null) return;
            }

            await File.WriteAllTextAsync(path, Code).ConfigureAwait(true);
            FilePath = path;
            if (string.IsNullOrWhiteSpace(DevicePath)) DevicePath = SuggestDevicePath(path);
            Status = $"Enregistré : {path}";
            OnPropertyChanged(nameof(Title));
        }

        [RelayCommand]
        public void CheckCode()
        {
            LastCheckResult = _checker.Check(Code, SelectedLanguage.Language);
            Status = LastCheckResult.Ok
                ? "Vérification : aucun problème structurel trouvé."
                : $"Ligne {LastCheckResult.Line}, colonne {LastCheckResult.Column} : {LastCheckResult.Message}";
        }

        [RelayCommand]
        public async Task SendToDeviceAsync()
        {
            if (string.IsNullOrWhiteSpace(PortName))
            {
                Status = "Indiquez un port série (le même que Flash/Monitor, ex. /dev/ttyUSB0 ou COM5).";
                return;
            }
            if (string.IsNullOrWhiteSpace(DevicePath))
            {
                Status = "Indiquez le chemin sur l'appareil (ex. cassebriques/main.lua).";
                return;
            }

            // Vérification structurelle AVANT l'envoi (voir CodeCheckService pour ce qu'elle couvre et
            // ne couvre pas) : un contrôle heuristique, donc un premier échec avertit sans bloquer pour
            // de bon -- cliquer une seconde fois sur "Envoyer" avec le MÊME code envoie quand même
            // (l'utilisateur sait ce qu'il fait ; peut-être un faux positif du vérificateur).
            LastCheckResult = _checker.Check(Code, SelectedLanguage.Language);
            if (!LastCheckResult.Ok && Code != _lastRejectedCode)
            {
                _lastRejectedCode = Code;
                Status = $"⚠ Ligne {LastCheckResult.Line}, colonne {LastCheckResult.Column} : " +
                         $"{LastCheckResult.Message} — cliquez de nouveau sur Envoyer pour l'envoyer quand même.";
                return;
            }
            _lastRejectedCode = null;

            // On envoie toujours le contenu ACTUEL de l'éditeur : un enregistrement local d'abord
            // garantit que le fichier envoyé et le fichier sur disque restent identiques.
            await SaveFileAsync().ConfigureAwait(true);
            if (FilePath is null) return;   // l'utilisateur a annulé l'enregistrement

            IsBusy = true;
            Status = "Envoi en cours...";
            try
            {
                var result = await _transfer.PutFileAsync(PortName, SelectedLanguage.Language, DevicePath,
                    FilePath).ConfigureAwait(true);
                Status = result.Status switch
                {
                    TransferStatus.Ok => $"Envoyé : {DevicePath}",
                    TransferStatus.OkRenamed =>
                        $"Envoyé : le joueur a choisi de renommer -> {result.FinalDevicePath}",
                    TransferStatus.Cancelled =>
                        "Annulé sur l'appareil (le joueur a refusé d'écraser le fichier existant).",
                    TransferStatus.NoResponse =>
                        "Pas de réponse de l'appareil — vérifiez le port, le câble, et que la console est allumée " +
                        "(ou attendez : si l'appareil montre l'écran \"Recevoir un code\", le joueur a peut-être " +
                        "jusqu'à 30 s pour répondre à un conflit de fichier).",
                    TransferStatus.BadLanguage => "Ce firmware ne gère pas ce langage.",
                    TransferStatus.BadPath => "Chemin sur l'appareil invalide.",
                    TransferStatus.CrcMismatch => "Erreur de transmission (somme de contrôle) — réessayez.",
                    TransferStatus.WriteError => "Erreur d'écriture sur l'appareil (carte SD absente ou pleine ?).",
                    TransferStatus.TooLarge => "Fichier trop volumineux (limite : 512 Ko).",
                    _ => $"Échec ({result.Status}).",
                };
            }
            catch (Exception ex)
            {
                Status = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string SuggestDevicePath(string localPath) => Path.GetFileName(localPath);
    }
}
