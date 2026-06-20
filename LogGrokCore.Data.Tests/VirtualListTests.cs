using System;
using System.Collections.Generic;
using System.Linq;
using LogGrokCore.Data.Virtualization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class VirtualListTests
    {
        private class TestItemProvider : IItemProvider<string>
        {
            private readonly string[] _items;
            public int FetchCallCount { get; private set; }

            public TestItemProvider(int count)
            {
                _items = Enumerable.Range(0, count).Select(i => $"item_{i}").ToArray();
            }

            public int Count => _items.Length;

            public void Fetch(int start, Span<string> values)
            {
                FetchCallCount++;
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = _items[start + i];
                }
            }
        }

        [TestMethod]
        public void Indexer_ReturnsCorrectItem()
        {
            var provider = new TestItemProvider(1000);
            var list = new VirtualList<string, string>(provider, s => s);

            Assert.AreEqual("item_0", list[0]);
            Assert.AreEqual("item_500", list[500]);
            Assert.AreEqual("item_999", list[999]);
        }

        [TestMethod]
        public void Count_ReflectsProvider()
        {
            var provider = new TestItemProvider(42);
            var list = new VirtualList<string, string>(provider, s => s);

            Assert.AreEqual(42, list.Count);
        }

        [TestMethod]
        public void Converter_IsApplied()
        {
            var provider = new TestItemProvider(10);
            var list = new VirtualList<string, string>(provider, s => s.ToUpper());

            Assert.AreEqual("ITEM_5", list[5]);
        }

        [TestMethod]
        public void CacheHit_DoesNotRefetch()
        {
            var provider = new TestItemProvider(100);
            var list = new VirtualList<string, string>(provider, s => s);

            // Access same page multiple times
            _ = list[0];
            _ = list[1];
            _ = list[50];

            // All within first page (size 128), should be 1 fetch
            Assert.AreEqual(1, provider.FetchCallCount);
        }

        [TestMethod]
        public void DifferentPages_TriggerSeparateFetches()
        {
            var provider = new TestItemProvider(500);
            var list = new VirtualList<string, string>(provider, s => s);

            _ = list[0];    // page 0
            _ = list[128];  // page 1
            _ = list[256];  // page 2

            Assert.AreEqual(3, provider.FetchCallCount);
        }

        [TestMethod]
        public void CacheEviction_WorksCorrectly()
        {
            // VirtualList has MaxCacheSize = 10, PageSize = 128
            // Access 12 different pages to force eviction
            var provider = new TestItemProvider(2000);
            var list = new VirtualList<string, string>(provider, s => s);

            for (int page = 0; page < 12; page++)
            {
                _ = list[page * 128];
            }

            // Access first page again - should require re-fetch since it was evicted
            var fetchCountBefore = provider.FetchCallCount;
            _ = list[0];
            Assert.AreEqual(fetchCountBefore + 1, provider.FetchCallCount);
        }

        private sealed class DisposableItem : IDisposable
        {
            public string Value { get; }
            public bool IsDisposed { get; private set; }
            public DisposableItem(string value) => Value = value;
            public void Dispose() => IsDisposed = true;
        }

        [TestMethod]
        public void CacheEviction_DisposesItemsOfEvictedPages()
        {
            // MaxCacheSize = 10, PageSize = 128.
            var provider = new TestItemProvider(2000);
            var created = new List<DisposableItem>();
            var list = new VirtualList<string, DisposableItem>(provider, s =>
            {
                var item = new DisposableItem(s);
                created.Add(item);
                return item;
            });

            // Touch 12 pages in order; the two oldest (pages 0 and 1) get evicted.
            for (var page = 0; page < 12; page++)
                _ = list[page * 128];

            var evictedPageItem = created.Single(i => i.Value == "item_0");
            var cachedPageItem = created.Single(i => i.Value == "item_256"); // page 2, still cached

            Assert.IsTrue(evictedPageItem.IsDisposed, "Items of an evicted page must be disposed.");
            Assert.IsFalse(cachedPageItem.IsDisposed, "Items of a still-cached page must not be disposed.");
        }

        [TestMethod]
        public void IsReadOnly_ReturnsTrue()
        {
            var provider = new TestItemProvider(10);
            var list = new VirtualList<string, string>(provider, s => s);

            Assert.IsTrue(list.IsReadOnly);
        }

        [TestMethod]
        public void Add_ThrowsNotSupported()
        {
            var provider = new TestItemProvider(10);
            var list = new VirtualList<string, string>(provider, s => s);

            Assert.ThrowsExactly<NotSupportedException>(() => list.Add("test"));
        }

        [TestMethod]
        public void RemoveAt_ThrowsNotSupported()
        {
            var provider = new TestItemProvider(10);
            var list = new VirtualList<string, string>(provider, s => s);

            Assert.ThrowsExactly<NotSupportedException>(() => list.RemoveAt(0));
        }

        [TestMethod]
        public void Enumerator_ReturnsAllItems()
        {
            var provider = new TestItemProvider(300);
            var list = new VirtualList<string, string>(provider, s => s);

            var items = new List<string>();
            foreach (var item in list)
            {
                items.Add(item);
            }

            Assert.AreEqual(300, items.Count);
            Assert.AreEqual("item_0", items[0]);
            Assert.AreEqual("item_299", items[299]);
        }
    }
}
