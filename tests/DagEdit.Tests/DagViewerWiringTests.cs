using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using DagEdit;
using VirtualCanvas.Core.Spatial;
using Xunit;

namespace DagEdit.Tests
{
    /// <summary>
    /// F-0 wiring spike — DagViewerProjectionAdapter.BuildSnapshot() 검증.
    ///
    /// 검증 목표:
    /// 1. add 후 snapshot에 item이 포함되는가
    /// 2. remove 후 snapshot이 비는가
    /// 3. move 후 snapshot의 item Bounds가 갱신되는가
    /// 4. snapshot의 item이 adapter cache와 same object ref인가 (stable reference)
    /// 5. BuildSnapshot() 호출마다 새 SpatialIndex 인스턴스가 반환되는가
    /// 6. full wiring loop: ProjectionChanged → BuildSnapshot → snapshot 최신 상태 반영
    ///
    /// 이 테스트는 "증명용 wiring"을 검증한다.
    /// Clear()+Insert+RaiseChanged() 패턴은 사용하지 않는다.
    /// </summary>
    public class DagViewerWiringTests
    {
        private static DagNode ValidNode(double x = 0, double y = 0) =>
            new DagNode { NodeId = Guid.NewGuid(), Location = new Point(x, y) };

        private static List<ISpatialItem> AllItems(SpatialIndex snapshot) =>
            snapshot.ToList();

        [Fact]
        public void BuildSnapshot_AfterNodeAdded_ContainsItem()
        {
            var adapter = new DagViewerProjectionAdapter();
            adapter.OnNodeAdded(ValidNode(100, 200));
            adapter.Flush();

            var snapshot = adapter.BuildSnapshot();

            Assert.Single(AllItems(snapshot));
        }

        [Fact]
        public void BuildSnapshot_AfterNodeRemoved_IsEmpty()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode(100, 200);
            adapter.OnNodeAdded(node);
            adapter.Flush();

            adapter.OnNodeRemoved(node.NodeId!.Value);
            adapter.Flush();

            var snapshot = adapter.BuildSnapshot();
            Assert.Empty(AllItems(snapshot));
        }

        [Fact]
        public void BuildSnapshot_AfterNodeMoved_ItemHasUpdatedBounds()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode(100, 200);
            adapter.OnNodeAdded(node);
            adapter.Flush();

            node.Location = new Point(300, 400);
            adapter.OnNodeMoved(node);
            adapter.Flush();

            var snapshot = adapter.BuildSnapshot();
            var item = AllItems(snapshot).Single();
            Assert.Equal(300, item.Bounds.X);
            Assert.Equal(400, item.Bounds.Y);
        }

        [Fact]
        public void BuildSnapshot_ItemIsSameRef_AsProjectionCache()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode(100, 200);
            adapter.OnNodeAdded(node);
            adapter.Flush();

            var cachedRef = adapter.Snapshots[node.NodeId!.Value];
            var snapshot = adapter.BuildSnapshot();

            // new index snapshot에 같은 object ref → VCA _visualMap reuse 가능
            Assert.Same(cachedRef, AllItems(snapshot).Single());
        }

        [Fact]
        public void BuildSnapshot_ReturnsNewIndexOnEachCall()
        {
            var adapter = new DagViewerProjectionAdapter();
            adapter.OnNodeAdded(ValidNode());
            adapter.Flush();

            var snapshot1 = adapter.BuildSnapshot();
            var snapshot2 = adapter.BuildSnapshot();

            Assert.NotSame(snapshot1, snapshot2);
        }

        [Fact]
        public void FullWiringLoop_ProjectionChanged_TriggersSnapshotBuild()
        {
            var adapter = new DagViewerProjectionAdapter();
            SpatialIndex? lastSnapshot = null;

            // F-0 wiring 패턴
            adapter.ProjectionChanged += (_, _) => lastSnapshot = adapter.BuildSnapshot();

            var node = ValidNode(50, 60);
            adapter.OnNodeAdded(node);
            adapter.Flush();

            Assert.NotNull(lastSnapshot);
            Assert.Single(AllItems(lastSnapshot));
        }

        [Fact]
        public void FullWiringLoop_Move_SnapshotReflectsNewBounds()
        {
            var adapter = new DagViewerProjectionAdapter();
            SpatialIndex? lastSnapshot = null;
            adapter.ProjectionChanged += (_, _) => lastSnapshot = adapter.BuildSnapshot();

            var node = ValidNode(100, 100);
            adapter.OnNodeAdded(node);
            adapter.Flush();

            node.Location = new Point(500, 600);
            adapter.OnNodeMoved(node);
            adapter.Flush();

            var item = AllItems(lastSnapshot!).Single();
            Assert.Equal(500, item.Bounds.X);
            Assert.Equal(600, item.Bounds.Y);
        }

        [Fact]
        public void FullWiringLoop_Remove_SnapshotBecomesEmpty()
        {
            var adapter = new DagViewerProjectionAdapter();
            SpatialIndex? lastSnapshot = null;
            adapter.ProjectionChanged += (_, _) => lastSnapshot = adapter.BuildSnapshot();

            var node = ValidNode(100, 100);
            adapter.OnNodeAdded(node);
            adapter.Flush();

            adapter.OnNodeRemoved(node.NodeId!.Value);
            adapter.Flush();

            Assert.Empty(AllItems(lastSnapshot!));
        }

        [Fact]
        public void MoveAfterAdd_SameRefInNewSnapshot()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = ValidNode(0, 0);
            adapter.OnNodeAdded(node);
            adapter.Flush();

            var refBeforeMove = AllItems(adapter.BuildSnapshot()).Single();

            node.Location = new Point(200, 300);
            adapter.OnNodeMoved(node);
            adapter.Flush();

            var refAfterMove = AllItems(adapter.BuildSnapshot()).Single();

            // move 전후 same ref — stable reference 계약 유지
            Assert.Same(refBeforeMove, refAfterMove);
        }
    }
}
