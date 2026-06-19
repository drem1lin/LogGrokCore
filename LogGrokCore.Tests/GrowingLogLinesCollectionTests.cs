#nullable enable
using System.Collections.Generic;
using LogGrokCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class GrowingLogLinesCollectionTests
    {
        private sealed class FakeItemViewModel : ItemViewModel
        {
        }

        private static FakeItemViewModel Item() => new();

        [TestMethod]
        public void UpdateCount_WhenSourceGrew_RaisesCollectionGrownAndCountChanged()
        {
            var headers = new List<ItemViewModel> { Item() };
            var source = new List<ItemViewModel> { Item(), Item() };
            var collection = new GrowingLogLinesCollection(headers, source);

            var grownTo = new List<int>();
            var changedProperties = new List<string?>();
            collection.CollectionGrown += n => grownTo.Add(n);
            collection.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

            source.Add(Item());
            source.Add(Item());
            collection.UpdateCount();

            CollectionAssert.AreEqual(new[] { 4 }, grownTo,
                "CollectionGrown must report the new source line count.");
            CollectionAssert.Contains(changedProperties, nameof(GrowingLogLinesCollection.Count));
            Assert.AreEqual(4 + 1, collection.Count, "Count = source lines + headers.");
        }

        [TestMethod]
        public void UpdateCount_WhenNothingChanged_RaisesNoEvents()
        {
            var headers = new List<ItemViewModel> { Item() };
            var source = new List<ItemViewModel> { Item(), Item() };
            var collection = new GrowingLogLinesCollection(headers, source);

            var raised = false;
            collection.CollectionGrown += _ => raised = true;
            collection.PropertyChanged += (_, _) => raised = true;

            collection.UpdateCount();

            Assert.IsFalse(raised, "UpdateCount must be a no-op when the counts are unchanged.");
        }
    }
}
