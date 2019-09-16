using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Zw.LinearTree
{
    public enum CollectionChangeType
    {
        INSERT,
        REMOVE,
        MOVE,
        REPLACE
    }

    public struct CollectionChange
    {
        public CollectionChange(CollectionChangeType type, int sourceIndex, int destinationIndex, int count)
        {
            Type = type;
            SourceIndex = sourceIndex;
            DestinationIndex = destinationIndex;
            Count = count;
        }
        
        public static CollectionChange Insert(int index, int count) => new CollectionChange(CollectionChangeType.INSERT, -1, index, count);
        public static CollectionChange Remove(int index, int count) => new CollectionChange(CollectionChangeType.REMOVE, index, -1, count);
        public static CollectionChange Move(int from, int to, int count) => new CollectionChange(CollectionChangeType.MOVE, from, to, count);
        public static CollectionChange Replace(int index, int count) => new CollectionChange(CollectionChangeType.REPLACE, index, index, count);

        public CollectionChangeType Type { get; }
        public int SourceIndex { get; }
        public int DestinationIndex { get; }
        public int Count { get; }
    }

    public struct Reparenting<T>
    {
        public LinearTreeNode<T> Node { get; }
        public LinearTreeNode<T> OldParent { get; }
        public CollectionChange RemChange { get; }
        public LinearTreeNode<T> NewParent { get; }
        public CollectionChange InsChange { get; }
        public LinearTreeNode<T> MostCommon { get; }
        public int NodeOffsetRelativeToMostCommonBefore { get; }
        public int NodeOffsetRelativeToMostCommonAfter { get; }

        public Reparenting(LinearTreeNode<T> node, LinearTreeNode<T> oldParent, CollectionChange remChange, LinearTreeNode<T> newParent,
            CollectionChange insChange, LinearTreeNode<T> mostCommon, int nodeOffsetRelativeToMostCommonBefore, int nodeOffsetRelativeToMostCommonAfter)
        {
            Node = node;
            OldParent = oldParent;
            RemChange = remChange;
            NewParent = newParent;
            InsChange = insChange;
            MostCommon = mostCommon;
            NodeOffsetRelativeToMostCommonBefore = nodeOffsetRelativeToMostCommonBefore;
            NodeOffsetRelativeToMostCommonAfter = nodeOffsetRelativeToMostCommonAfter;
        }
    }

    public interface ILinearTreeNode<T>
    {
        IReadOnlyList<LinearTreeNode<T>> Children { get; }
        LinearTreeNode<T> InsertNode(T value, int index);
        void RemoveNode(int index);
        void MoveNode(int from, int to);
        void ReparentNode(LinearTreeNode<T> node, int index);
        void ClearChildren();
        LinearTreeNode<T> NodeAt(int index);
        IEnumerable<LinearTreeNode<T>> IterateNodes();
    }
    
    [DebuggerDisplay("Value = ({Value}), Level = {Level}, Children = {Children.Count}")]
    public class LinearTreeNode<T> : IReadOnlyList<T>, ILinearTreeNode<T>
    {
        
        private readonly List<LinearTreeNode<T>> _children = new List<LinearTreeNode<T>>();
        private Action<LinearTreeNode<T>, CollectionChange> _dispatchCollectionChange;
        private readonly int _offset;

        private LinearTreeNode<T> _parent;
        private int _descendantsCount;
        private T _value;

        public LinearTreeNode(
            T value, 
            Action<LinearTreeNode<T>, CollectionChange> dispatchCollectionChange,
            LinearTreeNode<T> parent)
        {
            _dispatchCollectionChange = dispatchCollectionChange;

            _parent = parent;

            _offset = parent == null ? 0 : 1;
            
            Value = value;
        }

        public LinearTreeNode<T> Parent => _parent;

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                
                if (_parent != null)
                    _dispatchCollectionChange(this, CollectionChange.Replace(0, 1));
            }
        }

        public IReadOnlyList<LinearTreeNode<T>> Children => _children;

        public int IndexOfChild(LinearTreeNode<T> node) => _children.IndexOf(node);

        private IEnumerable<T> Iterate()
        {
            if (_parent != null)
                yield return Value;
            
            foreach (var child in _children)
            {
                foreach (var iterated in child.Iterate())
                    yield return iterated;
            }   
        }
        
        public IEnumerable<LinearTreeNode<T>> IterateNodes()
        {
            if (_parent != null)
                yield return this;
            
            foreach (var child in _children)
            {
                foreach (var iterated in child.IterateNodes())
                    yield return iterated;
            }   
        }

        public IEnumerator<T> GetEnumerator() => Iterate().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Iterate().GetEnumerator();

        public int Count => _descendantsCount + _offset;
        public int Level => _parent?.Level + 1 ?? 0;

        public T this[int index] => NodeAt(index).Value;

        public LinearTreeNode<T> NodeAt(int index)
        {
            if (index < 0 || index >= _descendantsCount + _offset)
                throw new IndexOutOfRangeException();

            var cnt = 0;

            if (_parent != null)
            {
                if (index == 0)
                    return this;

                index--;
            }

            foreach (var child in _children)
            {
                var count = child.Count;
                if (index < cnt + count)
                    return child.NodeAt(index - cnt);

                cnt += count;
            }

            throw new IndexOutOfRangeException("Should never happen");
        }

        public LinearTreeNode<T> InsertNode(T value, int index)
        {
            var node = new LinearTreeNode<T>(value, ReceiveCollectionChange, this);
            
            InsertNode(node, index, true);

            return node;
        }

        private CollectionChange InsertNode(LinearTreeNode<T> node, int index, bool dispatch)
        {
            var preCount = 0;

            for (var i = 0; i < index; i++)
                preCount += _children[i].Count;
            
            _children.Insert(index, node);
            _descendantsCount += node.Count;

            var change = CollectionChange.Insert(preCount + _offset, node.Count);
            
            if (dispatch)
                _dispatchCollectionChange(this, change);

            return change;
        }

        public void RemoveNode(int index) => RemoveNode(index, true);
        
        private CollectionChange RemoveNode(int index, bool dispatch)
        {
            var preCount = 0;

            for (var i = 0; i < index; i++)
                preCount += _children[i].Count;
            
            var node = _children[index];
            var innerCount = node.Count;
            
            _children.RemoveAt(index);
            _descendantsCount -= innerCount;

            var change = CollectionChange.Remove(preCount + _offset, innerCount);
            
            if (dispatch)
                _dispatchCollectionChange(this, change);

            return change;
        }

        public void MoveNode(int from, int to)
        {
            var node = _children[from];

            var positionBefore = 0;

            for (var i = 0; i < from; i++)
                positionBefore += _children[i].Count;
            
            _children.RemoveAt(from);
            _children.Insert(to, node);

            var positionAfter = 0;
            
            for (var i = 0; i < to; i++)
                positionAfter += _children[i].Count;
            
            _dispatchCollectionChange(this, CollectionChange.Move(positionBefore + _offset, positionAfter + _offset, node.Count));
        }

        public void ReparentNode(LinearTreeNode<T> node, int index)
        {
            if (node == this) throw new InvalidOperationException("Self-reparenting is not allowed");
            if (node._parent == this) throw new InvalidOperationException("Reparenting to same parent is not allowed");
            if (node._parent == null) throw new InvalidOperationException("Reparenting root node is not allowed");
            EnsureNotParentToChildReparenting(node);

            var mostCommon = node._parent;

            while (mostCommon != null)
            {
                var cur = this;

                while (cur != null)
                {
                    if (mostCommon == cur)
                        goto found;

                    cur = cur._parent;
                }

                mostCommon = mostCommon._parent;
            }
             
            throw new InvalidOperationException("No common parents between current and reparenting node");
            
            found:

            var oldParent = node._parent;
            var oldChildIx = oldParent._children.IndexOf(node);

            var offsetBefore = node.CalculateOffsetRelativeToParent(mostCommon);
            
            var remChange = oldParent.RemoveNode(oldChildIx, false);

            if (mostCommon == oldParent)
            {
                oldParent._descendantsCount += remChange.Count;
            }
            else
            {
                var oldParent_ = oldParent._parent;
                while (oldParent_ != mostCommon && oldParent_ != null)
                {
                    oldParent_._descendantsCount -= remChange.Count;
                    oldParent_ = oldParent_._parent;
                }
            }

            node._parent = this;
            node._dispatchCollectionChange = ReceiveCollectionChange;
            
            var insChange = InsertNode(node, index, false);

            if (mostCommon == this)
            {
                _descendantsCount -= insChange.Count;
            }
            else
            {
                var newParent_ = _parent;
                while (newParent_ != mostCommon && newParent_ != null)
                {
                    newParent_._descendantsCount += insChange.Count;
                    newParent_ = newParent_._parent;
                }
            }

            var offsetAfter = node.CalculateOffsetRelativeToParent(mostCommon);
            mostCommon._dispatchCollectionChange(mostCommon, CollectionChange.Move(offsetBefore, offsetAfter, insChange.Count));
        }

        public void ReparentForeignNode(LinearTreeNode<T> node, int index)
        {
            if (node._parent == null) throw new InvalidOperationException("Reparenting root node is not allowed");
            if (index < 0 || index > _children.Count) throw new IndexOutOfRangeException();
            
            var oldIndex = node._parent._children.IndexOf(node);

            node._parent.RemoveNode(oldIndex);
            InsertNode(node, index, true);
        }
 
        private void EnsureNotParentToChildReparenting(LinearTreeNode<T> node)
        {
            var parent = _parent;
            
            while (parent != null)
            {
                if (parent == node)
                    throw new InvalidOperationException("Reparenting parent to its child is not allowed");
                
                parent = parent._parent;
            }
        }

        public void ClearChildren()
        {
            if (_children.Count is int count && count > 0)
            {
                _children.Clear();

                var oldDescendantsCount = _descendantsCount;
                _descendantsCount = 0;
                _dispatchCollectionChange(this, CollectionChange.Remove(_offset, oldDescendantsCount));
            }
        }
        
        private void ReceiveCollectionChange(LinearTreeNode<T> child, CollectionChange change)
        {
            var baseIndex = _offset;
            
            foreach (var c in _children)
            {
                if (c == child)
                    goto found;
                    
                var count = c.Count;
                baseIndex += count;
            }
            
            return;

            found: ;
            
            var newChange = new CollectionChange(
                change.Type,
                change.SourceIndex == -1 ? -1 : change.SourceIndex + baseIndex,
                change.DestinationIndex == -1 ? -1 : change.DestinationIndex + baseIndex, 
                change.Count);

            switch (change.Type)
            {
                case CollectionChangeType.INSERT:
                    _descendantsCount += change.Count;
                    break;
                case CollectionChangeType.REMOVE:
                    _descendantsCount -= change.Count;
                    break;
            }

            _dispatchCollectionChange(this, newChange);
        }

        private int CalculateOffsetRelativeToParent(LinearTreeNode<T> node)
        {
            var offset = 0;

            var parent = _parent;
            var child = this;

            while (true)
            {
                var childIndex = parent._children.IndexOf(child);
                for (var i = 0; i < childIndex; i++)
                    offset += parent._children[i].Count;

                offset += parent._offset;

                if (parent == node)
                    break;

                child = parent;
                parent = parent._parent ?? throw new Exception("Could not calculate offset: reached root");
            }

            return offset;
        }
    }

    public interface IDispatchingCollectionChanges<T>
    {
        event EventHandler<CollectionChange> CollectionChanged;
    }

    public class LinearTree<T> : IReadOnlyList<T>, ILinearTreeNode<T>, IDispatchingCollectionChanges<T>
    {
        private readonly LinearTreeNode<T> _root;
        
        public LinearTree()
        {
            _root = new LinearTreeNode<T>(default, ReceiveCollectionChange, null);
        }

        private void ReceiveCollectionChange(LinearTreeNode<T> node, CollectionChange change)
        {
            CollectionChanged?.Invoke(this, change);
        }

        public IReadOnlyList<LinearTreeNode<T>> Children => _root.Children;
        public event EventHandler<CollectionChange> CollectionChanged;

        public LinearTreeNode<T> InsertNode(T value, int index) => _root.InsertNode(value, index);
        public void RemoveNode(int index) => _root.RemoveNode(index);
        public void MoveNode(int @from, int to) => _root.MoveNode(@from, to);
        public void ReparentNode(LinearTreeNode<T> node, int index) => _root.ReparentNode(node, index);

        public void ClearChildren() => _root.ClearChildren();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _root.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _root).GetEnumerator();
        public int Count => _root.Count;
        public T this[int index] => _root[index];
        public LinearTreeNode<T> NodeAt(int index) => _root.NodeAt(index);

        public IEnumerable<LinearTreeNode<T>> IterateNodes() => _root.IterateNodes();
    }
}