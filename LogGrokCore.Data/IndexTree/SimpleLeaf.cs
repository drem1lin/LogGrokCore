using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace LogGrokCore.Data.IndexTree
{
    // The list is appended on the loader thread while the UI reads it. The backing list never
    // reallocates (capacity is fixed at LeafCapacity; a full leaf spills into Next), so the only
    // cross-thread hazard is visibility: a reader must not observe the count before the element it
    // refers to. _count is published with release semantics after the element is written and read
    // with acquire semantics, which establishes that happens-before relationship without a lock.
    public sealed class SimpleLeaf<T> :
        LeafOrNode<T, SimpleLeaf<T>>,
        ILeaf<T, SimpleLeaf<T>>, ITreeNode<T>
    {
        private readonly List<T> _storage;
        private readonly int _firstValueIndex;
        private int _count;
        private SimpleLeaf<T>? _next;
        private const int LeafCapacity = 1024;

        public SimpleLeaf(T firstValue, int valueIndex)
        {
            _storage = new List<T>(LeafCapacity) {firstValue};
            _firstValueIndex = valueIndex;
            Volatile.Write(ref _count, 1);
        }

        public SimpleLeaf<T>? Add(T value, int valueIndex)
        {
            if (_storage.Count < LeafCapacity)
            {
                _storage.Add(value);
                Volatile.Write(ref _count, _storage.Count); // publish the element before the count
                return null;
            }

            var next = new SimpleLeaf<T>(value, valueIndex);
            Volatile.Write(ref _next, next); // fully constructed leaf published before it is linked
            return next;
        }

        public override T FirstValue => _storage[0];

        public override int MinIndex => _firstValueIndex;

        public T this[int index] => _storage[index];

        public int Count => Volatile.Read(ref _count);

        public override IEnumerable<T> GetEnumerableFromIndex(int index)
        {
            return this.GetEnumerableFromIndex<T, SimpleLeaf<T>>(index);
        }
        
        public override T GetValue(int index)
        {
            return this.GetValue<T, SimpleLeaf<T>>(index);
        }

        public override (int index, SimpleLeaf<T> leaf) FindByValue(T value)
        {
            var count = Volatile.Read(ref _count);
            var index = _storage.BinarySearch(0, count, value, null);
            return ((index >= 0 ? index : ~index) + _firstValueIndex, this);
        }

        public SimpleLeaf<T>? Next => Volatile.Read(ref _next);

        public IEnumerator<T> GetEnumerator()
        {
            return _storage.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}