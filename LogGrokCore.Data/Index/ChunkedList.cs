using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LogGrokCore.Data.Index;

// Appended by the loader thread while read cross-thread (e.g. the search pipeline reads it via
// Indexer.GetIndexKeyNum during loading). _chunks is a growing List, so _count is published with
// release semantics after the element/new chunk is in place and read with acquire before indexing,
// establishing happens-before without a lock (same model as the IndexTree leaves).
public class ChunkedList<T> : IList<T>
{
    private readonly List<T[]> _chunks = new();
    private readonly int _chunkSize;
    private int _count;

    public ChunkedList(int chunkSize)
    {
        _chunkSize = chunkSize;
    }

    private IEnumerable<T> GetEnumerableFrom(int index)
    {
        var count = Volatile.Read(ref _count);
        if (index >= count)
            throw new IndexOutOfRangeException();

        var chunkNum = index / _chunkSize;
        var from = index % _chunkSize;

        while (index < count)
        {
            var currentChunk = _chunks[chunkNum];
            var to = Math.Min(count - chunkNum * _chunkSize, _chunkSize);

            for (var idx = from; idx < to; idx++)
                yield return currentChunk[idx];

            index += to - from;
            chunkNum++;
            from = 0;
        }

    }

    public IEnumerator<T> GetEnumerator() => GetEnumerableFrom(0).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(T item)
    {
        var idx = _count % _chunkSize;
        if (_count % _chunkSize == 0)
            _chunks.Add(new T[_chunkSize]);

        var lastChunk = _chunks[^1];
        lastChunk[idx] = item;
        Volatile.Write(ref _count, _count + 1); // publish element (and any new chunk) before count
    }

    public void Clear()
    {
        _chunks.Clear();
        Volatile.Write(ref _count, 0);
    }

    public bool Contains(T item) => throw new NotSupportedException();

    public void CopyTo(T[] array, int arrayIndex) => throw new NotSupportedException();

    public bool Remove(T item) => throw new NotSupportedException();

    public int Count => Volatile.Read(ref _count);

    public bool IsReadOnly => false;

    public int IndexOf(T item) => throw new NotSupportedException();

    public void Insert(int index, T item) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();

    public T this[int index]
    {
        get
        {
            if (index >= Volatile.Read(ref _count))
                throw new IndexOutOfRangeException();

            return _chunks[index / _chunkSize][index % _chunkSize];
        }
        
        set => throw new NotSupportedException();
    }
}