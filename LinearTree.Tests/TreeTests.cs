using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zw.LinearTree;

namespace LinearTree.Tests
{
    public class TreeTests
    {
        public static List<T> CreateAutoDispatchedList<T, TTree>(TTree tree) where TTree : IReadOnlyList<T>, IDispatchingCollectionChanges<T>
        {
            var list = new List<T>(tree);

            tree.CollectionChanged += (_, change) =>
            {
                switch (change.Type)
                {
                    case CollectionChangeType.INSERT:
                        list.InsertRange(change.DestinationIndex, tree.Skip(change.DestinationIndex).Take(change.Count));
                        break;
                    case CollectionChangeType.REMOVE:
                        list.RemoveRange(change.SourceIndex, change.Count);
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
                                var el = list[i];
                                list.RemoveAt(i);
                                list.Insert(j, el);
                            }
                        }
                        else
                        {
                            for (int i = src, j = dst; i < src + n; i++, j++)
                            {
                                var el = list[i];
                                list.RemoveAt(i);
                                list.Insert(j, el);
                            }
                        }
                        
                        break;
                    case CollectionChangeType.REPLACE:
                        for (var i = change.SourceIndex; i < change.SourceIndex + change.Count; i++)
                            list[i] = tree[i];
                        break;
                }
            };

            return list;
        }

        private List<T> CreateAutoDispatchedList<T>(LinearTree<T> tree) =>
            CreateAutoDispatchedList<T, LinearTree<T>>(tree);

        private LinearTree<int> GenerateTestTree()
        {
            var tree = new LinearTree<int>();
            
            // @formatter:off
            // ReSharper disable UnusedVariable
            var n00 = tree.InsertNode(00, 0);
                var n01 = n00.InsertNode(01, 0);
            var n02 = tree.InsertNode(02, 1);
                var n03 = n02.InsertNode(03, 0);
                    var n04 = n03.InsertNode(04, 0);
                var n05 = n02.InsertNode(05, 1);
                var n06 = n02.InsertNode(06, 2);
            var n07 = tree.InsertNode(07, 2);
            var n08 = tree.InsertNode(08, 3);
                var n09 = n08.InsertNode(09, 0);
                    var n10 = n09.InsertNode(10, 0);
                        var n11 = n10.InsertNode(11, 0);
            var n12 = tree.InsertNode(12, 4);
                var n13 = n12.InsertNode(13, 0);
            var n14 = tree.InsertNode(14, 5);
                var n15 = n14.InsertNode(15, 0);
                var n16 = n14.InsertNode(16, 1);
            // ReSharper restore UnusedVariable
            // @formatter:on

            return tree;
        }

        [Fact]
        public void TestTreeMatchesListRepresentation()
        {
            Assert.Equal(Enumerable.Range(0, 17), GenerateTestTree());
        }

        [Fact]
        public void AutoDispatchedInsertMatchesTree()
        {
            var testTree = GenerateTestTree();
            var autoList = CreateAutoDispatchedList(testTree);
            var manualList = Enumerable.Range(0, 17).ToList();

            testTree.Children[4].InsertNode(-1, 1);
            manualList.Insert(14, -1);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            testTree.InsertNode(-2, 6);
            manualList.Insert(18, -2);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
        }

        [Fact]
        public void AutoDispatchedRemoveMatchesTree()
        {
            var testTree = GenerateTestTree();
            var autoList = CreateAutoDispatchedList(testTree);
            var manualList = Enumerable.Range(0, 17).ToList();

            testTree.Children[4].RemoveNode(0);
            manualList.RemoveAt(13);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            testTree.RemoveNode(1);
            manualList.RemoveRange(2, 5);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
        }
        
        [Fact]
        public void AutoDispatchedMoveMatchesTree()
        {
            var testTree = GenerateTestTree();
            var autoList = CreateAutoDispatchedList(testTree);
            var manualList = Enumerable.Range(0, 17).ToList();

            testTree.MoveNode(5, 3);
            manualList.Insert(8, 14);
            manualList.Insert(9, 15);
            manualList.Insert(10, 16);
            manualList.RemoveRange(17, 3);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            testTree.Children[1].MoveNode(0, 1);
            manualList.RemoveRange(3, 2);
            manualList.Insert(4, 3);
            manualList.Insert(5, 4);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            testTree.Children[1].MoveNode(1, 0);
            manualList.RemoveRange(4, 2);
            manualList.Insert(3, 3);
            manualList.Insert(4, 4);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            testTree.MoveNode(4, 4);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
        }

        [Fact]
        public void AutoDispatchedReplaceMatchesTree()
        {
            var testTree = GenerateTestTree();
            var autoList = CreateAutoDispatchedList(testTree);
            var manualList = Enumerable.Range(0, 17).ToList();

            testTree.Children[4].Value = -1;
            manualList[12] = -1;

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            testTree.Children[1].Children[2].Value = -2;
            manualList[6] = -2;

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
        }

        [Fact]
        public void AutoDispatchedReparentingMatchesTree()
        {
            var testTree = GenerateTestTree();
            var autoList = CreateAutoDispatchedList(testTree);
            var manualList = Enumerable.Range(0, 17).ToList();
            
            // 00
            //     01
            // 02
            //     03
            //         04
            //     05
            //     06
            // 07
            // 08
            //     09
            //         10
            //             11
            // 12
            //     13
            // 14
            //     15
            //     16

            testTree.Children[2].ReparentNode(testTree.Children[3].Children[0], 0);
            manualList.RemoveAt(8);
            manualList.Insert(11, 8);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            // 00
            //     01
            // 02
            //     03
            //         04
            //     05
            //     06
            // 07
            //     09
            //         10
            //             11
            // 08
            // 12
            //     13
            // 14
            //     15
            //     16
            
            testTree.Children[1].ReparentNode(testTree.Children[1].Children[0].Children[0], 0);
            manualList.RemoveAt(4);
            manualList.Insert(3, 4);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            // 00
            //     01
            // 02
            //     04
            //     03
            //     05
            //     06
            // 07
            //     09
            //         10
            //             11
            // 08
            // 12
            //     13
            // 14
            //     15
            //     16
            
            testTree.Children[1].ReparentNode(testTree.Children[0].Children[0], 0);
            manualList.RemoveAt(1);
            manualList.Insert(2, 1);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            
            // 00
            // 02
            //     01
            //     04
            //     03
            //     05
            //     06
            // 07
            //     09
            //         10
            //             11
            // 08
            // 12
            //     13
            // 14
            //     15
            //     16
            
            testTree.Children[2].Children[0].Children[0].Children[0].ReparentNode(testTree.Children[1], 0);
            var copy = manualList.Skip(1).Take(6).ToArray();
            manualList.RemoveRange(1, 6);
            manualList.InsertRange(5, copy);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            // 00
            // 07
            //     09
            //         10
            //             11
            //                 02
            //                     01
            //                     04
            //                     03
            //                     05
            //                     06
            // 08
            // 12
            //     13
            // 14
            //     15
            //     16
            
            testTree.ReparentNode(testTree.Children[1].Children[0], 0);
            var copy2 = manualList.Skip(2).Take(9).ToArray();
            manualList.RemoveRange(2, 9);
            manualList.InsertRange(0, copy2);

            Assert.Equal(manualList.Count, testTree.Count);
            Assert.Equal(manualList, testTree);
            Assert.Equal(manualList, autoList);
            
            // 09
            //     10
            //         11
            //             02
            //                 01
            //                 04
            //                 03
            //                 05
            //                 06
            // 00
            // 07
            // 08
            // 12
            //     13
            // 14
            //     15
            //     16
        }
    }
}