using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LinearTree
{
    public enum GroupedTreeSeparator { NONE, ALWAYS, NON_EMPTY }
    
    public delegate TGroupKey SelectGroupKey<T, TGroupKey>(T item) where T : class;
    public delegate bool GroupKeyComparer<TGroupKey>(TGroupKey lhs, TGroupKey rhs);

    public static class GroupedTreeList<T> where T : class
    {  
        public static GroupedTreeList<T, TId, TSortKey, TGroupKey> Create<TId, TSortKey, TGroupKey>(
            SelectId<T, TId> selectId,
            SelectParentId<T, TId> selectParentId,
            IdComparer<TId> idComparer,
            SelectSortKey<T, TSortKey> selectSortKey,
            SortKeyComparer<TSortKey> sortKeyComparer,
            SelectGroupKey<T, TGroupKey> selectGroupKey,
            GroupKeyComparer<TGroupKey> groupKeyComparer,
            IEnumerable<TGroupKey> groups,
            GroupedTreeSeparator separator) where TId : struct
        {
            return new GroupedTreeList<T, TId, TSortKey, TGroupKey>(
                selectId, selectParentId, idComparer, selectSortKey, sortKeyComparer, selectGroupKey, groupKeyComparer, groups,
                separator);
        }
    }

    public struct GroupedTreeItem<T, TGroupKey> where T : class
    {
        public T Item { get; }
        public int Depth { get; }
        public bool IsSeparator { get; }
        public TGroupKey GroupKey { get; }

        public GroupedTreeItem(T item, int depth, TGroupKey groupKey)
        {
            Item = item;
            Depth = depth;
            GroupKey = groupKey;
            IsSeparator = false;
        }
        
        private GroupedTreeItem(TGroupKey groupKey)
        {
            Item = null;
            Depth = 0;
            GroupKey = groupKey;
            IsSeparator = true;
        }
        
        public static GroupedTreeItem<T, TGroupKey> Separator(TGroupKey groupKey)
            => new GroupedTreeItem<T, TGroupKey>(groupKey); 
    }
    
    public class GroupedTreeList<T, TId, TSortKey, TGroupKey> : 
        IReadOnlyList<GroupedTreeItem<T, TGroupKey>>,
        IDispatchingCollectionChanges<GroupedTreeItem<T, TGroupKey>> 
        where T : class where TId : struct
    {
        private struct Group
        {
            public Group(TGroupKey key, AutoTreeSortedList<T, TId, TSortKey> list)
            {
                Key = key;
                List = list;
            }

            public TGroupKey Key { get; }
            public AutoTreeSortedList<T, TId, TSortKey> List { get; }
        }
        
        private readonly SelectId<T, TId> _selectId;
        private readonly SelectGroupKey<T, TGroupKey> _selectGroupKey;
        private readonly GroupKeyComparer<TGroupKey> _groupKeyComparer;
        private readonly GroupedTreeSeparator _separator;

        private readonly Dictionary<TId, TGroupKey> _groupKeyMapping = new Dictionary<TId, TGroupKey>();
        private readonly List<Group> _groups;
        private int _count;
        
        public GroupedTreeList(SelectId<T, TId> selectId, 
            SelectParentId<T, TId> selectParentId,
            IdComparer<TId> idComparer, 
            SelectSortKey<T, TSortKey> selectSortKey,
            SortKeyComparer<TSortKey> sortKeyComparer, 
            SelectGroupKey<T, TGroupKey> selectGroupKey,
            GroupKeyComparer<TGroupKey> groupKeyComparer,
            IEnumerable<TGroupKey> groups,
            GroupedTreeSeparator separator)
        {
            _selectId = selectId;
            _selectGroupKey = selectGroupKey;
            _groupKeyComparer = groupKeyComparer;
            _separator = separator;

            var set = new HashSet<TGroupKey>();
            _groups = new List<Group>();

            var ix = 0;
            
            foreach (var g in groups)
            {
                if (!set.Add(g)) throw new ArgumentException("Duplicate groups detected", nameof(groups));

                var tree = new AutoTreeSortedList<T, TId, TSortKey>(
                    selectId, selectParentId, idComparer, selectSortKey, sortKeyComparer);

                var localIx = ix;
                tree.CollectionChanged += (_, change) => ReceiveCollectionChanged(localIx, tree, g, change);
                
                _groups.Add(new Group(g, tree));

                if (separator == GroupedTreeSeparator.ALWAYS)
                    _count++;

                ix++;
            }
        }

        private void ReceiveCollectionChanged(
            int index, 
            AutoTreeSortedList<T, TId, TSortKey> tree, 
            TGroupKey groupKey,
            CollectionChange change)
        {
            var treeOffset = 0;
            var alwaysHasSep = _separator == GroupedTreeSeparator.ALWAYS;
            var hasAutoSep = _separator == GroupedTreeSeparator.NON_EMPTY;

            for (var i = 0; i < index; i++)
            {
                var count = _groups[i].List.Count;
                var hasSep = alwaysHasSep || (hasAutoSep && count > 0);
                
                treeOffset += count;
                if (hasSep) 
                    treeOffset++;
            }

            switch (change.Type)
            {
                case CollectionChangeType.MOVE:
                case CollectionChangeType.REPLACE:
                {
                    var sepOfs = alwaysHasSep || (hasAutoSep && tree.Count > 0) ? 1 : 0; // should be 1 in any cases?
                    change = new CollectionChange(
                        change.Type,
                        change.SourceIndex + treeOffset + sepOfs,
                        change.DestinationIndex + treeOffset + sepOfs,
                        change.Count);
                    break;
                }

                case CollectionChangeType.INSERT:
                {
                    var isInsertedToEmpty = change.Count == tree.Count && tree.Count > 0;
                    
                    if (isInsertedToEmpty && hasAutoSep)
                    {
                        change = new CollectionChange(
                            CollectionChangeType.INSERT,
                            -1, 
                            change.DestinationIndex + treeOffset,
                            change.Count + 1);
                    }
                    else
                    {
                        var sepOfs = alwaysHasSep || (hasAutoSep && tree.Count > 0) ? 1 : 0;
                        
                        change = new CollectionChange(
                            CollectionChangeType.INSERT,
                            -1, 
                            change.DestinationIndex + treeOffset + sepOfs,
                            change.Count);
                    }
                    
                    break;
                }

                case CollectionChangeType.REMOVE:
                {
                    var isRemovedAll = tree.Count == 0 && change.Count > 0;
                    if (isRemovedAll && hasAutoSep)
                    {
                        change = new CollectionChange(
                            CollectionChangeType.REMOVE,
                            change.SourceIndex + treeOffset,
                            -1, 
                            change.Count + 1);
                    }
                    else
                    {
                        var sepOfs = alwaysHasSep || (hasAutoSep && tree.Count > 0) ? 1 : 0;
                        
                        change = new CollectionChange(
                            CollectionChangeType.REMOVE,
                            change.SourceIndex + treeOffset + sepOfs,
                            -1, 
                            change.Count);
                    }
                    
                    break;
                }
            }
            
            CollectionChanged?.Invoke(this, change);
        }

        public void Upsert(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var id = _selectId(item);
            
            var group = _selectGroupKey(item);
            var groupList = _groups.Single(x => _groupKeyComparer(x.Key, group));
            
            if (_groupKeyMapping.TryGetValue(id, out var existingGroup))
            {
                if (_groupKeyComparer(existingGroup, group))
                {
                    groupList.List.Upsert(item);
                }
                else
                {
                    var existingGroupList = _groups.Single(x => _groupKeyComparer(x.Key, existingGroup));
                    
                    existingGroupList.List.Delete(item);
                    groupList.List.Upsert(item);

                    _groupKeyMapping[id] = group;
                }
            }
            else
            {
                groupList.List.Upsert(item);
                _groupKeyMapping.Add(id, group);
                _count++;
                
                if (_separator == GroupedTreeSeparator.NON_EMPTY && groupList.List.Count == 1)
                    _count++;
            }
        }

        public void Delete(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var id = _selectId(item);

            if (_groupKeyMapping.Remove(id, out var existingGroup))
            {
                var existingGroupList = _groups.Single(x => _groupKeyComparer(x.Key, existingGroup));
                existingGroupList.List.Delete(item);
                
                _groupKeyMapping.Remove(id);
                _count--;
                
                if (_separator == GroupedTreeSeparator.NON_EMPTY && existingGroupList.List.Count == 0)
                    _count--;
            }
        }
        
        public IEnumerator<GroupedTreeItem<T, TGroupKey>> GetEnumerator()
        {
            foreach (var list in _groups)
            {
                if (_separator == GroupedTreeSeparator.ALWAYS ||
                    (_separator == GroupedTreeSeparator.NON_EMPTY && list.List.Count > 0))
                    yield return GroupedTreeItem<T, TGroupKey>.Separator(list.Key);
                
                foreach (var item in list.List)
                {
                    yield return new GroupedTreeItem<T, TGroupKey>(item.Value, item.Level, list.Key);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => _count;

        public GroupedTreeItem<T, TGroupKey> this[int index]
        {
            get
            {
                var cnt = 0;

                if (index < 0 || index >= _count)
                    throw new IndexOutOfRangeException();
                
                foreach (var list in _groups)
                {
                    var count = list.List.Count;

                    if (count == 0)
                    {
                        if (_separator == GroupedTreeSeparator.ALWAYS)
                            return GroupedTreeItem<T, TGroupKey>.Separator(list.Key);
                        
                        continue;
                    }

                    var hasSep = _separator != GroupedTreeSeparator.NONE;
                    var sepOfs = hasSep ? 1 : 0;
                    if (index - cnt < count + sepOfs)
                    {
                        if (index == cnt && hasSep)
                            return GroupedTreeItem<T, TGroupKey>.Separator(list.Key);

                        var node = list.List[index - cnt - sepOfs];
                        return new GroupedTreeItem<T, TGroupKey>(node.Value, node.Level, list.Key);
                    }

                    cnt += count + sepOfs;
                }
                
                throw new IndexOutOfRangeException("Should never happen");
            }
        }

        public event EventHandler<CollectionChange> CollectionChanged;
    }
} 