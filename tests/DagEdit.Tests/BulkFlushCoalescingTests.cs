using Avalonia;
using Xunit;

namespace DagEdit.Tests
{
    /// <summary>
    /// H-1 Bulk flush coalescing 검증 테스트.
    ///
    /// 검증 목표:
    /// 1. bulk add: N×ExecuteAddNode를 outer BeginBatch/EndBatch로 묶으면 ProjectionChanged 1회
    /// 2. bulk del: N×ExecuteDelNode를 outer batch로 묶으면 ProjectionChanged 1회
    /// 3. non-bulk 단건 add/del: 기존 semantics 유지 (단건당 1회 Flush)
    /// 4. undo/redo: 기존 batch semantics 유지 (1회 Flush per undo/redo)
    /// 5. move: stable reference (같은 NodeViewItem object) 및 bounds 정합성 유지
    /// </summary>
    public class BulkFlushCoalescingTests
    {
        // ─── Helpers ──────────────────────────────────────────────────────────
        private static Guid AddNodeAndGetId(DagEditorViewModel vm, double x, double y)
        {
            vm.ExecuteAddNode(new Point(x, y));
            return vm.Items[^1].NodeItem!.NodeId!.Value;
        }

        // ─── Bulk add coalescing ───────────────────────────────────────────────

        [Fact]
        public void BulkAdd_OuterBatch_SingleProjectionChanged()
        {
            using var vm = new DagEditorViewModel();
            var flushCount = 0;
            vm.ViewerAdapter.ProjectionChanged += (_, _) => flushCount++;

            vm.BeginBatch();
            try
            {
                vm.ExecuteAddNode(new Point(0, 0));
                vm.ExecuteAddNode(new Point(100, 0));
                vm.ExecuteAddNode(new Point(200, 0));
            }
            finally
            {
                vm.EndBatch();
            }

            Assert.Equal(1, flushCount);
        }

        [Fact]
        public void BulkAdd_OuterBatch_AllNodesReflectedInSnapshot()
        {
            using var vm = new DagEditorViewModel();

            vm.BeginBatch();
            try
            {
                vm.ExecuteAddNode(new Point(0, 0));
                vm.ExecuteAddNode(new Point(100, 0));
                vm.ExecuteAddNode(new Point(200, 0));
            }
            finally
            {
                vm.EndBatch();
            }

            Assert.Equal(3, vm.ViewerAdapter.Snapshots.Count);
        }

        [Fact]
        public void UncoalescedAdd_NOperations_NFlushes()
        {
            // batch 없이 N번 호출 → N회 Flush (기준선: outer batch 없을 때 동작)
            using var vm = new DagEditorViewModel();
            var flushCount = 0;
            vm.ViewerAdapter.ProjectionChanged += (_, _) => flushCount++;

            vm.ExecuteAddNode(new Point(0, 0));
            vm.ExecuteAddNode(new Point(100, 0));
            vm.ExecuteAddNode(new Point(200, 0));

            Assert.Equal(3, flushCount);
            Assert.Equal(3, vm.ViewerAdapter.Snapshots.Count);
        }

        // ─── Bulk remove coalescing ────────────────────────────────────────────

        [Fact]
        public void BulkDel_OuterBatch_SingleProjectionChanged()
        {
            using var vm = new DagEditorViewModel();
            Guid id1 = AddNodeAndGetId(vm, 0, 0);
            Guid id2 = AddNodeAndGetId(vm, 100, 0);
            Guid id3 = AddNodeAndGetId(vm, 200, 0);
            var flushCount = 0;
            vm.ViewerAdapter.ProjectionChanged += (_, _) => flushCount++;

            vm.BeginBatch();
            try
            {
                vm.ExecuteDelNode(id1);
                vm.ExecuteDelNode(id2);
                vm.ExecuteDelNode(id3);
            }
            finally
            {
                vm.EndBatch();
            }

            Assert.Equal(1, flushCount);
        }

        [Fact]
        public void BulkDel_OuterBatch_SnapshotBecomesEmpty()
        {
            using var vm = new DagEditorViewModel();
            Guid id1 = AddNodeAndGetId(vm, 0, 0);
            Guid id2 = AddNodeAndGetId(vm, 100, 0);
            Guid id3 = AddNodeAndGetId(vm, 200, 0);

            vm.BeginBatch();
            try
            {
                vm.ExecuteDelNode(id1);
                vm.ExecuteDelNode(id2);
                vm.ExecuteDelNode(id3);
            }
            finally
            {
                vm.EndBatch();
            }

            Assert.Empty(vm.ViewerAdapter.Snapshots);
        }

