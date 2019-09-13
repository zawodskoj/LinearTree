using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Schema;
using Xunit;

namespace LinearTree.Tests
{
    public class AutoTreeSortedListTests
    {
        [DebuggerDisplay("Id = {Id}, Parent = {ParentId}, SortKey = {SortKey}")]
        public class Item 
        {
            protected bool Equals(Item other)
            {
                return Id == other.Id && ParentId == other.ParentId && SortKey == other.SortKey;
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
                    return hashCode;
                }
            }

            public Item(int id, int? parentId, int sortKey)
            {
                Id = id;
                ParentId = parentId;
                SortKey = sortKey;
            }

            public int Id { get; }
            public int? ParentId { get; }
            public int SortKey { get; }
        }

        private List<Item> GenerateTestItems()
        {
            return new List<Item>
            {
                // @formatter:off
                new Item(00, null, 0),
                    new Item(01, 00, 0),
                new Item(02, null, 1),
                    new Item(03, 02, 0),
                        new Item(04, 03, 0),
                    new Item(05, 02, 1),
                    new Item(06, 02, 2),
                new Item(07, null, 2),
                new Item(08, null, 3),
                    new Item(09, 08, 0),
                        new Item(10, 09, 0),
                            new Item(11, 10, 0),
                new Item(12, null, 4),
                    new Item(13, 12, 0),
                new Item(14, null, 5),
                    new Item(15, 14, 0),
                    new Item(16, 14, 1),
                // @formatter:on
            };
        }
            
        private AutoTreeSortedList<Item, int, int> GenerateTestTree()
        {
            var list = AutoTreeSortedList<Item>.Create(
                x => x.Id, x => x.ParentId, (x, y) => x == y,
                x => x.SortKey, (lhs, rhs) => lhs - rhs);
            
            foreach (var item in GenerateTestItems())
                list.Upsert(item);

            return list;
        }

        [Fact]
        public void TestTreeMatchesListRepresentation()
        {
            Assert.Equal(Enumerable.Range(0, 17), GenerateTestTree().Select(x => x.Value.Id));
        }

        [Fact]
        public void InsertInRoot()
        {
            var testTree = GenerateTestTree();
            var manualItems = GenerateTestItems();

            var item1 = new Item(-1, null, 0);
            testTree.Upsert(item1);
            manualItems.Insert(0, item1);

            Assert.Equal(manualItems, testTree.Select(x => x.Value));
            
            var item2 = new Item(-2, null, 100);
            testTree.Upsert(item2);
            manualItems.Add(item2);
            
            Assert.Equal(manualItems, testTree.Select(x => x.Value));
        }

        [Fact]
        public void InsertInChild()
        {
            var testTree = GenerateTestTree();
            var manualItems = GenerateTestItems();

            var item1 = new Item(-1, 09, -1);
            testTree.Upsert(item1);
            manualItems.Insert(10, item1);

            Assert.Equal(manualItems, testTree.Select(x => x.Value));
            
            var item2 = new Item(-2, 09, 1);
            testTree.Upsert(item2);
            manualItems.Insert(13, item2);
            
            Assert.Equal(manualItems, testTree.Select(x => x.Value));
        }

        [Fact]
        public void UpdateParent()
        {
            var testTree = GenerateTestTree();
            var manualItems = GenerateTestItems();

            var item = new Item(2, null, 0);
            testTree.Delete(item);
            manualItems.RemoveRange(2, 5);
            manualItems.Insert(0, new Item(3, 2, 0));
            manualItems.Insert(1, new Item(4, 3, 0));
            manualItems.Insert(4, new Item(5, 2, 1));
            manualItems.Insert(5, new Item(6, 2, 2));

            Assert.Equal(manualItems, testTree.Select(x => x.Value));

            testTree.Upsert(item);
            manualItems.RemoveRange(4, 2);
            manualItems.Insert(0, item);
            manualItems.Insert(3, new Item(5, 2, 1));
            manualItems.Insert(4, new Item(6, 2, 2));
            
            Assert.Equal(manualItems, testTree.Select(x => x.Value));
        }

        [Fact]
        public void ReorderingUpdate()
        {
            var testTree = GenerateTestTree();
            var manualItems = GenerateTestItems();

            var item1 = new Item(2, null, -1);
            testTree.Upsert(item1);
            manualItems.RemoveRange(0, 3);
            manualItems.Insert(0, item1);
            manualItems.Insert(5, new Item(0, null, 0));
            manualItems.Insert(6, new Item(1, 0, 0));

            Assert.Equal(manualItems, testTree.Select(x => x.Value));
            
            var item2 = new Item(2, null, 100);
            testTree.Upsert(item2);
            manualItems.RemoveRange(0, 5);
            manualItems.Add(item2);
            manualItems.Add(new Item(3, 2, 0));
            manualItems.Add(new Item(4, 3, 0));
            manualItems.Add(new Item(5, 2, 1));
            manualItems.Add(new Item(6, 2, 2));

            Assert.Equal(manualItems, testTree.Select(x => x.Value));
        }

        [Fact]
        public void StableUpdate()
        {
            var testTree = GenerateTestTree();
            var manualItems = GenerateTestItems();

            var item1 = new Item(-1, null, 1);
            testTree.Upsert(item1);
            manualItems.Insert(2, item1);

            Assert.Equal(manualItems, testTree.Select(x => x.Value));
            
            var item2 = new Item(2, null, 1);
            testTree.Upsert(item2);

            Assert.Equal(manualItems, testTree.Select(x => x.Value));
            
            var item3 = new Item(-2, null, 1);
            testTree.Upsert(item3);
            manualItems.Insert(2, item3);

            Assert.Equal(manualItems, testTree.Select(x => x.Value));
            testTree.Upsert(item2);

            Assert.Equal(manualItems, testTree.Select(x => x.Value));
            
            var item4 = new Item(2, null, 2);
            testTree.Upsert(item4);
            manualItems[4] = item4;
            
            Assert.Equal(manualItems, testTree.Select(x => x.Value));
            
            var item5 = new Item(-1, null, 2);
            testTree.Upsert(item5);
            manualItems[3] = item5;
            
            Assert.Equal(manualItems, testTree.Select(x => x.Value));
            
            var item6 = new Item(2, null, 2);
            testTree.Upsert(item6);
            manualItems[4] = item6;
            
            Assert.Equal(manualItems, testTree.Select(x => x.Value));
        }
    }
}