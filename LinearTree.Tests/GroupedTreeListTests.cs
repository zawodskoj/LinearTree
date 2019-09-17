using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Zw.LinearTree;

namespace LinearTree.Tests
{
    public static class GroupedTreeListTestsHelpers
    {
        public static IEnumerable<GroupedTreeItem<A, D>> Index<A, B, C, D>(this GroupedTreeList<A, B, C, D> tree) where B : struct where A : class
        {
            for (var i = 0; i < tree.Count; i++)
                yield return tree[i];
        }
    }

    public class GroupedTreeListTests
    {
        [DebuggerDisplay("Id = {Id}, Parent = {ParentId}, SortKey = {SortKey}, GroupKey = {GroupKey}")]
        public class Item 
        {
            protected bool Equals(Item other)
            {
                return Id == other.Id && ParentId == other.ParentId && SortKey == other.SortKey && GroupKey == other.GroupKey;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Item) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Id;
                    hashCode = (hashCode * 397) ^ ParentId.GetHashCode();
                    hashCode = (hashCode * 397) ^ SortKey;
                    hashCode = (hashCode * 397) ^ GroupKey;
                    return hashCode;
                }
            }

            public Item(int id, int? parentId, int sortKey, int groupKey)
            {
                Id = id;
                ParentId = parentId;
                SortKey = sortKey;
                GroupKey = groupKey;
            }

