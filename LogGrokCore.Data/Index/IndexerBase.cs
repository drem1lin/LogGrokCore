using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LogGrokCore.Data.IndexTree;

namespace LogGrokCore.Data.Index;

public abstract class IndexerBase : IDisposable
{
    protected readonly ConcurrentDictionary<IndexKeyNum, IndexTree<int, SimpleLeaf<int>>> Indices =
        new(1, 16384);

    protected readonly CountIndex<IndexTree<int, SimpleLeaf<int>>> CountIndex;

    protected readonly ConcurrentDictionary<IndexKey, IndexKeyNum> KeysToNumbers;
    protected readonly ConcurrentDictionary<IndexKeyNum, IndexKey> NumbersToKeys;

    public IndexerBase(ConcurrentDictionary<IndexKey, IndexKeyNum> keysToNumbers,
        ConcurrentDictionary<IndexKeyNum, IndexKey> numbersToKeys)
    {
        KeysToNumbers = keysToNumbers;
        NumbersToKeys = numbersToKeys;
        
        CountIndex = new CountIndex<IndexTree<int, SimpleLeaf<int>>>(Indices);
    }

    public IIndexedLinesProvider GetIndexedLinesProvider(
        IReadOnlyDictionary<int, IEnumerable<string>> excludedComponents)
    {
        var updatableCounts =
            UpdatableValue.Create(() => CountIndex.Counts);
        
        return new IndexedLinesProvider(this, updatableCounts,
            CountIndex<IndexTree<int, SimpleLeaf<int>>>.Granularity, excludedComponents, NumbersToKeys);
    }

    public IndexTree<int, SimpleLeaf<int>> GetIndex(IndexKeyNum key) => Indices[key];
    
    
    // The set of keys matching a component value only changes when new keys are added (the index is
    // append-only), so cache the matching keys and rescan only when the key count grows. The per-key
    // line counts are always read live, so counts stay correct while a file is still loading.
    private readonly ConcurrentDictionary<(int componentIndex, string value), (int keyCountSnapshot, IndexKeyNum[] keys)>
        _componentKeyCache = new();

    public int GetIndexCountForComponent(int componentIndex, string componentValue)
    {
        var currentKeyCount = Indices.Count;
        var cacheKey = (componentIndex, componentValue);

        if (!_componentKeyCache.TryGetValue(cacheKey, out var cached) || cached.keyCountSnapshot != currentKeyCount)
        {
            var matching = Indices.Keys
                .Where(key =>
                    NumbersToKeys[key].GetComponent(componentIndex).SequenceEqual(componentValue.AsSpan()))
                .ToArray();
            cached = (currentKeyCount, matching);
            _componentKeyCache[cacheKey] = cached;
        }

        var sum = 0;
        foreach (var key in cached.keys)
        {
            if (Indices.TryGetValue(key, out var index))
                sum += index.Count;
        }

        return sum;
    }

    private protected static IndexTree<int, SimpleLeaf<int>> CreateIndexTree()
    {
        return new IndexTree<int, SimpleLeaf<int>>(16,
            static value => new SimpleLeaf<int>(value, 0));
    }

    public void Dispose()
    {
        Indices.Clear();
        _componentKeyCache.Clear();
    }

    public void Finish()
    {
        CountIndex.Finish(Indices);
    }
}