using Avalonia;
using Xunit;

namespace DagEdit.Tests
{
    /// <summary>
    /// 삭제된 selected/pinned node의 pin cleanup 검증.
    ///
    /// ─── 배경 ────────────────────────────────────────────────────────────────────
    /// DagEditor.HandleKeyDown (Delete 경로) 는 ExecuteDelNode를 호출한다.
    /// ExecuteDelNode → Dag.DelDagNodeItem → DynamicData Remove → OnNodeRemoved →
    /// _snapshots.Remove(nodeId) 순서로 snapshot이 제거된다.
    ///
    /// 문제:
    ///   DagEditor._pinnedBySelection 에 있는 nodeId의 unpin은 FinalizeSelection에서만 처리됨.
    ///   삭제 이후 FinalizeSelection이 호출되면 RequestUnpinNode(deletedId) →
    ///   MainWindow._onUnpinRequested → Snapshots.TryGetValue(deletedId) → false →
    ///   VCA.Unpin 호출 누락 → VCA._pinnedItems 에 stale pin 잔류 → visual 잔류.
    ///
    /// 수정 (H-7):
    ///   DagEditor.HandleKeyDown Delete 경로에서 ExecuteDelNode 전에
    ///   _pinnedBySelection 정리 + RequestUnpinNode 호출.
    ///   이 시점에서는 snapshot이 아직 존재 → VCA.Unpin 정상 작동.
    ///
    /// ─── 검증 구조 ────────────────────────────────────────────────────────────────
    /// Part A: DagViewerProjectionAdapter 타이밍 (3개) — unpin lookup 시점 검증
    /// Part B: DeleteSim PinSim-style 시나리오 (6개) — _pinnedBySelection 상태 검증
    ///
    /// ─── 아키텍처 불변조건 ─────────────────────────────────────────────────────────
    /// Scenario 3 (selected + drag 중 delete) 는 현재 아키텍처상 불가능.
    /// drag 중 포인터 캡처로 DagEditor.HandlePointerPressed 차단 → FinalizeSelection 미호출.
    /// 이 시나리오는 "불가능" 판정으로 명시하고 테스트하지 않음.
    /// </summary>
    public class DeletePinCleanupTests
    {
        private static DagNode MakeNode(double x = 10, double y = 10) =>
            new() { NodeId = Guid.NewGuid(), Location = new Point(x, y) };

        // ═══════════════════════════════════════════════════════════════════════
        // Part A: Adapter snapshot 타이밍 — 3개
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// OnNodeRemoved 이후 TryGetValue는 false를 반환한다.
        /// → ExecuteDelNode 이후 RequestUnpinNode → MainWindow._onUnpinRequested 에서
        ///   Snapshots.TryGetValue 실패 → VCA.Unpin silently skipped.
        /// </summary>
        [Fact]
        public void AfterOnNodeRemoved_TryGetValue_ReturnsFalse()
        {
            var adapter = new DagViewerProjectionAdapter();
            DagNode node = MakeNode();
            adapter.OnNodeAdded(node);

            adapter.OnNodeRemoved(node.NodeId!.Value);

            Assert.False(adapter.Snapshots.TryGetValue(node.NodeId.Value, out _));
        }

        /// <summary>
        /// OnNodeRemoved 이전 TryGetValue는 true를 반환한다.
        /// → RequestUnpinNode를 ExecuteDelNode 이전에 호출하면 VCA.Unpin이 정상 작동한다.
        /// </summary>
        [Fact]
        public void BeforeOnNodeRemoved_TryGetValue_ReturnsTrue()
        {
            var adapter = new DagViewerProjectionAdapter();
            DagNode node = MakeNode();
            adapter.OnNodeAdded(node);

            bool found = adapter.Snapshots.TryGetValue(node.NodeId!.Value, out NodeViewItem? item);

            Assert.True(found);
            Assert.NotNull(item);
        }

