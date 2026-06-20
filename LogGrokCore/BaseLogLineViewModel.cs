using System;
using LogGrokCore.Controls;
using LogGrokCore.MarkedLines;

namespace LogGrokCore
{
    public abstract class BaseLogLineViewModel : ItemViewModel, ILineMark, IDisposable
    {
        private readonly Selection _markedLines;
        private readonly Action _onMarkedLinesChanged;

        protected BaseLogLineViewModel(int index, Selection markedLines)
        {
            Index = index;
            IndexViewModel = new LinePartViewModel(HashCode.Combine(-1, index), Index.ToString());
            _markedLines = markedLines;
            // Keep a handle to the subscription so it can be removed when this view model is
            // evicted; otherwise the per-document Selection singleton accumulates handlers
            // for every line ever realized while scrolling (an unbounded leak).
            _onMarkedLinesChanged = () => InvokePropertyChanged(nameof(IsMarked));
            _markedLines.Changed += _onMarkedLinesChanged;
        }

        public void Dispose() => _markedLines.Changed -= _onMarkedLinesChanged;

        public int Index { get; }

        public LinePartViewModel IndexViewModel { get; }
        
        public bool IsMarked
        {
            get => _markedLines.Contains(Index);
            set
            {
                if (value)
                    _markedLines.Add(Index);
                else 
                    _markedLines.Remove(Index);
            }
        }
    }
}