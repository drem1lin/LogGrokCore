using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace LogGrokCore.Data.Index
{
   public class CountIndex<TIndex> where TIndex : IIndex<int>
    {
        public const int Granularity = 16384;
        private ImmutableList<List<(IndexKeyNum, int)>> _counts = ImmutableList<List<(IndexKeyNum, int)>>.Empty;
        private readonly IDictionary<IndexKeyNum,TIndex> _indices;
        private volatile bool _isFinished;
        private int _lastIndex = -1;

        private sealed class LiveSnapshot
        {
            public readonly int Index;
            public readonly IReadOnlyList<List<(IndexKeyNum, int)>> Counts;

            public LiveSnapshot(int index, IReadOnlyList<List<(IndexKeyNum, int)>> counts)
            {
                Index = index;
                Counts = counts;
            }
        }

        private LiveSnapshot? _liveSnapshot;

        public IReadOnlyList<List<(IndexKeyNum, int)>> Counts
        {
            get
            {
                if (_isFinished)
                    return _counts;

                // The UI dereferences this several times per fetch; rebuilding the full O(keys)
                // per-key snapshot every time was pure allocation churn. Reuse the previous snapshot
                // while no new line has been indexed (per-key counts only change on Add, which bumps
                // _lastIndex; a new Granularity checkpoint also bumps it, so the cache is never
                // reused across a change to _counts either).
                var index = Volatile.Read(ref _lastIndex);
                var cached = Volatile.Read(ref _liveSnapshot);
                if (cached != null && cached.Index == index)
                    return cached.Counts;

                var counts = _counts.Add(MakeCountsSnapshot());
                if (_isFinished)
                    return _counts;

                Volatile.Write(ref _liveSnapshot, new LiveSnapshot(index, counts));
                return counts;
            }
        }

        public CountIndex(IDictionary<IndexKeyNum, TIndex> indices)
        {
            _indices = indices;
        }

        public void Add(int currentIndex, IDictionary<IndexKeyNum, TIndex> indices)
        {
            // Append the Granularity checkpoint BEFORE publishing _lastIndex: a reader that observes
            // the new _lastIndex (acquire) then also observes the new checkpoint in _counts, and the
            // snapshot cache (keyed by _lastIndex) is correctly invalidated across the checkpoint.
            if (currentIndex % Granularity == 0 && currentIndex != 0)
                UpdateCountsSnapshot();
            Volatile.Write(ref _lastIndex, currentIndex);
        }

        public void Finish(IDictionary<IndexKeyNum, TIndex> indices)
        {
            UpdateCountsSnapshot();
            _isFinished = true;
        }

        private void UpdateCountsSnapshot()
        {
            _counts = _counts.Add(MakeCountsSnapshot());
        }

        private List<(IndexKeyNum, int)> MakeCountsSnapshot()
        {
            var snapshotList = new List<(IndexKeyNum, int)>(_indices.Count);


#pragma warning disable CS8619
            foreach (var (key, value) in _indices)
#pragma warning restore CS8619
            {
                snapshotList.Add((key, value.Count));
            }

            return snapshotList;
        }
    }
}
