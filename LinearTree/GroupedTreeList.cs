using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearTree
{
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
            IEnumerable<TGroupKey> groups) where TId : struct
        {
            return new GroupedTreeList<T, TId, TSortKey, TGroupKey>(
                selectId, selectParentId, idComparer, selectSortKey, sortKeyComparer, selectGroupKey, groupKeyComparer, groups);
        }
    }
    
    public class GroupedTreeList<T, TId, TSortKey, TGroupKey> where T : class where TId : struct
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

        private readonly Dictionary<TId, TGroupKey> _groupKeyMapping = new Dictionary<TId, TGroupKey>();
        private readonly List<Group> _groups;
        
        public GroupedTreeList(SelectId<T, TId> selectId, 
            SelectParentId<T, TId> selectParentId,
            IdComparer<TId> idComparer, 
            SelectSortKey<T, TSortKey> selectSortKey,
            SortKeyComparer<TSortKey> sortKeyComparer, 
            SelectGroupKey<T, TGroupKey> selectGroupKey,
            GroupKeyComparer<TGroupKey> groupKeyComparer,
            IEnumerable<TGroupKey> groups)
        {
            _selectId = selectId;
            _selectGroupKey = selectGroupKey;
            _groupKeyComparer = groupKeyComparer;

            var set = new HashSet<TGroupKey>();
            _groups = new List<Group>();

            foreach (var g in groups)
            {
                if(!set.Add(g)) throw new ArgumentException("Duplicate groups detected", nameof(groups));

                _groups.Add(new Group(g, new AutoTreeSortedList<T, TId, TSortKey>(
                    selectId, selectParentId, idComparer, selectSortKey, sortKeyComparer)));
            }
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
            }
        }
    }
}