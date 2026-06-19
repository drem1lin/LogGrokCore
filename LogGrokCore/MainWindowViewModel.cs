using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Windows;
using System.Windows.Input;
using LogGrokCore.AvalonDockExtensions;
using LogGrokCore.Data;
using LogGrokCore.Diagnostics;
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
        private bool _isDebugLoggingEnabled;

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
            MarkedLinesViewModel = markedLinesViewModelFactory(Documents);
            OpenSettings = new DelegateCommand(() =>
            {
                OpenExternalFile(ApplicationSettings.SettingsFileName);
            });

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
            if (File.Exists(fileName))
                CurrentDocument = CreateDocument(fileName);
            else
                Trace.TraceError($"File {fileName} is not exists");
        }

        private DocumentViewModel CreateDocument(string fileName)
        {
            var container = new DocumentContainer(fileName, _applicationSettings, _searchAutocompleteCache);
            var viewModel = container.GetDocumentViewModel();
            Documents.Add(viewModel);
            Documents.CollectionChanged += (o, e) =>
            {
                if (Documents.Contains(viewModel))
                    return;
                container.Dispose();
            };
            return viewModel;
        }

        public event EventHandler? ShowScratchPad;
        
        public object? GetContent(string contentId)
        {
            return contentId == Constants.MarkedLinesContentId ? MarkedLinesViewModel : null;
        }

        public void Dispose()
        {
            Documents.Clear();
        }
    }
}
