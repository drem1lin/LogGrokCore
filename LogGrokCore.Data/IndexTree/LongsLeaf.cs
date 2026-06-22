using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace LogGrokCore.Data.IndexTree
{
    // See SimpleLeaf for the concurrency model: the fixed-capacity list is appended by the loader
    // while the UI reads it; _count is published/consumed with release/acquire semantics so a
    // reader never observes the count ahead of the element it points at. (LineIndex also serialises
    // access with a lock, but the leaf is made memory-safe on its own.)
    public sealed class LongsLeaf
        : LeafOrNode<long, LongsLeaf>,
            ILeaf<long, LongsLeaf>
    {
        private const int Capacity = 64*1024;
        private readonly long _firstValue;
        private readonly int _firstIndex;
        private readonly List<int> _storage;
        private int _count;
        private LongsLeaf? _next;

        public LongsLeaf(long firstValue, int valueIndex)
        {
            _storage = new List<int>(Capacity) {0};
            _firstIndex = valueIndex;
            _firstValue = firstValue;
            Volatile.Write(ref _count, 1);
        }

        public LongsLeaf? Add(long value, int valueIndex)
        {
            if (_storage.Count < Capacity)
            {
                _storage.Add((int)(value - _firstValue));
                Volatile.Write(ref _count, _storage.Count); // publish the element before the count
                return null;
            }

            var next = new LongsLeaf(value, valueIndex);
            Volatile.Write(ref _next, next); // fully constructed leaf published before it is linked
            return next;
        }

        public long this[int index] => _firstValue +_storage[index];

        public int Count => Volatile.Read(ref _count);
        public LongsLeaf? Next => Volatile.Read(ref _next);
        
        public override long FirstValue => _firstValue;
        public override int MinIndex => _firstIndex;
        
        public override IEnumerable<long> GetEnumerableFromIndex(int index)
        {
            return this.GetEnumerableFromIndex<long, LongsLeaf>(index);
        }

        public override long GetValue(int index)
        {
            return this.GetValue<long, LongsLeaf>(index);
        }

        public override (int index, LongsLeaf leaf) FindByValue(long value)
        {
            var count = Volatile.Read(ref _count);
            var index = _storage.BinarySearch(0, count, (int) (value - _firstIndex), null);
            return (_firstIndex + (index >= 0 ? index : ~index), this);
        }

        public IEnumerator<long> GetEnumerator()
        {
            foreach (var value in _storage)
            {
                yield return _firstIndex + value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}