        // ─── Non-bulk 단건 regression ──────────────────────────────────────────

        [Fact]
        public void SingleAdd_NoOuterBatch_ExactlyOneFlush()
        {
            using var vm = new DagEditorViewModel();
            var flushCount = 0;
            vm.ViewerAdapter.ProjectionChanged += (_, _) => flushCount++;

            vm.ExecuteAddNode(new Point(0, 0));

            Assert.Equal(1, flushCount);
            Assert.Single(vm.ViewerAdapter.Snapshots);
        }

        [Fact]
        public void SingleDel_NoOuterBatch_ExactlyOneFlush()
        {
            using var vm = new DagEditorViewModel();
            Guid id = AddNodeAndGetId(vm, 0, 0);
            var flushCount = 0;
            vm.ViewerAdapter.ProjectionChanged += (_, _) => flushCount++;

            vm.ExecuteDelNode(id);

            Assert.Equal(1, flushCount);
            Assert.Empty(vm.ViewerAdapter.Snapshots);
        }

        // ─── Undo/Redo batch semantics ─────────────────────────────────────────

        [Fact]
        public void Undo_AfterAdd_RemovesFromSnapshot_OneFlush()
        {
            using var vm = new DagEditorViewModel();
            vm.ExecuteAddNode(new Point(0, 0));
            var flushCount = 0;
            vm.ViewerAdapter.ProjectionChanged += (_, _) => flushCount++;

            vm.Undo();

            Assert.Equal(1, flushCount);
            Assert.Empty(vm.ViewerAdapter.Snapshots);
        }

        [Fact]
        public void Redo_AfterUndoAdd_RestoresSnapshot_OneFlush()
        {
            using var vm = new DagEditorViewModel();
            vm.ExecuteAddNode(new Point(0, 0));
            vm.Undo();
            var flushCount = 0;
            vm.ViewerAdapter.ProjectionChanged += (_, _) => flushCount++;

            vm.Redo();

            Assert.Equal(1, flushCount);
            Assert.Single(vm.ViewerAdapter.Snapshots);
        }

        [Fact]
        public void Undo_AfterDel_RestoresSnapshot_OneFlush()
        {
            using var vm = new DagEditorViewModel();
            Guid id = AddNodeAndGetId(vm, 50, 50);
            vm.ExecuteDelNode(id);
            var flushCount = 0;
            vm.ViewerAdapter.ProjectionChanged += (_, _) => flushCount++;

            vm.Undo();

            Assert.Equal(1, flushCount);
            Assert.Single(vm.ViewerAdapter.Snapshots);
        }

        // ─── Move stable projection ────────────────────────────────────────────

        [Fact]
        public void Move_StableProjectionRef_SameObject()
        {
            using var vm = new DagEditorViewModel();
            Guid id = AddNodeAndGetId(vm, 0, 0);
            NodeViewItem before = vm.ViewerAdapter.Snapshots[id];

            vm.PushMoveNode(id, new Point(0, 0), new Point(100, 100));

            NodeViewItem after = vm.ViewerAdapter.Snapshots[id];
            Assert.Same(before, after);
        }

        [Fact]
        public void Move_UpdatesBoundsInSnapshot()
        {
            using var vm = new DagEditorViewModel();
            Guid id = AddNodeAndGetId(vm, 0, 0);

            vm.PushMoveNode(id, new Point(0, 0), new Point(150, 75));

            NodeViewItem item = vm.ViewerAdapter.Snapshots[id];
            Assert.Equal(150, item.Bounds.X);
            Assert.Equal(75, item.Bounds.Y);
        }

        [Fact]
        public void Move_ExactlyOneFlush()
        {
            using var vm = new DagEditorViewModel();
            Guid id = AddNodeAndGetId(vm, 0, 0);
            var flushCount = 0;
            vm.ViewerAdapter.ProjectionChanged += (_, _) => flushCount++;

            vm.PushMoveNode(id, new Point(0, 0), new Point(100, 100));

            Assert.Equal(1, flushCount);
        }
    }
}