        /// <summary>
        /// 같은 id로 remove 후 re-add 시 새 NodeViewItem 객체가 생성된다.
        /// → 이전 pin에서 잡고 있던 old item ref와 new item ref는 다른 object다.
        ///   old item이 VCA._pinnedItems에 남아 있으면 new item의 pin 상태에 영향을 주지 않는다.
        ///   (stale old object는 여전히 _pinnedItems에 잔류 가능 — cleanup 필요.)
        /// </summary>
        [Fact]
        public void RemoveThenReAdd_SameId_SnapshotRefIsDifferentObject()
        {
            var adapter = new DagViewerProjectionAdapter();
            DagNode node = MakeNode();
            adapter.OnNodeAdded(node);
            NodeViewItem refBefore = adapter.Snapshots[node.NodeId!.Value];

            adapter.OnNodeRemoved(node.NodeId.Value);
            adapter.OnNodeAdded(node);
            NodeViewItem refAfter = adapter.Snapshots[node.NodeId.Value];

            Assert.NotSame(refBefore, refAfter);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Part B: DeleteSim — _pinnedBySelection 상태 검증 6개
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// DagEditor HandleKeyDown Delete 경로의 pin guard 로직 test-local 시뮬레이터.
        ///
        /// 미러링 대상:
        ///   Select(id)               : _pinnedBySelection.Add + RequestPinNode
        ///   DeleteBuggy(id)          : ExecuteDelNode only (H-7 fix 없음, 버그 재현용)
        ///   DeleteFixed(id, adapter) : RequestUnpinNode + _pinnedBySelection.Remove + OnNodeRemoved
        ///                             (H-7 fix 적용 순서 재현)
        /// </summary>
        private sealed class DeleteSim : IDisposable
        {
            private readonly HashSet<Guid> _pinnedBySelection = new();
            public readonly DagEditorViewModel Vm;
            public readonly List<(string Op, Guid Id)> Events = new();

            public DeleteSim()
            {
                Vm = new();
                Vm.PinRequested   += (_, id) => Events.Add(("pin",   id));
                Vm.UnpinRequested += (_, id) => Events.Add(("unpin", id));
            }

            /// <summary>
            /// 노드를 어댑터에 등록하고 snapshot을 생성한다.
            /// (DagEditor에서는 DynamicData Add → OnNodeAdded 경로로 자동 처리)
            /// </summary>
            public void AddToAdapter(DagViewerProjectionAdapter adapter, DagNode node)
                => adapter.OnNodeAdded(node);

            /// <summary>FinalizeSelection — _pinnedBySelection에 nodeId 추가 + pin 요청.</summary>
            public void Select(Guid id)
            {
                _pinnedBySelection.Add(id);
                Vm.RequestPinNode(id);
            }

            /// <summary>
            /// 버그 재현: ExecuteDelNode만 호출 (_pinnedBySelection 정리 없음).
            /// adapter에서 snapshot 제거는 adapter.OnNodeRemoved로 직접 시뮬레이션.
            /// </summary>
            public void DeleteBuggy(Guid id, DagViewerProjectionAdapter adapter)
            {
                // DagEditor의 현재(수정 전) 동작: _pinnedBySelection 정리 없이 delete
                adapter.OnNodeRemoved(id); // Dag.DelDagNodeItem → OnNodeRemoved 경로 시뮬레이션
            }

            /// <summary>
            /// H-7 fix 재현: unpin 먼저 → _pinnedBySelection 정리 → snapshot 제거.
            /// snapshot이 아직 살아 있는 상태에서 RequestUnpinNode가 호출됨을 보장.
            /// </summary>
            public void DeleteFixed(Guid id, DagViewerProjectionAdapter adapter)
            {
                // H-7: ExecuteDelNode 전에 selection pin cleanup
                if (_pinnedBySelection.Remove(id))
                    Vm.RequestUnpinNode(id);
                // 이후 ExecuteDelNode → OnNodeRemoved (여기서는 직접 호출)
                adapter.OnNodeRemoved(id);
            }

            public bool IsPinnedBySelection(Guid id) => _pinnedBySelection.Contains(id);

            public void Dispose() => Vm.Dispose();
        }

        // ── S-1: selected node delete — 버그 재현 ────────────────────────────

        /// <summary>
        /// 버그 시나리오: selected node를 unpin 없이 delete하면
        /// _pinnedBySelection에 stale entry가 남는다.
        /// </summary>
        [Fact]
        public void SelectedNodeDelete_Buggy_PinnedBySelectionNotCleaned()
        {
            var adapter = new DagViewerProjectionAdapter();
            DagNode node = MakeNode();
            using var sim = new DeleteSim();
            sim.AddToAdapter(adapter, node);
            Guid id = node.NodeId!.Value;

            sim.Select(id);
            sim.DeleteBuggy(id, adapter);

            // 버그: _pinnedBySelection에 stale entry 잔류
            Assert.True(sim.IsPinnedBySelection(id));
            // 버그: snapshot도 이미 제거됨 → 이후 RequestUnpinNode → TryGetValue 실패 경로 확인
            Assert.False(adapter.Snapshots.ContainsKey(id));
        }

        // ── S-1 fix: selected node delete with H-7 cleanup ───────────────────

        /// <summary>
        /// 수정 시나리오: H-7 fix 적용 시 _pinnedBySelection이 정리된다.
        /// </summary>
        [Fact]
        public void SelectedNodeDelete_Fixed_PinnedBySelectionCleaned()
        {
            var adapter = new DagViewerProjectionAdapter();
            DagNode node = MakeNode();
            using var sim = new DeleteSim();
            sim.AddToAdapter(adapter, node);
            Guid id = node.NodeId!.Value;

            sim.Select(id);
            sim.DeleteFixed(id, adapter);

            Assert.False(sim.IsPinnedBySelection(id));
        }

        /// <summary>
        /// 수정 시나리오: H-7 fix 적용 시 unpin 이벤트가 발행된다.
        /// </summary>
        [Fact]
        public void SelectedNodeDelete_Fixed_UnpinEventFired()
        {
            var adapter = new DagViewerProjectionAdapter();
            DagNode node = MakeNode();
            using var sim = new DeleteSim();
            sim.AddToAdapter(adapter, node);
            Guid id = node.NodeId!.Value;

            sim.Select(id);
            sim.DeleteFixed(id, adapter);

            Assert.Contains(sim.Events, e => e.Op == "unpin" && e.Id == id);
        }

        /// <summary>
        /// 수정 시나리오: unpin 이벤트 발행 시점에 snapshot이 아직 존재한다.
        /// → MainWindow._onUnpinRequested에서 TryGetValue 성공 → VCA.Unpin 정상 호출 가능.
        /// </summary>
        [Fact]
        public void SelectedNodeDelete_Fixed_UnpinCalledWhileSnapshotStillPresent()
        {
            var adapter = new DagViewerProjectionAdapter();
            DagNode node = MakeNode();
            using var sim = new DeleteSim();
            sim.AddToAdapter(adapter, node);
            Guid id = node.NodeId!.Value;

            bool snapshotPresentAtUnpinTime = false;
            sim.Vm.UnpinRequested += (_, nodeId) =>
            {
                if (nodeId == id)
                    snapshotPresentAtUnpinTime = adapter.Snapshots.ContainsKey(nodeId);
            };

            sim.Select(id);
            sim.DeleteFixed(id, adapter);

            // unpin 발생 시점에 snapshot이 살아 있었음 → VCA.Unpin 조회 성공 경로
            Assert.True(snapshotPresentAtUnpinTime);
            // delete 완료 후 snapshot 제거됨
            Assert.False(adapter.Snapshots.ContainsKey(id));
        }

        // ── S-2: non-selected node delete ────────────────────────────────────

        /// <summary>
        /// 선택되지 않은 노드를 삭제해도 unpin 이벤트가 발행되지 않는다.
        /// </summary>
        [Fact]
        public void NonSelectedNodeDelete_Fixed_NoUnpinEvent()
        {
            var adapter = new DagViewerProjectionAdapter();
            DagNode node = MakeNode();
            using var sim = new DeleteSim();
            sim.AddToAdapter(adapter, node);
            Guid id = node.NodeId!.Value;

            // Select 없이 바로 delete
            sim.DeleteFixed(id, adapter);

            Assert.DoesNotContain(sim.Events, e => e.Op == "unpin");
            Assert.False(adapter.Snapshots.ContainsKey(id));
        }

        // ── S-4: delete then re-add same id ──────────────────────────────────

        /// <summary>
        /// selected node delete 후 같은 id로 re-add 시 clean state로 시작한다.
        /// H-7 fix 적용: old pin cleanup → delete → re-add → unpinned, snapshot 존재.
        /// </summary>
        [Fact]
        public void DeleteThenReAdd_SameId_CleanState()
        {
            var adapter = new DagViewerProjectionAdapter();
            DagNode node = MakeNode();
            using var sim = new DeleteSim();
            sim.AddToAdapter(adapter, node);
            Guid id = node.NodeId!.Value;

            sim.Select(id);
            sim.DeleteFixed(id, adapter);

            // Re-add: 새 snapshot 생성
            sim.AddToAdapter(adapter, node);

            Assert.True(adapter.Snapshots.ContainsKey(id));
            // _pinnedBySelection은 비어 있음 (stale pin 없음)
            Assert.False(sim.IsPinnedBySelection(id));
        }
    }
}
