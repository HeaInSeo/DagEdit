using System;
using Avalonia;
using DagEdit;
using Xunit;

namespace DagEdit.Tests
{
    /// <summary>
    /// Phase 1 Viewer spike — DagViewerProjectionAdapter projection trigger 검증.
    ///
    /// 검증 목표:
    /// 1. OnNodeAdded → 스냅샷 추가
    /// 2. OnNodeRemoved → 스냅샷 제거
    /// 3. OnNodeMoved → 스냅샷 위치 갱신 (불변 재생성)
    /// 4. Flush() → ProjectionChanged 1회 발생 (보류 있을 때)
    /// 5. Flush() → 보류 없으면 발생 안 함
    /// 6. 여러 OnNode* 후 Flush() 1회 → ProjectionChanged 1회
    /// 7. Flush() 후 재Flush() → 추가 발생 없음
    /// 8. null guard (NodeId/Location null)
    /// </summary>
    public class DagViewerProjectionAdapterTests
    {
        private static DagNode ValidNode(double x = 0, double y = 0) =>
            new DagNode { NodeId = Guid.NewGuid(), Location = new Point(x, y) };

        [Fact]
        public void OnNodeAdded_IncreasesSnapshotCount()
        {
            var adapter = new DagViewerProjectionAdapter();

            adapter.OnNodeAdded(ValidNode());

            Assert.Single(adapter.Snapshots);
        }

        [Fact]
        public void OnNodeRemoved_DecreasesSnapshotCount()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode();
            adapter.OnNodeAdded(node);

            adapter.OnNodeRemoved(node.NodeId!.Value);

            Assert.Empty(adapter.Snapshots);
        }

        [Fact]
        public void OnNodeMoved_UpdatesBoundsInSnapshot()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode(100, 200);
            adapter.OnNodeAdded(node);

            node.Location = new Point(300, 400);
            adapter.OnNodeMoved(node);

