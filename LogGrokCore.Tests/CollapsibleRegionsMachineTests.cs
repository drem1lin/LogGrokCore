using System.Collections.Generic;
using LogGrokCore.Controls.TextRender;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class CollapsibleRegionsMachineTests
    {
        // 5 source lines, one collapsible region covering lines 1..3 (start=1, length=3).
        private static CollapsibleRegionsMachine SingleRegion(HashSet<int>? collapsed = null) =>
            new(5, new[] { (1, 3) }, () => collapsed);

        private static void Toggle(CollapsibleRegionsMachine machine, int index)
        {
            ((Expandable)machine[index].Item1).Toggle();
        }

        [TestMethod]
        public void InitialState_AllExpanded()
        {
            var machine = SingleRegion();

            Assert.AreEqual(5, machine.LineCount);
            Assert.IsInstanceOfType(machine[0].Item1, typeof(None));
            Assert.IsInstanceOfType(machine[1].Item1, typeof(ExpandedUpper));
            Assert.IsInstanceOfType(machine[2].Item1, typeof(None));
            Assert.IsInstanceOfType(machine[3].Item1, typeof(ExpandedLower));
            Assert.IsInstanceOfType(machine[4].Item1, typeof(None));
            Assert.IsFalse(machine.HasCollapsedRegions());
        }

        [TestMethod]
        public void Toggle_CollapsesRegion_HidesInnerLines()
        {
            var machine = SingleRegion();
            Toggle(machine, 1);

            Assert.AreEqual(3, machine.LineCount);
            Assert.IsInstanceOfType(machine[1].Item1, typeof(Collapsed));
            // the line that follows the collapsed region (index 4) is the third visible row
            Assert.AreEqual(4, machine[2].Item2);
            Assert.IsTrue(machine.IsCollapsed(1));
            Assert.IsTrue(machine.HasCollapsedRegions());
        }

        [TestMethod]
        public void ToggleTwice_ReturnsToExpanded()
        {
            var machine = SingleRegion();
            Toggle(machine, 1); // collapse
            Assert.AreEqual(3, machine.LineCount);

            Toggle(machine, 1); // expand again
            Assert.AreEqual(5, machine.LineCount);
            Assert.IsFalse(machine.IsCollapsed(1));
            Assert.IsFalse(machine.HasCollapsedRegions());
        }

        [TestMethod]
        public void InitiallyCollapsed_FromGetter()
        {
            var machine = SingleRegion(new HashSet<int> { 1 });

            Assert.AreEqual(3, machine.LineCount);
            Assert.IsTrue(machine.IsCollapsed(1));
            Assert.IsInstanceOfType(machine[1].Item1, typeof(Collapsed));
        }

        [TestMethod]
        public void TwoRegions_TogglingOneLeavesTheOther()
        {
            // 8 lines: region A = lines 1..2 (start=1,len=2), region B = lines 4..6 (start=4,len=3)
            var machine = new CollapsibleRegionsMachine(8, new[] { (1, 2), (4, 3) }, () => null);
            Assert.AreEqual(8, machine.LineCount);

            Toggle(machine, 4); // collapse region B only

            Assert.AreEqual(6, machine.LineCount);
            Assert.IsTrue(machine.IsCollapsed(4));
            Assert.IsFalse(machine.IsCollapsed(1));
            Assert.IsInstanceOfType(machine[1].Item1, typeof(ExpandedUpper));
            Assert.IsInstanceOfType(machine[2].Item1, typeof(ExpandedLower));
        }

        [TestMethod]
        public void CollapseRecursively_ThenExpandRecursively()
        {
            var machine = new CollapsibleRegionsMachine(8, new[] { (1, 2), (4, 3) }, () => null);

            machine.CollapseRecursively();
            Assert.IsTrue(machine.HasCollapsedRegions());
            Assert.IsFalse(machine.HasExpandedRegions());

            machine.ExpandRecursively();
            Assert.IsFalse(machine.HasCollapsedRegions());
            Assert.IsTrue(machine.HasExpandedRegions());
            Assert.AreEqual(8, machine.LineCount);
        }
    }
}
