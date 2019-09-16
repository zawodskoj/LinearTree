using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Zw.LinearTree
{
    public delegate TId SelectId<T, TId>(T item) where T : class where TId : struct;
    public delegate TId? SelectParentId<T, TId>(T item) where T : class where TId : struct;
    public delegate bool IdComparer<TId>(TId id1, TId id2) where TId: struct;

    public delegate TSortKey SelectSortKey<T, TSortKey>(T item) where T : class;
    public delegate int SortKeyComparer<TSortKey>(TSortKey lhs, TSortKey rhs);
    
    public static class AutoTreeSortedList<T> where T : class
    {
        public static AutoTreeSortedList<T, TId, TSortKey> Create<TId, TSortKey>(
            SelectId<T, TId> selectId,
            SelectParentId<T, TId> selectParentId,
            IdComparer<TId> idComparer,
            SelectSortKey<T, TSortKey> selectSortKey,
            SortKeyComparer<TSortKey> sortKeyComparer) where TId : struct
        {
            return new AutoTreeSortedList<T, TId, TSortKey>(
                selectId, selectParentId, idComparer, selectSortKey, sortKeyComparer);
        }
    }
    
    public class AutoTreeSortedList<T, TId, TSortKey> : 
        IReadOnlyList<LinearTreeNode<T>>, 
        IDispatchingCollectionChanges<LinearTreeNode<T>>
        where T : class where TId : struct
    {
        private readonly LinearTree<T> _tree;
        private readonly List<LinearTreeNode<T>> _nodes;
        
        private readonly SelectId<T, TId> _selectId;
        private readonly SelectParentId<T, TId> _selectParentId;
        private readonly IdComparer<TId> _idComparer;
        private readonly SelectSortKey<T, TSortKey> _selectSortKey;
        private readonly SortKeyComparer<TSortKey> _sortKeyComparer;

        public AutoTreeSortedList(
            SelectId<T, TId> selectId,
            SelectParentId<T, TId> selectParentId,
            IdComparer<TId> idComparer,
            SelectSortKey<T, TSortKey> selectSortKey,
            SortKeyComparer<TSortKey> sortKeyComparer)
        {
            _selectId = selectId;
            _selectParentId = selectParentId;
            _selectSortKey = selectSortKey;
            _idComparer = idComparer;
            _sortKeyComparer = sortKeyComparer;
            _tree = new LinearTree<T>();
            _nodes = new List<LinearTreeNode<T>>();
            
            _tree.CollectionChanged += (_, change) =>
            {
                switch (change.Type)
                {
                    case CollectionChangeType.INSERT:
                        for (var i = 0; i < change.Count; i++)
                            _nodes.Insert(change.DestinationIndex + i, _tree.NodeAt(i + change.DestinationIndex));
                        break;
                    case CollectionChangeType.REMOVE:
                        _nodes.RemoveRange(change.SourceIndex, change.Count);
                        break;
                    case CollectionChangeType.MOVE:
                        var src = change.SourceIndex;
                        var dst = change.DestinationIndex;
                        var n = change.Count;

                        if (src == dst) break;
                        if (src < dst)
                        {
                            for (int i = src + n - 1, j = dst + n - 1; i >= src; i--, j--)
                            {
                                var el = _nodes[i];
                                _nodes.RemoveAt(i);
                                _nodes.Insert(j, el);
                            }
                        }
                        else
                        {
                            for (int i = src, j = dst; i < src + n; i++, j++)
                            {
                                var el = _nodes[i];
                                _nodes.RemoveAt(i);
                                _nodes.Insert(j, el);
                            }
                        }
                        
                        break;
                    case CollectionChangeType.REPLACE:
                        for (var i = change.SourceIndex; i < change.SourceIndex + change.Count; i++)
                            _nodes[i] = _nodes[i];
                        break;
                }
                
                CollectionChanged?.Invoke(this, change);
            };
        }

        private int FindRequiredPosition(ILinearTreeNode<T> parent, T item)
        {
            var id = _selectId(item);
            var sortKey = _selectSortKey(item);
            
            var requiredPosition = 0;
            for (; requiredPosition < parent.Children.Count; requiredPosition++)
            {
                var nodeAtI = parent.Children[requiredPosition];
                if (_idComparer(id, _selectId(nodeAtI.Value))) continue;

                var otherSortKey = _selectSortKey(nodeAtI.Value);
                var comparisonResult = _sortKeyComparer(sortKey, otherSortKey);

                if (comparisonResult <= 0)
                    break;
            }

            return requiredPosition;
        }
        
        private void MoveToRequiredPosition(LinearTreeNode<T> node)
        {
            var parent = node.Parent;

            var sortKey = _selectSortKey(node.Value);
            var actualPosition = parent.IndexOfChild(node);
            
            // checking that existing position is valid (keep sorting stable)
            var greaterThanAbove = actualPosition == 0 ||
                                _sortKeyComparer(sortKey, _selectSortKey(parent.Children[actualPosition - 1].Value)) >= 0;
            
            var lessThanBelow = actualPosition == parent.Children.Count - 1 ||
                                   _sortKeyComparer(sortKey, _selectSortKey(parent.Children[actualPosition + 1].Value)) <= 0;

            if (lessThanBelow && greaterThanAbove)
            {
                // position is valid, no need to move
                Debug.WriteLine("Node at {0}, no need to move (sk: {1})", actualPosition, sortKey);
                
                return;
            }
            
            // trying to find any valid position
            var requiredPosition = FindRequiredPosition(parent, node.Value);
            Debug.Assert(requiredPosition != actualPosition);

            if (requiredPosition > actualPosition)
                requiredPosition--;
            
            Debug.WriteLine("Node at {0}, moving to {1}", actualPosition, requiredPosition);

            parent.MoveNode(actualPosition, requiredPosition);
        }

        public void Upsert(T item)
        {
            Debug.WriteLine("Upserting item: {0}", item);
            
            if (item == null) throw new ArgumentNullException(nameof(item));
            var id = _selectId(item);

            var node = _nodes.FirstOrDefault(x => _idComparer(_selectId(x.Value), id));
            
            if (node != null)
            {
                Debug.WriteLine("Node exists, updating and moving to required position");
                
                node.Value = item;
                MoveToRequiredPosition(node);
                return;
            }
            
            var parentId = _selectParentId(item);
            ILinearTreeNode<T> parentNode = parentId != null
                ? _nodes.FirstOrDefault(x => _idComparer(_selectId(x.Value), parentId.Value))
                : null;

            parentNode = parentNode ?? _tree;
            
            var requiredPosition = FindRequiredPosition(parentNode, item);
            node = parentNode.InsertNode(item, requiredPosition);
            
            Debug.WriteLine("Node does not exist, inserting at position {0}", requiredPosition);

            while (true)
            {
                again:
                
                foreach (var treeChild in _tree.Children)
                {
                    var childParentId = _selectParentId(treeChild.Value);
                    if (childParentId != null && _idComparer(childParentId.Value, id))
                    {
                        var position = FindRequiredPosition(node, treeChild.Value);
                        node.ReparentNode(treeChild, position);

                        goto again;
                    }
                }

                break;
            }
        }

        public void Delete(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var id = _selectId(item);

            var node = _nodes.FirstOrDefault(x => _idComparer(_selectId(x.Value), id));
            if (node == null) return;

            var childCount = node.Children.Count;
            for (var i = 0; i < childCount; i++)
            {
                var child = node.Children[0];
                var position = FindRequiredPosition(_tree, child.Value);
                
                _tree.ReparentNode(child, position);
            }

            var index = node.Parent.IndexOfChild(node);
            node.Parent.RemoveNode(index);
        }

        public IEnumerator<LinearTreeNode<T>> GetEnumerator() => _nodes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => _nodes.Count;

        public LinearTreeNode<T> this[int index] => _nodes[index];
        public event EventHandler<CollectionChange> CollectionChanged;

        public void Clear()
        { 
            _tree.ClearChildren();

            if (_nodes.Count > 0)
                throw new Exception("Node count > 0 after clear, should not happen");
        }
    }
}