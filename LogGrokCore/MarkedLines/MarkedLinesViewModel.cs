using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Windows.Data;

namespace LogGrokCore.MarkedLines
{
    public class MarkedLinesViewModel : ViewModelBase
    {
        private readonly ObservableCollection<MarkedLineViewModel> _markedLines = new();
        private readonly ObservableCollection<DocumentViewModel> _documents;
        private readonly HashSet<DocumentViewModel> _alreadySubscribed = new();
        
        public ObservableCollection<MarkedLineViewModel> MarkedLines => _markedLines;

        public DelegateCommand CopyLinesCommand { get; }

        public DelegateCommand ItemActivatedCommand { get; }

        public bool HaveMarkedLines => _markedLines.Count != 0;

        // The shared AvalonDock LayoutItem style (intended for document panes) binds
        // Model.Content.Title / .IsCurrentDocument; expose them here so those bindings
        // resolve for the Marked Lines anchorable instead of logging path errors.
        public string Title => Localization.TranslationSource.Instance["Marked_Title"];

        public bool IsCurrentDocument => false;

        public IEnumerable? SelectedItems
        {
            get;
            set;
        }

        public MarkedLinesViewModel(ObservableCollection<DocumentViewModel> documents)
        {
            _documents = documents;

            // Title is read from resources, so refresh it when the UI language changes.
            Localization.TranslationSource.Instance.PropertyChanged +=
                (_, _) => InvokePropertyChanged(nameof(Title));

            SubscribeToNewDocumentChanges(_documents);

            _documents.CollectionChanged += (_, _) =>
            {
                SubscribeToNewDocumentChanges(_documents);
                UpdateLinesCollection();
            };

            CopyLinesCommand = new DelegateCommand(
                o =>
                {
                    var document = (DocumentViewModel) o;
                    var linesToCopy =
                        $"{document.Title}:".Yield().Concat(
                        MarkedLines.Where(m => m.Document == document)
                            .OrderBy(m => m.Index)
                            .Select(m => m.ToString().Trim('\0').TrimEnd()));

                    TextCopy.ClipboardService.SetText(string.Join("\r\n", linesToCopy));
                });

            ItemActivatedCommand = new DelegateCommand(
                o =>
                {
                    if (o is not MarkedLineViewModel item) return;
                    NavigationRequested?.Invoke(item.Document, item.Index);
                });
            
            var view = (CollectionView)CollectionViewSource.GetDefaultView(MarkedLines);
            var groupDescription = new PropertyGroupDescription("Document");
            view.GroupDescriptions?.Add(groupDescription);
            UpdateLinesCollection();
        }
        
        public event Action<DocumentViewModel, int>? NavigationRequested;

        private void SubscribeToNewDocumentChanges(ObservableCollection<DocumentViewModel> documents)
        {
            var newDocuments = new HashSet<DocumentViewModel>(documents);
            _alreadySubscribed.RemoveWhere(d => !newDocuments.Contains(d));
            foreach (var documentViewModel in documents)
            {
                if (_alreadySubscribed.Contains(documentViewModel)) continue;
                // MarkedLinesChanged is the reliable signal; the previous CollectionChanged hookup
                // attached to a throwaway collection instance and forced a disk read on subscribe.
                documentViewModel.MarkedLinesChanged += UpdateLinesCollection;
                _alreadySubscribed.Add(documentViewModel);
            }
        }

        private void UpdateLinesCollection()
        {
            var allDocuments = new HashSet<DocumentViewModel>(_documents);
            for (var i = _markedLines.Count - 1; i >= 0; i--)
            {
                if (!allDocuments.Contains(_markedLines[i].Document))
                    _markedLines.RemoveAt(i);
            }

            var index = 0;
            
            foreach (var (document, (lineNumber, text)) in _documents.SelectMany(
                document => document.MarkedLineViewModels.Select(line => (document, line))))
            {
                if (_markedLines.Count <= index || _markedLines[index].Document != document)
                {
                    _markedLines.Insert(index, new MarkedLineViewModel(document, lineNumber, text));
                    index++;
                    continue;
                }

                // Bound the index access: removals can drain the slot, and the original
                // unguarded _markedLines[index] would then throw IndexOutOfRange.
                while (index < _markedLines.Count && _markedLines[index].Index < lineNumber)
                {
                    _markedLines.RemoveAt(index);
                }

                if (index >= _markedLines.Count || _markedLines[index].Index > lineNumber)
                {
                    _markedLines.Insert(index, new MarkedLineViewModel(document, lineNumber, text));
                }

                index++;
            }

            while (_markedLines.Count > index)
            {
                _markedLines.RemoveAt(index);
            }
            
            InvokePropertyChanged(nameof(HaveMarkedLines));
        }
    }
}