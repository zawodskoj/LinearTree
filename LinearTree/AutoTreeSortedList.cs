using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LinearTree
{
    public static class AutoTreeSortedList<T> where T : class
    {
        public static AutoTreeSortedList<T, TId, TSortKey> Create<TId, TSortKey>(
            AutoTreeSortedList<T, TId, TSortKey>.SelectId selectId,
            AutoTreeSortedList<T, TId, TSortKey>.SelectParentId selectParentId,
            AutoTreeSortedList<T, TId, TSortKey>.IdComparer idComparer,
            AutoTreeSortedList<T, TId, TSortKey>.SelectSortKey selectSortKey,
            AutoTreeSortedList<T, TId, TSortKey>.SortKeyComparer sortKeyComparer) where TId : struct
        {
            return new AutoTreeSortedList<T, TId, TSortKey>(
                selectId, selectParentId, selectSortKey, idComparer, sortKeyComparer);
        }
    }
    
    public class AutoTreeSortedList<T, TId, TSortKey> : IReadOnlyList<LinearTreeNode<T>> where T : class where TId : struct
    {
        public delegate TId SelectId(T item);
        public delegate TId? SelectParentId(T item);
        public delegate bool IdComparer(TId id1, TId id2);

        public delegate TSortKey SelectSortKey(T item);
        public delegate int SortKeyComparer(TSortKey lhs, TSortKey rhs);

        private readonly LinearTree<T> _tree;
        private readonly List<LinearTreeNode<T>> _nodes;
        
        private readonly SelectId _selectId;
        private readonly SelectParentId _selectParentId;
        private readonly SelectSortKey _selectSortKey;
        private readonly IdComparer _idComparer;
        private readonly SortKeyComparer _sortKeyComparer;

        public AutoTreeSortedList(SelectId selectId, SelectParentId selectParentId, SelectSortKey selectSortKey,
            IdComparer idComparer, SortKeyComparer sortKeyComparer)
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
            var aboveLessThan = actualPosition == 0 ||
                                _sortKeyComparer(sortKey, _selectSortKey(parent.Children[actualPosition - 1].Value)) >= 0;
            
            var belowGreaterThan = actualPosition >= parent.Children.Count ||
                                   _sortKeyComparer(sortKey, _selectSortKey(parent.Children[actualPosition + 1].Value)) <= 0;

            if (belowGreaterThan && aboveLessThan)
            {
                // position is valid, no need to move
                return;
            }

            // trying to find any valid position
            var requiredPosition = FindRequiredPosition(parent, node.Value);
            if (actualPosition < requiredPosition) requiredPosition--;
            
            parent.MoveNode(actualPosition, requiredPosition);
        }

        public void Upsert(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var id = _selectId(item);

            var node = _nodes.FirstOrDefault(x => _idComparer(_selectId(x.Value), id));
            
            if (node != null)
            {
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
    }
}