            public int Id { get; }
            public int? ParentId { get; }
            public int SortKey { get; }
            public int GroupKey { get; }
        }

        private List<Item> GenerateTestItems()
        {
            return new List<Item>
            {
                // @formatter:off
                new Item(00, null, 0, 0),
                    new Item(01, 00, 0, 0),
                new Item(02, null, 1, 0),
                    new Item(03, 02, 0, 1),
                        new Item(04, 03, 0, 0),
                    new Item(05, 02, 1, 0),
                    new Item(06, 02, 2, 0),
                new Item(07, null, 2, 0),
                new Item(08, null, 3, 0),
                    new Item(09, 08, 0, 2),
                        new Item(10, 09, 0, 2),
                            new Item(11, 10, 0, 1),
                new Item(12, null, 4, 4),
                    new Item(13, 12, 0, 4),
                new Item(14, null, 5, 0),
                    new Item(15, 14, 0, 4),
                    new Item(16, 14, 1, 4),
                    
                /*
                 * Actual items disposition
                 *
                 * -- group 0
                 * new Item(04, 03, 0, 0), - on top!
                 * new Item(00, null, 0, 0),
                 *     new Item(01, 00, 0, 0),
                 * new Item(02, null, 1, 0),
                 *     new Item(05, 02, 1, 0),
                 *     new Item(06, 02, 2, 0),
                 * new Item(07, null, 2, 0),
                 * new Item(08, null, 3, 0),
                 * new Item(14, null, 5, 0),
                 * -- group 1
                 * new Item(11, 10, 0, 1), - reverse order due to order of insertions
                 * new Item(03, 02, 0, 1),
                 * -- group 2
                 * new Item(09, 08, 0, 2),
                 *     new Item(10, 09, 0, 2),
                 * -- group 3 - empty
                 * -- group 4
                 * new Item(15, 14, 0, 4), - sort order changed
                 * new Item(16, 14, 1, 4),
                 * new Item(12, null, 4, 4),
                 *     new Item(13, 12, 0, 4),
                 */
                // @formatter:on
            };
        }

        private List<Item> GenerateTestItemsRegrouped()
        {
            return new List<Item>
            {
                // @formatter:off
                // group 0
                new Item(04, 03, 0, 0),
                new Item(00, null, 0, 0),
                    new Item(01, 00, 0, 0),
                new Item(02, null, 1, 0),
                    new Item(05, 02, 1, 0),
                    new Item(06, 02, 2, 0),
                new Item(07, null, 2, 0),
                new Item(08, null, 3, 0),
                new Item(14, null, 5, 0),
                
                // group 1
                new Item(11, 10, 0, 1),
                new Item(03, 02, 0, 1),

                // group 2
                new Item(09, 08, 0, 2),
                    new Item(10, 09, 0, 2),
                    
                // group 3 - empty
                    
                // group 4
                new Item(15, 14, 0, 4),
                new Item(16, 14, 1, 4),
                new Item(12, null, 4, 4),
                    new Item(13, 12, 0, 4),
                // @formatter:on
            };
        }
            
        private GroupedTreeList<Item, int, int, int> GenerateTestTree(GroupedTreeSeparator separator)
        {
            var list = GroupedTreeList<Item>.Create(
                x => x.Id, x => x.ParentId, (x, y) => x == y,
                x => x.SortKey, (lhs, rhs) => lhs - rhs,
                x => x.GroupKey, (lhs, rhs) => lhs == rhs,
                new[] { 0, 1, 2, 3, 4 },
                separator);
            
            foreach (var item in GenerateTestItems())
                list.Upsert(item);

            return list;
        }
        
        private static List<GroupedTreeItem<Item, int>> CreateAutoDispatchedList(GroupedTreeList<Item, int, int, int> tree)
        {
            return TreeTests.CreateAutoDispatchedList<GroupedTreeItem<Item, int>,
                GroupedTreeList<Item, int, int, int>>(tree);
        }

        [Fact]
        public void TestTreeMatchesListRepresentation()
        {
            var withoutSeps = GenerateTestTree(GroupedTreeSeparator.NONE).Select(x => new { i = x.Item, g = x.GroupKey, s = x.IsSeparator }).ToArray();
            var expected = GenerateTestItemsRegrouped().Select(x => new {i = x, g = x.GroupKey, s = false}).ToList();

            Assert.Equal(expected, withoutSeps);
            
            var withAllSeps = GenerateTestTree(GroupedTreeSeparator.ALWAYS).Select(x => new { i = x.Item, g = x.GroupKey, s = x.IsSeparator }).ToArray();
            expected.Insert(0, new { i = (Item) null, g = 0, s = true });
            expected.Insert(10, new { i = (Item) null, g = 1, s = true });
            expected.Insert(13, new { i = (Item) null, g = 2, s = true });
            expected.Insert(16, new { i = (Item) null, g = 3, s = true });
            expected.Insert(17, new { i = (Item) null, g = 4, s = true });

            Assert.Equal(expected, withAllSeps);
            
            var withAutoSeps = GenerateTestTree(GroupedTreeSeparator.NON_EMPTY).Select(x => new { i = x.Item, g = x.GroupKey, s = x.IsSeparator }).ToArray();
            expected.RemoveAt(16);

            Assert.Equal(expected, withAutoSeps);
        }
        
        [Fact]
        public void InsertItemsWithoutSeps()
        {
            var tree = GenerateTestTree(GroupedTreeSeparator.NONE);
            var manualItems = GenerateTestItemsRegrouped();
            var autoItems = CreateAutoDispatchedList(tree);
            
            var item1 = new Item(-1, null, -1, 0);
            tree.Upsert(item1);
            manualItems.Insert(0, item1);
            
            Assert.Equal(manualItems, tree.Select(x => x.Item));
            Assert.Equal(manualItems, tree.Index().Select(x => x.Item));
            Assert.Equal(manualItems, autoItems.Select(x => x.Item));
            
            var item2 = new Item(-2, 9, 1, 2);
            tree.Upsert(item2);
            manualItems.Insert(14, item2);
            
            Assert.Equal(manualItems, tree.Select(x => x.Item));
            Assert.Equal(manualItems, tree.Index().Select(x => x.Item));
            Assert.Equal(manualItems, autoItems.Select(x => x.Item));
            
            var item3 = new Item(-3, 9, 1, 3);
            tree.Upsert(item3);
            manualItems.Insert(15, item3);
            
            Assert.Equal(manualItems, tree.Select(x => x.Item));
            Assert.Equal(manualItems, tree.Index().Select(x => x.Item));
            Assert.Equal(manualItems, autoItems.Select(x => x.Item));
        }

        [Fact]
        public void InsertItemsWithAllSeps()
        {
            var tree = GenerateTestTree(GroupedTreeSeparator.ALWAYS);
            var manualItems = GenerateTestItemsRegrouped().Select(x => new {i = x, s = false, g = x.GroupKey}).ToList();

            // separators
            manualItems.Insert(0, new {i = (Item) null, s = true, g = 0});
            manualItems.Insert(10, new {i = (Item) null, s = true, g = 1});
            manualItems.Insert(13, new {i = (Item) null, s = true, g = 2});
            manualItems.Insert(16, new {i = (Item) null, s = true, g = 3});
            manualItems.Insert(17, new {i = (Item) null, s = true, g = 4});

            var autoItems = CreateAutoDispatchedList(tree);

            var item1 = new Item(-1, null, -1, 0);
            tree.Upsert(item1);
            manualItems.Insert(1, new {i = item1, s = false, g = 0});

            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));

            var item2 = new Item(-2, 9, 1, 2);
            tree.Upsert(item2);
            manualItems.Insert(17, new {i = item2, s = false, g = 2});

            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));

            var item3 = new Item(-3, 9, 1, 3);
            tree.Upsert(item3);
            manualItems.Insert(19, new {i = item3, s = false, g = 3});

            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
        }

        [Fact]
        public void InsertItemsWithAutoSeps()
        {
            var tree = GenerateTestTree(GroupedTreeSeparator.NON_EMPTY);
            var manualItems = GenerateTestItemsRegrouped().Select(x => new { i = x, s = false, g = x.GroupKey }).ToList();
            
            // separators
            manualItems.Insert(0, new {i = (Item) null, s = true, g = 0});
            manualItems.Insert(10, new {i = (Item) null, s = true, g = 1});
            manualItems.Insert(13, new {i = (Item) null, s = true, g = 2});
            manualItems.Insert(16, new {i = (Item) null, s = true, g = 4});
            
            var autoItems = CreateAutoDispatchedList(tree);

            var item1 = new Item(-1, null, -1, 0);
            tree.Upsert(item1);
            manualItems.Insert(1, new { i = item1, s = false, g = 0 });
            
            Assert.Equal(manualItems, tree.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));
            
            var item2 = new Item(-2, 9, 1, 2);
            tree.Upsert(item2);
            manualItems.Insert(17, new { i = item2, s = false, g = 2 });
            
            Assert.Equal(manualItems, tree.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));
            
            var item3 = new Item(-3, 9, 1, 3);
            tree.Upsert(item3);
            manualItems.Insert(18, new {i = (Item) null, s = true, g = 3});
            manualItems.Insert(19, new { i = item3, s = false, g = 3 });
            
            Assert.Equal(manualItems, tree.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));
        }
        

        [Fact]
        public void InsertItemsWithAutoSepsAfterClear()
        {
            var tree = GenerateTestTree(GroupedTreeSeparator.NON_EMPTY);
            var manualItems = GenerateTestItemsRegrouped().Select(x => new { i = x, s = false, g = x.GroupKey }).ToList();
            
            // separators
            manualItems.Insert(0, new {i = (Item) null, s = true, g = 0});
            manualItems.Insert(10, new {i = (Item) null, s = true, g = 1});
            manualItems.Insert(13, new {i = (Item) null, s = true, g = 2});
            manualItems.Insert(16, new {i = (Item) null, s = true, g = 4});
            
            var autoItems = CreateAutoDispatchedList(tree);
            
            Assert.Equal(manualItems, tree.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));

            tree.Clear();
            manualItems.Clear();
            
            Assert.Equal(manualItems, tree.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));

            var item1 = new Item(-1, null, -1, 0);
            tree.Upsert(item1);
            manualItems.Add(new { i = (Item) null, s = true, g = 0 });
            manualItems.Add(new { i = item1, s = false, g = 0 });
            
            Assert.Equal(manualItems, tree.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new { i = x.Item, s = x.IsSeparator, g = x.GroupKey }));
        }
        
        [Fact]
        public void RemoveItemsWithoutSeps()
        {
            var tree = GenerateTestTree(GroupedTreeSeparator.NONE);
            var manualItems = GenerateTestItemsRegrouped();
            var autoItems = CreateAutoDispatchedList(tree);
            
            tree.Delete(new Item(4, null, 0, 0));
            manualItems.RemoveAt(0);
            
            Assert.Equal(manualItems, tree.Select(x => x.Item));
            Assert.Equal(manualItems, tree.Index().Select(x => x.Item));
            Assert.Equal(manualItems, autoItems.Select(x => x.Item));
            
            tree.Delete(new Item(14, null, 0, 0));
            manualItems.RemoveAt(7);
            
            Assert.Equal(manualItems, tree.Select(x => x.Item));
            Assert.Equal(manualItems, tree.Index().Select(x => x.Item));
            Assert.Equal(manualItems, autoItems.Select(x => x.Item));
            
            tree.Delete(new Item(11, null, 0, 0));
            tree.Delete(new Item(03, null, 0, 0));
            manualItems.RemoveAt(7);
            manualItems.RemoveAt(7);
            
            Assert.Equal(manualItems, tree.Select(x => x.Item));
            Assert.Equal(manualItems, tree.Index().Select(x => x.Item));
            Assert.Equal(manualItems, autoItems.Select(x => x.Item));
        }

        [Fact]
        public void RemoveItemsWithAllSeps()
        {
            var tree = GenerateTestTree(GroupedTreeSeparator.ALWAYS);
            var manualItems = GenerateTestItemsRegrouped().Select(x => new {i = x, s = false, g = x.GroupKey}).ToList();

            // separators
            manualItems.Insert(0, new {i = (Item) null, s = true, g = 0});
            manualItems.Insert(10, new {i = (Item) null, s = true, g = 1});
            manualItems.Insert(13, new {i = (Item) null, s = true, g = 2});
            manualItems.Insert(16, new {i = (Item) null, s = true, g = 3});
            manualItems.Insert(17, new {i = (Item) null, s = true, g = 4});

            var autoItems = CreateAutoDispatchedList(tree);
            
            tree.Delete(new Item(4, null, 0, 0));
            manualItems.RemoveAt(1);

            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));

            tree.Delete(new Item(14, null, 0, 0));
            manualItems.RemoveAt(8);

            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));

            tree.Delete(new Item(11, null, 0, 0));
            tree.Delete(new Item(03, null, 0, 0));
            manualItems.RemoveAt(9);
            manualItems.RemoveAt(9);
            
            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
        }

        [Fact]
        public void RemoveItemsWithAutoSeps()
        {
            var tree = GenerateTestTree(GroupedTreeSeparator.NON_EMPTY);
            var manualItems = GenerateTestItemsRegrouped().Select(x => new { i = x, s = false, g = x.GroupKey }).ToList();
            
            // separators
            manualItems.Insert(0, new {i = (Item) null, s = true, g = 0});
            manualItems.Insert(10, new {i = (Item) null, s = true, g = 1});
            manualItems.Insert(13, new {i = (Item) null, s = true, g = 2});
            manualItems.Insert(16, new {i = (Item) null, s = true, g = 4});
            
            var autoItems = CreateAutoDispatchedList(tree);
            
            tree.Delete(new Item(4, null, 0, 0));
            manualItems.RemoveAt(1);

            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));

            tree.Delete(new Item(14, null, 0, 0));
            manualItems.RemoveAt(8);

            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));

            tree.Delete(new Item(11, null, 0, 0));
            tree.Delete(new Item(03, null, 0, 0));
            manualItems.RemoveAt(8);
            manualItems.RemoveAt(8);
            manualItems.RemoveAt(8);
            
            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
        }

        [Fact]
        public void MoveSubtreeWithAutoSeparators()
        {
            var tree = GenerateTestTree(GroupedTreeSeparator.NON_EMPTY);
            var manualItems = GenerateTestItemsRegrouped().Select(x => new { i = x, s = false, g = x.GroupKey }).ToList();
            
            // separators
            manualItems.Insert(0, new {i = (Item) null, s = true, g = 0});
            manualItems.Insert(10, new {i = (Item) null, s = true, g = 1});
            manualItems.Insert(13, new {i = (Item) null, s = true, g = 2});
            manualItems.Insert(16, new {i = (Item) null, s = true, g = 4});
            
            var autoItems = CreateAutoDispatchedList(tree);

            // re-parenting element (to create nesting)
            var item1 = new Item(03, 11, 0, 1);
            tree.Upsert(item1);
            manualItems[12] = new {i = item1, s = false, g = 1};

            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));

            var item2 = new Item(11, null, 0, 3);
            tree.Upsert(item2);
            manualItems.RemoveAt(11);
            manualItems.Insert(15, new { i = (Item) null, s = true, g = 3 });
            manualItems.Insert(16, new { i = item2, s = false, g = 3 });

            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));

            var item3 = new Item(03, 11, 0, 3);
            tree.Upsert(item3);
            manualItems.Insert(17, new { i = item3, s = false, g = 3 });
            manualItems.RemoveAt(11);
            manualItems.RemoveAt(10);
            
            Assert.Equal(manualItems, tree.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, tree.Index().Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
            Assert.Equal(manualItems, autoItems.Select(x => new {i = x.Item, s = x.IsSeparator, g = x.GroupKey}));
        }
    }
}