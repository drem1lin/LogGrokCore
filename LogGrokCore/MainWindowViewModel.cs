using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Windows;
using System.Windows.Input;
using LogGrokCore.AvalonDockExtensions;
using LogGrokCore.Data;
using LogGrokCore.Diagnostics;
using LogGrokCore.Localization;
using LogGrokCore.MarkedLines;
using LogGrokCore.Search;
using Microsoft.Win32;

namespace LogGrokCore
{
    public class MainWindowViewModel : ViewModelBase, IContentProvider, IDisposable
    {
        private DocumentViewModel? _currentDocument;
        private readonly ApplicationSettings _applicationSettings;
        private readonly SearchAutocompleteCache _searchAutocompleteCache;
        private readonly Dictionary<DocumentViewModel, DocumentContainer> _containers = new();
        private bool _isDebugLoggingEnabled;
        private ProfileSettings _selectedProfile;
        private bool _disposed;

        public ObservableCollection<DocumentViewModel> Documents { get; }

        public MarkedLinesViewModel MarkedLinesViewModel { get; }
        
        public ICommand OpenFileCommand => new DelegateCommand(OpenFile);

        public ICommand OnDocumentCloseCommand => DelegateCommand.Create((DocumentViewModel document) =>
        {
            document.CloseFile();
        });

        public ICommand DropCommand => new DelegateCommand(
            obj=> OpenFiles((IEnumerable<string>)obj), 
            o => o is IEnumerable<string>);

        public MainWindowViewModel(ApplicationSettings applicationSettings, 
            SearchAutocompleteCache searchAutocompleteCache, 
            Func<ObservableCollection<DocumentViewModel>, MarkedLinesViewModel> markedLinesViewModelFactory)
        {
            _applicationSettings = applicationSettings;
            _searchAutocompleteCache = searchAutocompleteCache;
            Documents = new ObservableCollection<DocumentViewModel>();
            Documents.CollectionChanged += OnDocumentsChanged;
            MarkedLinesViewModel = markedLinesViewModelFactory(Documents);
            Profiles = new ObservableCollection<ProfileSettings>(_applicationSettings.GetProfiles());
            _selectedProfile = ResolveSelectedProfile();
            _applicationSettings.ProfilesChanged += OnProfilesChanged;
            OpenSettings = new DelegateCommand(() =>
            {
                OpenExternalFile(ApplicationSettings.SettingsFileName);
            });

            Languages = BuildLanguageMenu(_applicationSettings.Language);

            // Reflect the persisted state and apply verbose logging if it was left enabled.
            _isDebugLoggingEnabled = _applicationSettings.DebugSettings.EnableCrashDumps;
            DebugLogging.SetVerbose(_isDebugLoggingEnabled);

            MarkedLinesViewModel.NavigationRequested += (document, index) =>
            {
                CurrentDocument = document;
                document.NavigateTo(index);
            };
        }

        private static void OpenExternalFile(string fileName)
        {
            void StartProcess(string verb)
            {
                using var process = new Process
                {
                    StartInfo =
                    {
                        FileName = fileName,
                        UseShellExecute = true,
                        Verb = verb
                    }
                };
                process.Start();
            }

            try
            {
                StartProcess(string.Empty);
            }
            catch (Win32Exception e)
            {
                if (e.NativeErrorCode == 1155) // 'No application is associated with the specified file for this operation.'
                {
                    StartProcess("openas");
                }
                else throw;
            }
        }

        public DocumentViewModel? CurrentDocument
        {
            get => _currentDocument;
            set
            {
                if (_currentDocument == value) return;
                
                if (_currentDocument != null)
                    _currentDocument.IsCurrentDocument = false;
                
                _currentDocument = value;
                
                if (_currentDocument != null)
                    _currentDocument.IsCurrentDocument = true;
                
                InvokePropertyChanged();
            }
        }

        public ICommand OpenSettings { get; }

        public ObservableCollection<ProfileSettings> Profiles { get; }

        /// <summary>
        /// The active parser/highlight profile. Changing it rebuilds every open document so its
        /// parser, indexes and table columns all come from the same profile.
        /// </summary>
        public ProfileSettings SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (value == null || string.Equals(_selectedProfile.Name, value.Name,
                        StringComparison.OrdinalIgnoreCase))
                    return;

                if (!TryReplaceDocumentsForProfile(value))
                {
                    InvokePropertyChanged();
                    return;
                }

