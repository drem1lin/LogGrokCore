using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using LogGrokCore.Data.Index;

namespace LogGrokCore.Data.IndexTree
{
    public class IndexTree<T, TLeaf> : IIndex<T>
        where TLeaf : LeafOrNode<T, TLeaf>, ILeaf<T, TLeaf>, ITreeNode<T>, IEnumerable<T>
        where T : IComparable<T>
    {
        private readonly int _nodeCapacity;
        private TLeaf? _currentLeaf;
        private int _count;
        private LeafOrNode<T, TLeaf>? _head;
        private readonly Func<T, TLeaf> _createFirstLeaf;

        public IndexTree(int nodeCapacity, Func<T, TLeaf> createFirstLeaf)
        {
            _nodeCapacity = nodeCapacity;
            _createFirstLeaf = createFirstLeaf;
            _head = _currentLeaf;
        }

        // Single writer (the loader thread). _head and _count are published with release semantics so
        // UI reader threads observe a fully constructed leaf/node before the count that refers to it.
        public void Add(T value)
        {
            if (_currentLeaf != null)
            {
                var newLeaf = _currentLeaf.Add(value, _count);
                if (newLeaf != null)
                    OnNewLeafCreated(newLeaf);
            }
            else
            {
                _currentLeaf = _createFirstLeaf(value);
                Volatile.Write(ref _head, _currentLeaf);
            }
            Volatile.Write(ref _count, _count + 1);
        }

        public int Count => Volatile.Read(ref _count);

        public IEnumerable<T> GetEnumerableFromIndex(int index)
        {
            var head = Volatile.Read(ref _head);
            return head == null ? Enumerable.Empty<T>() : head.GetEnumerableFromIndex(index);
        }

        public IEnumerable<T> GetEnumerableFromValue(T value)
        {
            var head = Volatile.Read(ref _head);
            return head == null ? Enumerable.Empty<T>() : head.GetEnumerableFromValue(value);
        }

        public int FindIndexByValue(T value)
        {
            return Volatile.Read(ref _head)?.GetIndexByValue(value) ?? 0;
        }

        private void OnNewLeafCreated(TLeaf newLeaf)
        {
            switch (_head)
            {
                case TreeNode<T, TLeaf> headNode:
                    var newNode = AddToTree(headNode, newLeaf);
                    if (newNode != null)
                        Volatile.Write(ref _head, new TreeNode<T, TLeaf>(_nodeCapacity, _head, newNode));
                    break;
                case TLeaf leaf:
                    var treeHead = new TreeNode<T, TLeaf>(_nodeCapacity, leaf, newLeaf);
                    Volatile.Write(ref _head, treeHead);
                    break;
            }
            _currentLeaf = newLeaf;
        }

        private static TreeNode<T, TLeaf>? AddToTree(TreeNode<T, TLeaf> node, TLeaf newLeaf)
        {
            var lastSubNode = node.LastSubNode;
            switch (lastSubNode)
            {
                case TreeNode<T, TLeaf> subNode:
                    var newlyCreated = AddToTree(subNode, newLeaf);
                    return newlyCreated != null ? node.TryAdd(newlyCreated) : null;
                case TLeaf _:
                    var newNode = node.TryAdd(newLeaf);
                    return newNode;
                default:
                    throw new InvalidOperationException();
            }
        }

        public T this[int idx]
        {
            get
            {
                Debug.Assert(idx <= Volatile.Read(ref _count));
                var head = Volatile.Read(ref _head);
                if (head == null)
                    throw new InvalidOperationException("Unable to get element from empty tree.");
                return head.GetValue(idx);
            }
        }
    }
}