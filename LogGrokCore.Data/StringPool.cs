using System;
using System.Collections.Concurrent;
using System.Threading;

namespace LogGrokCore.Data
{
    public class StringPool
    {
        // Past this many cached strings per size bucket, returned strings are dropped (GC reclaims
        // them) instead of being retained forever. Without a cap the bags only ever grow to the
        // historical peak in-flight count, which slowly leaks over long sessions with varied sizes.
        private const int MaxPooledPerBucket = 64;

        private class StringPoolBucket
        {
            private readonly int _stringSize;
            private readonly ConcurrentBag<string> _pool = new();
            private int _pooledCount;

            public StringPoolBucket(int stringSize)
            {
                _stringSize = stringSize;
            }
            public string Rent()
            {
                if (_pool.TryTake(out var result))
                {
                    Interlocked.Decrement(ref _pooledCount);
                    return result;
                }

                return new string('\0', _stringSize);
            }

            public void Return(string returned)
            {
                if (Interlocked.Increment(ref _pooledCount) > MaxPooledPerBucket)
                {
                    Interlocked.Decrement(ref _pooledCount);
                    return;
                }

                _pool.Add(returned);
            }

            public int PooledCount => Volatile.Read(ref _pooledCount);
        }

        ConcurrentDictionary<int, StringPoolBucket> _buckets = new();

        private Func<int, StringPoolBucket> _bucketFactory = size => new StringPoolBucket(size);
        public string Rent(int size)
        {
            var pooledStringSize = size < 32 ?  32 : Pow2Roundup(size);
            var bucket = _buckets.GetOrAdd(pooledStringSize, _bucketFactory(pooledStringSize ));
            return bucket.Rent();
        }

        public void Return(string returned)
        {
            if (!_buckets.TryGetValue(returned.Length, out var bucket))
                throw new InvalidOperationException();

            bucket.Return(returned);
        }

        internal int GetPooledCount(int size)
        {
            var pooledStringSize = size < 32 ? 32 : Pow2Roundup(size);
            return _buckets.TryGetValue(pooledStringSize, out var bucket) ? bucket.PooledCount : 0;
        }
        
        private static int Pow2Roundup (int x)
        {
            if (x < 0)
                return 0;
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x+1;
        }
    }
}