                _selectedProfile = value;
                InvokePropertyChanged();
                ApplicationSettings.SetSelectedProfile(value.Name);
            }
        }

        private ProfileSettings ResolveSelectedProfile()
        {
            var configured = _applicationSettings.GetSelectedProfile();
            foreach (var profile in Profiles)
            {
                if (string.Equals(profile.Name, configured.Name, StringComparison.OrdinalIgnoreCase))
                    return profile;
            }

            return Profiles[0];
        }

        private void OnProfilesChanged()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                _ = dispatcher.BeginInvoke(OnProfilesChanged);
                return;
            }

            if (_disposed)
                return;

            var currentName = _selectedProfile.Name;
            var refreshedProfiles = _applicationSettings.GetProfiles();
            Profiles.Clear();
            foreach (var profile in refreshedProfiles)
                Profiles.Add(profile);

            var refreshedSelection = Profiles.FirstOrDefault(profile =>
                                         string.Equals(profile.Name, currentName,
                                             StringComparison.OrdinalIgnoreCase));
            if (refreshedSelection != null)
            {
                _selectedProfile = refreshedSelection;
                InvokePropertyChanged(nameof(SelectedProfile));
                return;
            }

            // The active profile was removed from the file. Apply the configured fallback so the
            // combo box and every open table still describe the same parser configuration.
            var fallback = ResolveSelectedProfile();
            if (TryReplaceDocumentsForProfile(fallback))
                _selectedProfile = fallback;
            InvokePropertyChanged(nameof(SelectedProfile));
        }

        private bool TryReplaceDocumentsForProfile(ProfileSettings profile)
        {
            if (Documents.Count == 0)
                return true;

            var oldDocuments = Documents.ToArray();
            var currentIndex = CurrentDocument == null ? -1 : Array.IndexOf(oldDocuments, CurrentDocument);
            var replacements = new List<(DocumentViewModel viewModel, DocumentContainer container)>();

            try
            {
                foreach (var oldDocument in oldDocuments)
                {
                    var replacement = CreateDocumentResources(oldDocument.DocumentId, profile);
                    foreach (var markedLine in oldDocument.MarkedLines)
                        replacement.viewModel.MarkedLines.Add(markedLine);
                    replacements.Add(replacement);
                }
            }
            catch (Exception ex)
            {
                foreach (var replacement in replacements)
                    replacement.container.Dispose();

                Trace.TraceError($"Failed to apply profile '{profile.Name}': {ex}");
                var source = TranslationSource.Instance;
                MessageBox.Show(
                    string.Format(source["Profile_ChangeErrorMessage"], profile.Name, ex.Message),
                    source["Profile_ChangeErrorTitle"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            for (var i = 0; i < replacements.Count; i++)
            {
                var replacement = replacements[i];
                _containers[replacement.viewModel] = replacement.container;
                Documents[i] = replacement.viewModel;
            }

            CurrentDocument = currentIndex >= 0 ? replacements[currentIndex].viewModel : null;
            return true;
        }

        public ICommand OpenAbout => new DelegateCommand(() =>
        {
            var about = new AboutWindow { Owner = Application.Current.MainWindow };
            about.ShowDialog();
        });

        /// <summary>Entries for the Language submenu, with live "current language" checkmarks.</summary>
        public ObservableCollection<LanguageViewModel> Languages { get; }

        private ObservableCollection<LanguageViewModel> BuildLanguageMenu(string currentCode)
        {
            var languages = new ObservableCollection<LanguageViewModel>();
            var selectCommand = DelegateCommand.Create<string>(SelectLanguage);
            foreach (var info in TranslationSource.AvailableLanguages)
            {
                languages.Add(new LanguageViewModel(info, selectCommand)
                {
                    IsSelected = string.Equals(info.Code, currentCode,
                        StringComparison.OrdinalIgnoreCase)
                });
            }

            return languages;
        }

        private void SelectLanguage(string code)
        {
            var appliedCode = TranslationSource.Instance.SetCulture(code);

            foreach (var language in Languages)
                language.IsSelected =
                    string.Equals(language.Code, appliedCode, StringComparison.OrdinalIgnoreCase);

            ApplicationSettings.SetLanguage(appliedCode);
        }

        public ICommand ExitCommand => new DelegateCommand(() => Application.Current.Shutdown());

        /// <summary>
        /// When enabled, raises log verbosity to Trace and configures Windows Error
        /// Reporting to capture full crash dumps under %PROGRAMDATA%\LogGrok2\Data\Dumps.
        /// Bound two-way to a checkable menu item; the work happens in the setter.
        /// </summary>
        public bool IsDebugLoggingEnabled
        {
            get => _isDebugLoggingEnabled;
            set
            {
                if (_isDebugLoggingEnabled == value)
                    return;

                if (TryApplyDebugLogging(value))
                    _isDebugLoggingEnabled = value;

                // Re-raise either way: on success to confirm, on failure to revert the menu check.
                InvokePropertyChanged();
            }
        }

        private bool TryApplyDebugLogging(bool enable)
        {
            // Crash-dump (WER) registration needs elevation; ask first so a declined UAC
            // prompt leaves nothing half-applied.
            if (!CrashDumpConfiguration.RequestConfigureElevated(enable))
            {
                Trace.TraceWarning("Debug logging toggle cancelled: crash dump configuration was not applied.");
                return false;
            }

            DebugLogging.SetVerbose(enable);
            ApplicationSettings.SetEnableCrashDumps(enable);
            Logger.Get().Info("Debug logging and crash dumps {0}.", enable ? "enabled" : "disabled");
            return true;
        }

        private void OpenFile()
        {
            var dialog = new OpenFileDialog
            {
                DefaultExt = "log",
                Filter = "All Files|*.*|Log files(*.log)|*.log|Text files(*.txt)|*.txt",
                Multiselect = true
            };

            var dialogResult = dialog.ShowDialog();
            if (dialogResult.GetValueOrDefault())
            {
                foreach (var fileName in dialog.FileNames)
                {
                    Trace.TraceInformation($"Open document {fileName}.");
                    AddDocument(fileName);
                }
            }
            
            ShowScratchPad?.Invoke(this, new EventArgs());
        }
        
        private void OpenFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                AddDocument(file);
            }
        }

        public void AddDocument(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Trace.TraceError($"File {fileName} is not exists");
                return;
            }

            // If the file is already open, activate its tab instead of opening a duplicate.
            var alreadyOpen = FindOpenDocument(fileName);
            if (alreadyOpen != null)
            {
                CurrentDocument = alreadyOpen;
                return;
            }

            try
            {
                CurrentDocument = CreateDocument(fileName);
            }
            catch (Exception ex)
            {
                // Opening can fail for reasons outside our control (access denied, locked,
                // unreadable). Surface it instead of letting it crash the application.
                Trace.TraceError($"Failed to open '{fileName}': {ex}");
                var source = Localization.TranslationSource.Instance;
                MessageBox.Show(
                    string.Format(source["OpenFile_ErrorMessage"], fileName, ex.Message),
                    source["OpenFile_ErrorTitle"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Returns the already-open document for <paramref name="fileName"/>, comparing by full
        /// path (case-insensitively, as Windows paths are), or null if it isn't open.
        /// </summary>
        private DocumentViewModel? FindOpenDocument(string fileName)
        {
            var target = TryGetFullPath(fileName);
            foreach (var document in Documents)
            {
                if (string.Equals(TryGetFullPath(document.DocumentId), target,
                        StringComparison.OrdinalIgnoreCase))
                    return document;
            }

            return null;
        }

        private static string TryGetFullPath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private DocumentViewModel CreateDocument(string fileName)
        {
            var (viewModel, container) = CreateDocumentResources(fileName, _selectedProfile);
            _containers[viewModel] = container;
            Documents.Add(viewModel);
            return viewModel;
        }

        private (DocumentViewModel viewModel, DocumentContainer container) CreateDocumentResources(
            string fileName, ProfileSettings profile)
        {
            var container = new DocumentContainer(fileName, profile, _applicationSettings,
                _searchAutocompleteCache);
            try
            {
                return (container.GetDocumentViewModel(), container);
            }
            catch
            {
                container.Dispose();
                throw;
            }
        }

        // Dispose a document's container (which disposes its view models and file handle) when the
        // document is removed. Subscribed once, so handlers don't accumulate per opened document.
        private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var container in _containers.Values)
                    container.Dispose();
                _containers.Clear();
                return;
            }

            if (e.OldItems == null)
                return;

            foreach (DocumentViewModel viewModel in e.OldItems)
            {
                if (_containers.Remove(viewModel, out var container))
                    container.Dispose();
            }
        }

        public event EventHandler? ShowScratchPad;
        
        public object? GetContent(string contentId)
        {
            return contentId == Constants.MarkedLinesContentId ? MarkedLinesViewModel : null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _applicationSettings.ProfilesChanged -= OnProfilesChanged;
            Documents.Clear();
            MarkedLinesViewModel.Dispose();
        }
    }
}
