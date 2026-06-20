using System;
using System.Collections.Generic;
using System.Linq;
using LogGrokCore;
using LogGrokCore.Data.Virtualization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class ItemProviderMapperTests
    {
        private sealed class ArrayItemProvider<T> : IItemProvider<T>
        {
            private readonly T[] _items;
            public List<(int start, int length)> Calls { get; } = new();

            public ArrayItemProvider(T[] items) => _items = items;

            public int Count => _items.Length;

            public void Fetch(int start, Span<T> values)
            {
                Calls.Add((start, values.Length));
                for (var i = 0; i < values.Length; i++)
                    values[i] = _items[start + i];
            }
        }

        [TestMethod]
        public void Fetch_CoalescesContiguousSourceRanges()
        {
            // Item numbers: three runs -> [10,11,12], [20], [30,31].
            var numbers = new ArrayItemProvider<int>(new[] { 10, 11, 12, 20, 30, 31 });
            var items = new ArrayItemProvider<string>(
                Enumerable.Range(0, 40).Select(i => $"v{i}").ToArray());

            var mapper = new ItemProviderMapper<string>(numbers, items);

            var result = new string[6];
            mapper.Fetch(0, result);

            CollectionAssert.AreEqual(
                new[] { "v10", "v11", "v12", "v20", "v30", "v31" }, result);

            // One source fetch per contiguous run, not one per item.
            CollectionAssert.AreEqual(
                new[] { (10, 3), (20, 1), (30, 2) }, items.Calls);
        }

        [TestMethod]
        public void Count_ReflectsItemNumbersProvider()
        {
            var numbers = new ArrayItemProvider<int>(new[] { 1, 2, 3 });
            var items = new ArrayItemProvider<string>(new[] { "a", "b", "c", "d" });

            Assert.AreEqual(3, new ItemProviderMapper<string>(numbers, items).Count);
        }
    }
}
