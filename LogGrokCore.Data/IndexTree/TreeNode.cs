using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using LogGrokCore.Data.Index;

namespace LogGrokCore.Data.IndexTree
{
    // Like the leaves: the sub-node list has fixed capacity (never reallocates; a full node spills
    // into a new node) and is appended by the loader while the UI reads it. _count is published with
    // release semantics after the sub-node is stored and read with acquire semantics, and the
    // reader-side BinarySearch is bounded by it, so a reader never indexes a not-yet-published slot.
    public class TreeNode<T, TLeaf> : LeafOrNode<T, TLeaf>
        where TLeaf : class, ILeaf<T, TLeaf>, ITreeNode<T>, IEnumerable<T>
        where T : IComparable<T>
    {
        private readonly List<LeafOrNode<T, TLeaf>> _subNodes;
        private int _count;

        public TreeNode(int nodeCapacity, LeafOrNode<T, TLeaf> first, LeafOrNode<T, TLeaf> second)
        {
            Debug.Assert(nodeCapacity > 1);
            _subNodes = new List<LeafOrNode<T, TLeaf>>(nodeCapacity) {first, second};
            Volatile.Write(ref _count, 2);
        }

        private TreeNode(int nodeCapacity, LeafOrNode<T, TLeaf> first)
        {
            Debug.Assert(nodeCapacity > 1);
            _subNodes = new List<LeafOrNode<T, TLeaf>>(nodeCapacity) {first};
            Volatile.Write(ref _count, 1);
        }

        internal LeafOrNode<T, TLeaf> LastSubNode => _subNodes[^1];

        public TreeNode<T, TLeaf>? TryAdd(LeafOrNode<T, TLeaf> node)
        {
            if (_subNodes.Count < _subNodes.Capacity)
            {
                _subNodes.Add(node);
                Volatile.Write(ref _count, _subNodes.Count); // publish the sub-node before the count
            }
            else
                return new TreeNode<T, TLeaf>(_subNodes.Capacity, node);

            return null;
        }

        public override T GetValue(int index) => GetSubNodeByIndex(index).GetValue(index);

        public override IEnumerable<T> GetEnumerableFromIndex(int index) =>
            GetSubNodeByIndex(index).GetEnumerableFromIndex(index);

        public override (int index, TLeaf leaf) FindByValue(T value)
        {
            var count = Volatile.Read(ref _count);
            var index = _subNodes.BinarySearch(0, count, value,
                static (leafOrNode, t) => leafOrNode.FirstValue.CompareTo(t));
            var subNodeIndex = index >= 0 ? index : ~index - 1;
            return _subNodes[subNodeIndex].FindByValue(value);
        }

        private LeafOrNode<T, TLeaf> GetSubNodeByIndex(int index)
        {
            var count = Volatile.Read(ref _count);
            var found = _subNodes.BinarySearch(0, count, index,
                static (leafOrNode, t) => leafOrNode.MinIndex.CompareTo(t));
            
            // When i is < 0, ~i is index of the first element, which MinIndex is greater than index.
            // Since we need last element with MinIndex < index, the result is (~i - 1)
            var subNodeIndex = found >= 0 ? found : ~found - 1;
            return _subNodes[subNodeIndex];
        }
        
        public override T FirstValue => _subNodes[0].FirstValue;
        public override int MinIndex => _subNodes[0].MinIndex;
    }
}