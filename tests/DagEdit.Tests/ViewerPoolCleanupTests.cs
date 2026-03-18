using Avalonia;
using Xunit;

namespace DagEdit.Tests
{
    /// <summary>
    /// H-2 pool cleanup — NodeViewItemVisualFactory._pool cleanup 검증.
    ///
    /// 검증 목표:
    /// 1. RemoveFromPool: 해당 item이 _pool에서 제거된다
    /// 2. RemoveFromPool: _pool에 없는 item은 no-op (예외 없음)
    /// 3. ItemRemoved 이벤트: OnNodeRemoved 시 실제 제거된 item 포함
    /// 4. ItemRemoved 이벤트: nodeId가 없으면 발생하지 않음
    /// 5. ItemRemoved wiring: adapter.ItemRemoved → factory.RemoveFromPool 연결 시
    ///    OnNodeRemoved 후 PoolCount가 감소한다
    /// 6. H-1 batch 영향 없음: BeginBatch/EndBatch 중 remove가 일어나도 pool cleanup은 즉시
    /// </summary>
    public class ViewerPoolCleanupTests
    {
        private static DagNode ValidNode(double x = 0, double y = 0) =>
            new() { NodeId = Guid.NewGuid(), Location = new Point(x, y) };

        // ─── NodeViewItemVisualFactory.RemoveFromPool ─────────────────────────

        [Fact]
        public void RemoveFromPool_AfterRealize_PoolCountDecreases()
        {
            var factory = new NodeViewItemVisualFactory();
            var item = NodeViewItem.From(ValidNode(10, 20))!;

            factory.Realize(item, false); // pool에 추가
            Assert.Equal(1, factory.PoolCount);

            factory.RemoveFromPool(item);
            Assert.Equal(0, factory.PoolCount);
        }

        [Fact]
        public void RemoveFromPool_UnknownItem_IsNoOp()
        {
            var factory = new NodeViewItemVisualFactory();
            var item = NodeViewItem.From(ValidNode())!;

            // pool에 없는 item을 remove → 예외 없이 no-op
            var ex = Record.Exception(() => factory.RemoveFromPool(item));
            Assert.Null(ex);
            Assert.Equal(0, factory.PoolCount);
        }

        [Fact]
        public void RemoveFromPool_DoesNotAffectOtherItems()
        {
            var factory = new NodeViewItemVisualFactory();
            var item1 = NodeViewItem.From(ValidNode(0, 0))!;
            var item2 = NodeViewItem.From(ValidNode(100, 100))!;

            factory.Realize(item1, false);
            factory.Realize(item2, false);
            Assert.Equal(2, factory.PoolCount);

            factory.RemoveFromPool(item1);
            Assert.Equal(1, factory.PoolCount);

            // item2는 여전히 pool에 있어야 한다
            var result = factory.Realize(item2, false);
            Assert.NotNull(result);
            Assert.Equal(1, factory.RealizeHitCount); // pool hit
        }

        // ─── DagViewerProjectionAdapter.ItemRemoved event ────────────────────

        [Fact]
        public void OnNodeRemoved_ExistingNode_FiresItemRemovedWithCorrectItem()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode(100, 200);
            adapter.OnNodeAdded(node);

            NodeViewItem? firedItem = null;
            adapter.ItemRemoved += (_, item) => firedItem = item;

            adapter.OnNodeRemoved(node.NodeId!.Value);

            Assert.NotNull(firedItem);
            Assert.Equal(node.NodeId!.Value, firedItem!.NodeId);
        }

        [Fact]
        public void OnNodeRemoved_UnknownNodeId_DoesNotFireItemRemoved()
        {
            var adapter = new DagViewerProjectionAdapter();
            int callCount = 0;
            adapter.ItemRemoved += (_, _) => callCount++;

            adapter.OnNodeRemoved(Guid.NewGuid()); // 없는 id

            Assert.Equal(0, callCount);
        }

        // ─── End-to-end wiring: adapter.ItemRemoved → factory.RemoveFromPool ──

        [Fact]
        public void Wiring_OnNodeRemoved_PoolCountDecreases()
        {
            var adapter = new DagViewerProjectionAdapter();
            var factory = new NodeViewItemVisualFactory();

            // MainWindow.OnLoaded 에서 하는 wiring 패턴
            adapter.ItemRemoved += (_, item) => factory.RemoveFromPool(item);

            var node = ValidNode(50, 60);
            adapter.OnNodeAdded(node);

            // VCA realize 시뮬레이션: projection item을 factory에 직접 realize
            var projectionItem = adapter.Snapshots[node.NodeId!.Value];
            factory.Realize(projectionItem, false);
            Assert.Equal(1, factory.PoolCount);

            // node remove → ItemRemoved 발생 → factory pool cleanup
            adapter.OnNodeRemoved(node.NodeId!.Value);
            Assert.Equal(0, factory.PoolCount);
        }

        [Fact]
        public void Wiring_AddRemoveAdd_SameId_NewItemInPool()
        {
            var adapter = new DagViewerProjectionAdapter();
            var factory = new NodeViewItemVisualFactory();
            adapter.ItemRemoved += (_, item) => factory.RemoveFromPool(item);

            var node = ValidNode(0, 0);
            adapter.OnNodeAdded(node);
            var first = adapter.Snapshots[node.NodeId!.Value];
            factory.Realize(first, false);

            adapter.OnNodeRemoved(node.NodeId!.Value);
            Assert.Equal(0, factory.PoolCount);

            // 같은 nodeId로 다시 추가 → 새 NodeViewItem (different ref)
            node.Location = new Point(200, 300);
            adapter.OnNodeAdded(node);
            var second = adapter.Snapshots[node.NodeId!.Value];

            Assert.NotSame(first, second); // new object (remove→add creates new ref)
            Assert.Equal(0, factory.PoolCount); // 아직 realize 안 됨
        }

        // ─── H-1 batch 영향 없음 ──────────────────────────────────────────────

        [Fact]
        public void Wiring_InsideBatch_PoolCleanupIsImmediate()
        {
            // pool cleanup은 batch와 무관하게 OnNodeRemoved 시점에 즉시 발생해야 한다.
            // (Flush는 deferred이지만 ItemRemoved는 즉시)
            var adapter = new DagViewerProjectionAdapter();
            var factory = new NodeViewItemVisualFactory();
            adapter.ItemRemoved += (_, item) => factory.RemoveFromPool(item);

            var node = ValidNode();
            adapter.OnNodeAdded(node);
            var projectionItem = adapter.Snapshots[node.NodeId!.Value];
            factory.Realize(projectionItem, false);

            adapter.BeginBatch();
            adapter.OnNodeRemoved(node.NodeId!.Value);

            // batch 안에서도 pool cleanup은 즉시 발생
            Assert.Equal(0, factory.PoolCount);

            adapter.EndBatch(); // Flush는 여기서 발생
            Assert.Equal(0, factory.PoolCount); // 여전히 0
        }
    }
}
