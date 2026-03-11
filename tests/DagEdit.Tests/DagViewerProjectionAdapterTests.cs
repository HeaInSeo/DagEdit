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

            Assert.Equal(1, adapter.Snapshots.Count);
        }

        [Fact]
        public void OnNodeRemoved_DecreasesSnapshotCount()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode();
            adapter.OnNodeAdded(node);

            adapter.OnNodeRemoved(node.NodeId!.Value);

            Assert.Equal(0, adapter.Snapshots.Count);
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

            Assert.Equal(1, adapter.Snapshots.Count);
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

            Assert.Equal(0, adapter.Snapshots.Count);
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
    }
}