            var snapshot = adapter.Snapshots[node.NodeId!.Value];
            Assert.Equal(300, snapshot.Bounds.X);
            Assert.Equal(400, snapshot.Bounds.Y);
        }

        [Fact]
        public void OnNodeMoved_SnapshotCountUnchanged()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode(0, 0);
            adapter.OnNodeAdded(node);

            node.Location = new Point(50, 50);
            adapter.OnNodeMoved(node);

            Assert.Single(adapter.Snapshots);
        }

        [Fact]
        public void Flush_RaisesProjectionChanged_WhenPending()
        {
            var adapter = new DagViewerProjectionAdapter();
            adapter.OnNodeAdded(ValidNode());
            int callCount = 0;
            adapter.ProjectionChanged += (_, _) => callCount++;

            adapter.Flush();

            Assert.Equal(1, callCount);
        }

        [Fact]
        public void Flush_DoesNotRaise_WhenNoPendingChange()
        {
            var adapter = new DagViewerProjectionAdapter();
            int callCount = 0;
            adapter.ProjectionChanged += (_, _) => callCount++;

            adapter.Flush();

            Assert.Equal(0, callCount);
        }

        [Fact]
        public void MultipleChanges_SingleFlush_RaisesOnce()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node1 = ValidNode(0, 0);
            var node2 = ValidNode(100, 100);
            adapter.OnNodeAdded(node1);
            adapter.OnNodeAdded(node2);
            node1.Location = new Point(50, 50);
            adapter.OnNodeMoved(node1);
            int callCount = 0;
            adapter.ProjectionChanged += (_, _) => callCount++;

            adapter.Flush();

            Assert.Equal(1, callCount);
        }

        [Fact]
        public void Flush_AfterFlush_DoesNotRaiseAgain()
        {
            var adapter = new DagViewerProjectionAdapter();
            adapter.OnNodeAdded(ValidNode());
            int callCount = 0;
            adapter.ProjectionChanged += (_, _) => callCount++;

            adapter.Flush();
            adapter.Flush();

            Assert.Equal(1, callCount);
        }

        [Fact]
        public void OnNodeAdded_WithNullNodeId_IgnoredInSnapshots()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = new DagNode { NodeId = null, Location = new Point(0, 0) };

            adapter.OnNodeAdded(node);

            Assert.Empty(adapter.Snapshots);
        }

        [Fact]
        public void OnNodeRemoved_UnknownId_DoesNotFlushSignal()
        {
            var adapter = new DagViewerProjectionAdapter();
            int callCount = 0;
            adapter.ProjectionChanged += (_, _) => callCount++;

            adapter.OnNodeRemoved(Guid.NewGuid());
            adapter.Flush();

            Assert.Equal(0, callCount);
        }

        // ─── Stable Reference Contract (F-0-prep) ─────────────────────────────

        [Fact]
        public void OnNodeMoved_ReturnsSameObjectReference()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode(100, 200);
            adapter.OnNodeAdded(node);
            var before = adapter.Snapshots[node.NodeId!.Value];

            node.Location = new Point(300, 400);
            adapter.OnNodeMoved(node);

            var after = adapter.Snapshots[node.NodeId!.Value];
            Assert.Same(before, after);
        }

        [Fact]
        public void OnNodeRemoved_ThenOnNodeAdded_SameId_CreatesNewObject()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode(100, 200);
            adapter.OnNodeAdded(node);
            var original = adapter.Snapshots[node.NodeId!.Value];

            adapter.OnNodeRemoved(node.NodeId!.Value);
            node.Location = new Point(300, 400);
            adapter.OnNodeAdded(node);

            var renewed = adapter.Snapshots[node.NodeId!.Value];
            Assert.NotSame(original, renewed);
        }

        [Fact]
        public void DifferentNodeIds_ProduceSeparateProjectionObjects()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node1 = ValidNode(0, 0);
            var node2 = ValidNode(100, 100);
            adapter.OnNodeAdded(node1);
            adapter.OnNodeAdded(node2);

            Assert.NotSame(
                adapter.Snapshots[node1.NodeId!.Value],
                adapter.Snapshots[node2.NodeId!.Value]);
        }

        // ─── H-1 Batch Flush ──────────────────────────────────────────────────

        /// <summary>
        /// batch scope 안에서 여러 OnNode* + Flush() 호출 → ProjectionChanged 1회만.
        /// 기대: add×3 + Flush×3 → EndBatch 이후 총 1회.
        /// </summary>
        [Fact]
        public void BeginBatch_MultipleMutations_SingleFlush()
        {
            var adapter = new DagViewerProjectionAdapter();
            int callCount = 0;
            adapter.ProjectionChanged += (_, _) => callCount++;

            adapter.BeginBatch();
            adapter.OnNodeAdded(ValidNode());
            adapter.Flush(); // suppressed
            adapter.OnNodeAdded(ValidNode());
            adapter.Flush(); // suppressed
            adapter.OnNodeAdded(ValidNode());
            adapter.Flush(); // suppressed
            adapter.EndBatch(); // fires here

            Assert.Equal(1, callCount);
            Assert.Equal(1, adapter.ProjectionChangedCount);
            Assert.Equal(3, adapter.Snapshots.Count);
        }

        /// <summary>
        /// EndBatch 시 pending 변경이 없으면 Flush를 발생시키지 않는다.
        /// </summary>
        [Fact]
        public void EndBatch_WithNoPendingChange_DoesNotFlush()
        {
            var adapter = new DagViewerProjectionAdapter();
            int callCount = 0;
            adapter.ProjectionChanged += (_, _) => callCount++;

            adapter.BeginBatch();
            adapter.EndBatch();

            Assert.Equal(0, callCount);
        }

        /// <summary>
        /// 중첩 batch: 가장 바깥쪽 EndBatch()에서만 Flush가 발생한다.
        /// </summary>
        [Fact]
        public void NestedBatch_OnlyOutermostEndBatchFlushes()
        {
            var adapter = new DagViewerProjectionAdapter();
            int callCount = 0;
            adapter.ProjectionChanged += (_, _) => callCount++;

            adapter.BeginBatch();    // depth=1
            adapter.BeginBatch();    // depth=2
            adapter.OnNodeAdded(ValidNode());
            adapter.Flush();         // suppressed (depth=2)
            adapter.EndBatch();      // depth→1
            Assert.Equal(0, callCount); // 아직 발생 안 함

            adapter.EndBatch();      // depth→0 → Flush 발생
            Assert.Equal(1, callCount);
        }

        /// <summary>
        /// batch 없는 단건 동작은 기존과 동일하게 즉시 Flush된다.
        /// </summary>
        [Fact]
        public void NoBatch_SingleOperation_FlushesImmediately()
        {
            var adapter = new DagViewerProjectionAdapter();
            int callCount = 0;
            adapter.ProjectionChanged += (_, _) => callCount++;

            adapter.OnNodeAdded(ValidNode());
            adapter.Flush();

            Assert.Equal(1, callCount);
        }

        /// <summary>
        /// BatchedFlushCount: EndBatch가 실제로 Flush를 발생시킨 횟수를 추적한다.
        /// </summary>
        [Fact]
        public void BatchedFlushCount_TracksEndBatchFireCount()
        {
            var adapter = new DagViewerProjectionAdapter();

            adapter.BeginBatch();
            adapter.OnNodeAdded(ValidNode());
            adapter.Flush(); // suppressed
            adapter.OnNodeAdded(ValidNode());
            adapter.Flush(); // suppressed
            adapter.EndBatch(); // fires → BatchedFlushCount=1

            Assert.Equal(1, adapter.BatchedFlushCount);
            Assert.Equal(1, adapter.ProjectionChangedCount);
        }

        /// <summary>
        /// batch 내 remove → EndBatch: snapshot이 비어 있고 1회 Flush만 발생.
        /// </summary>
        [Fact]
        public void BeginBatch_AddThenRemove_SingleFlushEmptySnapshot()
        {
            var adapter = new DagViewerProjectionAdapter();
            int callCount = 0;
            adapter.ProjectionChanged += (_, _) => callCount++;
            var node = ValidNode();

            adapter.BeginBatch();
            adapter.OnNodeAdded(node);
            adapter.Flush(); // suppressed
            adapter.OnNodeRemoved(node.NodeId!.Value);
            adapter.Flush(); // suppressed
            adapter.EndBatch(); // fires

            Assert.Equal(1, callCount);
            Assert.Empty(adapter.Snapshots);
        }
    }
}
