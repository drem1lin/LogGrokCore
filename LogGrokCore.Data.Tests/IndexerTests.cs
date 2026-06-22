using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LogGrokCore.Data.Index;
using LogGrokCore.Data.Monikers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class IndexerTests
    {
        // Builds an IndexKey with the same in-memory layout the parser produces: a meta header
        // (LineOffsetFromBufferStart + per-component start/length, stored as ints) followed by the
        // component text. Uses string.Create + MemoryMarshal so no unsafe block is needed.
        private static IndexKey MakeKey(params string[] components)
        {
            var componentCount = components.Length;
            var headerChars = LineMetaInformation.GetSizeChars(componentCount);
            var data = string.Concat(components);
            var aligned = (headerChars + data.Length + 1) & ~1; // TotalSizeWithPayloadCharsAligned

            var buffer = string.Create(aligned, (components, headerChars, data), static (span, state) =>
            {
                var (comps, hdr, dataStr) = state;
                var meta = MemoryMarshal.Cast<char, int>(span.Slice(0, hdr));
                meta[0] = 0; // LineOffsetFromBufferStart
                var offset = 0;
                for (var i = 0; i < comps.Length; i++)
                {
                    meta[1 + i * 2] = offset;             // ComponentStart
                    meta[1 + i * 2 + 1] = comps[i].Length; // ComponentLength
                    offset += comps[i].Length;
                }
                dataStr.AsSpan().CopyTo(span.Slice(hdr));
            });

            return new IndexKey(buffer, 0, componentCount);
        }

        [TestMethod]
        public void SameKeyTwice_SharesKeyNumber_AndAddsComponentOnce()
        {
            var indexer = new Indexer();
            var newComponents = new List<string>();
            indexer.NewComponentAdded += t => newComponents.Add(t.key.GetComponent(t.compnentNumber).ToString());

            indexer.Add(MakeKey("ERROR"), 0);
            indexer.Add(MakeKey("ERROR"), 1);

            // The repeated key must be recognised as already-known: one component value, one event.
            CollectionAssert.AreEqual(new[] { "ERROR" }, indexer.GetAllComponents(0).ToList());
            CollectionAssert.AreEqual(new[] { "ERROR" }, newComponents);
        }

        [TestMethod]
        public void DistinctKeys_AreTrackedSeparately()
        {
            var indexer = new Indexer();
            indexer.Add(MakeKey("INFO"), 0);
            indexer.Add(MakeKey("ERROR"), 1);
            indexer.Add(MakeKey("INFO"), 2);

            CollectionAssert.AreEqual(
                new[] { "ERROR", "INFO" },
                indexer.GetAllComponents(0).OrderBy(s => s).ToList());
        }

        [TestMethod]
        public void GetIndexCountForComponent_CountsLinesPerValue()
        {
            var indexer = new Indexer();
            indexer.Add(MakeKey("INFO"), 0);
            indexer.Add(MakeKey("ERROR"), 1);
            indexer.Add(MakeKey("INFO"), 2);
            indexer.Add(MakeKey("INFO"), 3);

            Assert.AreEqual(3, indexer.GetIndexCountForComponent(0, "INFO"));
            Assert.AreEqual(1, indexer.GetIndexCountForComponent(0, "ERROR"));
            Assert.AreEqual(0, indexer.GetIndexCountForComponent(0, "WARN"));
        }

        [TestMethod]
        public void GetIndexCountForComponent_RecountsAfterMoreLinesAdded()
        {
            var indexer = new Indexer();
            indexer.Add(MakeKey("INFO"), 0);
            Assert.AreEqual(1, indexer.GetIndexCountForComponent(0, "INFO")); // populates the cache

            // Adding a new key changes the key count, so the cached match set must be refreshed and
            // live counts re-summed.
            indexer.Add(MakeKey("ERROR"), 1);
            indexer.Add(MakeKey("INFO"), 2);

            Assert.AreEqual(2, indexer.GetIndexCountForComponent(0, "INFO"));
            Assert.AreEqual(1, indexer.GetIndexCountForComponent(0, "ERROR"));
        }

        [TestMethod]
        public void MultiComponentKey_ComponentsTrackedPerIndex()
        {
            var indexer = new Indexer();
            indexer.Add(MakeKey("2026", "INFO"), 0);
            indexer.Add(MakeKey("2026", "ERROR"), 1);

            CollectionAssert.AreEqual(new[] { "2026" }, indexer.GetAllComponents(0).ToList());
            CollectionAssert.AreEqual(
                new[] { "ERROR", "INFO" },
                indexer.GetAllComponents(1).OrderBy(s => s).ToList());
        }
    }
}
