using Avalonia;
using Xunit;

namespace DagEdit.Tests
{
    /// <summary>
    /// "Undo-after-delete" pin 계약 고정.
    ///
    /// ─── 배경 ─────────────────────────────────────────────────────────────────
    /// H-7에서 selected node delete 전에 unpin을 선행하여 stale pin 버그를 수정했다
    /// (DeletePinCleanupTests 참조).
    ///
    /// 이 파일은 그 다음 계약을 고정한다:
    ///   Undo로 노드가 복원되었을 때 pin state는 자동 복원되지 않는다.
    ///
    /// ─── Undo 경로 요약 ────────────────────────────────────────────────────────
    /// DagEditorViewModel.Undo()
    ///   → _undoRedo.Undo()
    ///   → DelNodeCommand.Undo()
    ///   → Dag.RestoreDagNodeItem(_nodeItem)          ← _dagItemsSource.Add
    ///   → DynamicData Add 구독
    ///   → _viewerAdapter.OnNodeAdded(nodeItem)       ← 새 snapshot 생성
    ///   (PinRequested / UnpinRequested 이벤트 없음)
    ///
    /// ─── 계약 목록 ────────────────────────────────────────────────────────────
    /// C1: delete 전 selection pin은 정리된다
    ///     → DeletePinCleanupTests.SelectedNodeDelete_Fixed_* 에서 확인 완료.
    ///       이 파일은 C1을 전제 조건으로 사용하고 재검증하지 않는다.
    ///
    /// C2: Undo로 node가 복원되어도 PinRequested는 발행되지 않는다.
    /// C3: Undo 후 _pinnedBySelection은 해당 nodeId를 포함하지 않는다.
    /// C4: Undo 후 adapter snapshot이 복원된다 (새 object ref).
    /// C5: Undo 후 사용자가 다시 selection하면 새 pin cycle이 정상 시작된다.
    /// C6: Undo 후 사용자가 drag를 시작하면 새 pin cycle이 정상 시작된다.
    ///
    /// ─── 테스트 방법 ──────────────────────────────────────────────────────────
    /// UndoAfterDeleteSim: DagEditor pin guard 로직 + Undo 경로를 test-local pure 클래스로 미러링.
    /// VCA 의존 없음. 이벤트 스트림(Events)으로만 검증.
    /// </summary>
    public class UndoAfterDeletePinContractTests
    {
        private static DagNode MakeNode(double x = 10, double y = 10) =>
            new() { NodeId = Guid.NewGuid(), Location = new Point(x, y) };

        // ─── UndoAfterDeleteSim ───────────────────────────────────────────────

        /// <summary>
        /// DagEditor의 pin lifecycle + Undo 경로를 test-local pure 클래스로 미러링.
        ///
        /// 미러링 대상:
        ///   AddToAdapter(adapter, node)   : DynamicData Add → OnNodeAdded (snapshot 생성)
        ///   Select(id)                    : FinalizeSelection (old unpin → new pin)
        ///   DeleteFixed(id, adapter)      : H-7 fix — unpin first, then OnNodeRemoved
        ///   UndoDelete(id, adapter, node) : DelNodeCommand.Undo() — OnNodeAdded only, pin 없음
        ///   DragStart(id)                 : HandleNodeDragStarted — RequestPinNode
        ///   DragEnd(id)                   : HandleNodeDragEnded — RequestUnpinNode if not selected
        /// </summary>
        private sealed class UndoAfterDeleteSim : IDisposable
        {
            private readonly HashSet<Guid> _pinnedBySelection = new();
            public readonly DagEditorViewModel Vm;
            public readonly List<(string Op, Guid Id)> Events = new();

            public UndoAfterDeleteSim()
            {
                Vm = new();
                Vm.PinRequested   += (_, id) => Events.Add(("pin",   id));
                Vm.UnpinRequested += (_, id) => Events.Add(("unpin", id));
            }

            /// <summary>DynamicData Add → OnNodeAdded (snapshot 생성).</summary>
            public void AddToAdapter(DagViewerProjectionAdapter adapter, DagNode node)
                => adapter.OnNodeAdded(node);

            /// <summary>
            /// FinalizeSelection — 기존 선택 unpin 후 신규 선택 pin.
            /// 인수 없이 호출 시 전체 deselect.
            /// </summary>
            public void Select(params Guid[] newIds)
            {
                foreach (var oldId in _pinnedBySelection)
                    Vm.RequestUnpinNode(oldId);
                _pinnedBySelection.Clear();

                foreach (var id in newIds)
                {
                    _pinnedBySelection.Add(id);
                    Vm.RequestPinNode(id);
                }
            }

            /// <summary>
            /// H-7 fix 재현: unpin 먼저 → _pinnedBySelection 정리 → snapshot 제거.
            /// (DagEditor.HandleKeyDown Delete 경로)
            /// </summary>
            public void DeleteFixed(Guid id, DagViewerProjectionAdapter adapter)
            {
                if (_pinnedBySelection.Remove(id))
                    Vm.RequestUnpinNode(id);
                adapter.OnNodeRemoved(id);
            }

            /// <summary>
            /// DelNodeCommand.Undo() 시뮬레이션.
            ///
            /// 실제 경로:
            ///   Dag.RestoreDagNodeItem → _dagItemsSource.Add
            ///   → ViewModel 구독: _viewerAdapter.OnNodeAdded(nodeItem) ← 새 snapshot
            ///   PinRequested / UnpinRequested 없음.
            ///   _pinnedBySelection 건드리지 않음.
            ///
            /// 계약: Undo는 pin state를 복원하지 않는다.
            /// </summary>
            public void UndoDelete(DagViewerProjectionAdapter adapter, DagNode node)
            {
                // _pinnedBySelection 변경 없음 — Undo는 selection state를 건드리지 않는다.
                // PinRequested / UnpinRequested 발행 없음.
                adapter.OnNodeAdded(node); // RestoreDagNodeItem → DynamicData Add → OnNodeAdded
            }

            /// <summary>HandleNodeDragStarted — RequestPinNode.</summary>
            public void DragStart(Guid id) => Vm.RequestPinNode(id);

            /// <summary>HandleNodeDragEnded — 선택 집합 외부면 RequestUnpinNode.</summary>
            public void DragEnd(Guid id)
            {
                if (!_pinnedBySelection.Contains(id))
                    Vm.RequestUnpinNode(id);
            }

            public bool IsPinnedBySelection(Guid id) => _pinnedBySelection.Contains(id);
            public int PinCount(Guid id) => Events.Count(e => e.Op == "pin" && e.Id == id);
            public int UnpinCount(Guid id) => Events.Count(e => e.Op == "unpin" && e.Id == id);
            public string? LastOp(Guid id) => Events.LastOrDefault(e => e.Id == id).Op;

            public void Dispose() => Vm.Dispose();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Part A: Undo-pin 격리 — C2 / C3 / C4
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// C2: Undo는 PinRequested를 발행하지 않는다.
        ///
        /// 근거: DelNodeCommand.Undo()는 Dag.RestoreDagNodeItem을 호출한다.
        /// RestoreDagNodeItem은 _dagItemsSource.Add만 수행하고
        /// PinRequested / UnpinRequested 이벤트를 발행하는 코드 경로에 닿지 않는다.
        /// </summary>
        [Fact]
        public void C2_UndoDelete_DoesNotFirePinRequested()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = MakeNode();
            using var sim = new UndoAfterDeleteSim();
            sim.AddToAdapter(adapter, node);
            var id = node.NodeId!.Value;

            sim.Select(id);
            sim.DeleteFixed(id, adapter); // H-7: unpin 선행 후 삭제

            var pinCountBeforeUndo = sim.PinCount(id);
            sim.UndoDelete(adapter, node); // Undo: RestoreDagNodeItem → OnNodeAdded

            // Undo 이후 PinCount가 증가하지 않아야 한다
            Assert.Equal(pinCountBeforeUndo, sim.PinCount(id));
        }

        /// <summary>
        /// C3: Undo 후 _pinnedBySelection에 해당 nodeId가 없다.
        ///
        /// 근거: _pinnedBySelection은 DagEditor가 소유하는 selection state다.
        /// Undo 경로(DelNodeCommand.Undo)는 Dag 모델 계층만 건드리며
        /// DagEditor의 selection state를 알지 못한다.
        /// </summary>
        [Fact]
        public void C3_UndoDelete_NodeNotInPinnedBySelection()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = MakeNode();
            using var sim = new UndoAfterDeleteSim();
            sim.AddToAdapter(adapter, node);
            var id = node.NodeId!.Value;

            sim.Select(id);
            sim.DeleteFixed(id, adapter);
            sim.UndoDelete(adapter, node);

            Assert.False(sim.IsPinnedBySelection(id));
        }

        /// <summary>
        /// C4: Undo 후 adapter snapshot이 복원된다.
        ///
        /// 근거: RestoreDagNodeItem → _dagItemsSource.Add → ViewModel 구독
        /// → _viewerAdapter.OnNodeAdded(nodeItem) → Snapshots에 새 entry 생성.
        /// 이전 snapshot과 다른 object ref (DeletePinCleanupTests.RemoveThenReAdd_SameId 참조).
        /// </summary>
        [Fact]
        public void C4_UndoDelete_AdapterSnapshotRestored()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = MakeNode();
            using var sim = new UndoAfterDeleteSim();
            sim.AddToAdapter(adapter, node);
            var id = node.NodeId!.Value;

            sim.Select(id);
            sim.DeleteFixed(id, adapter);

            // delete 후 snapshot 없음
            Assert.False(adapter.Snapshots.ContainsKey(id));

            sim.UndoDelete(adapter, node);

            // Undo 후 새 snapshot 존재
            Assert.True(adapter.Snapshots.ContainsKey(id));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Part B: Undo 후 fresh pin cycle — C5 / C6
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// C5: Undo 후 사용자가 다시 selection하면 새 pin cycle이 정상 시작된다.
        ///
        /// 근거: FinalizeSelection은 DagEditor의 _pinnedBySelection에 id를 추가하고
        /// RequestPinNode를 호출한다. Undo가 pin state를 건드리지 않으므로,
        /// 복원 후 selection은 처음 선택하는 것과 동일하게 동작한다.
        /// </summary>
        [Fact]
        public void C5_AfterUndoDelete_SelectionPinsNormally()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = MakeNode();
            using var sim = new UndoAfterDeleteSim();
            sim.AddToAdapter(adapter, node);
            var id = node.NodeId!.Value;

            sim.Select(id);
            sim.DeleteFixed(id, adapter);
            sim.UndoDelete(adapter, node);

            // Undo 후 re-select
            sim.Select(id);

            Assert.True(sim.IsPinnedBySelection(id));
            // 마지막 이벤트는 pin이어야 한다
            Assert.Equal("pin", sim.LastOp(id));
        }

        /// <summary>
        /// C5b: Undo 후 selection 시 PinRequested가 정확히 1회 발행된다.
        ///
        /// (선택 이전에 자동 pin이 없었음을 PinCount로 확인)
        /// </summary>
        [Fact]
        public void C5b_AfterUndoDelete_SelectFiresPinRequestedExactlyOnce()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = MakeNode();
            using var sim = new UndoAfterDeleteSim();
            sim.AddToAdapter(adapter, node);
            var id = node.NodeId!.Value;

            sim.Select(id);           // pin [1]
            sim.DeleteFixed(id, adapter); // unpin [1]
            sim.UndoDelete(adapter, node); // no pin

            var pinCountAfterUndo = sim.PinCount(id);

            sim.Select(id);           // pin [2]

            // Undo 구간에서 추가 pin 없음, Select 후 정확히 1회 증가
            Assert.Equal(pinCountAfterUndo + 1, sim.PinCount(id));
        }

        /// <summary>
        /// C6: Undo 후 사용자가 drag를 시작하면 새 pin cycle이 정상 시작된다.
        ///
        /// 근거: HandleNodeDragStarted는 항상 RequestPinNode를 호출한다.
        /// Undo가 pin state를 변경하지 않으므로, drag pin은 정상 동작한다.
        /// </summary>
        [Fact]
        public void C6_AfterUndoDelete_DragStartPinsNormally()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = MakeNode();
            using var sim = new UndoAfterDeleteSim();
            sim.AddToAdapter(adapter, node);
            var id = node.NodeId!.Value;

            sim.Select(id);
            sim.DeleteFixed(id, adapter);
            sim.UndoDelete(adapter, node);

            var pinCountAfterUndo = sim.PinCount(id);

            sim.DragStart(id); // HandleNodeDragStarted → RequestPinNode

            Assert.Equal(pinCountAfterUndo + 1, sim.PinCount(id));
        }

        /// <summary>
        /// C6b: Undo 후 drag 완료 시 unpin이 정상 발행된다.
        ///
        /// Undo 후 선택 상태가 아니므로(C3), DragEnd에서 unpin이 발행된다.
        /// </summary>
        [Fact]
        public void C6b_AfterUndoDelete_DragEndUnpinsNormally()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = MakeNode();
            using var sim = new UndoAfterDeleteSim();
            sim.AddToAdapter(adapter, node);
            var id = node.NodeId!.Value;

            sim.Select(id);
            sim.DeleteFixed(id, adapter);
            sim.UndoDelete(adapter, node);

            sim.DragStart(id);

            var unpinCountBeforeDragEnd = sim.UnpinCount(id);
            sim.DragEnd(id); // 선택 없으므로 unpin 발행

            Assert.Equal(unpinCountBeforeDragEnd + 1, sim.UnpinCount(id));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Part C: 전체 시퀀스 + 비선택 노드 케이스
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 전체 시퀀스: Select → Delete → Undo → Select → DragStart → DragEnd → Deselect.
        ///
        /// 각 단계의 pin/unpin 이벤트 수를 단계별로 검증한다.
        /// </summary>
        [Fact]
        public void FullSequence_SelectDeleteUndoSelectDragEndDeselect()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = MakeNode();
            using var sim = new UndoAfterDeleteSim();
            sim.AddToAdapter(adapter, node);
            var id = node.NodeId!.Value;

            // 1. 선택 → pin [1]
            sim.Select(id);
            Assert.Equal(1, sim.PinCount(id));
            Assert.Equal(0, sim.UnpinCount(id));

            // 2. 삭제 → unpin [1], _pinnedBySelection 정리
            sim.DeleteFixed(id, adapter);
            Assert.Equal(1, sim.PinCount(id));
            Assert.Equal(1, sim.UnpinCount(id));
            Assert.False(sim.IsPinnedBySelection(id));

            // 3. Undo → snapshot 복원, pin event 없음
            sim.UndoDelete(adapter, node);
            Assert.Equal(1, sim.PinCount(id));   // 변화 없음
            Assert.Equal(1, sim.UnpinCount(id)); // 변화 없음
            Assert.False(sim.IsPinnedBySelection(id)); // 여전히 비선택 상태
            Assert.True(adapter.Snapshots.ContainsKey(id)); // snapshot 복원됨

            // 4. 재선택 → pin [2]
            sim.Select(id);
            Assert.Equal(2, sim.PinCount(id));
            Assert.True(sim.IsPinnedBySelection(id));

            // 5. drag start → pin [3] (selection guard: unpin 없음)
            sim.DragStart(id);
            Assert.Equal(3, sim.PinCount(id));
            Assert.Equal(1, sim.UnpinCount(id)); // selection guard: unpin 안 함

            // 6. drag end → selection guard: unpin 없음
            sim.DragEnd(id);
            Assert.Equal(1, sim.UnpinCount(id)); // 여전히 1 — selection guard 작동

            // 7. deselect → unpin [2]
            sim.Select(); // empty selection
            Assert.Equal(2, sim.UnpinCount(id));
            Assert.Equal("unpin", sim.LastOp(id));
        }

        /// <summary>
        /// 비선택 노드 Undo: delete + Undo 후에도 clean state 유지.
        ///
        /// 선택 없이 삭제된 노드를 Undo했을 때,
        /// _pinnedBySelection이 비어 있고 unpin 이벤트도 발생하지 않는다.
        /// </summary>
        [Fact]
        public void NonSelectedNode_DeleteThenUndo_CleanState()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = MakeNode();
            using var sim = new UndoAfterDeleteSim();
            sim.AddToAdapter(adapter, node);
            var id = node.NodeId!.Value;

            // Select 없이 바로 delete
            sim.DeleteFixed(id, adapter); // _pinnedBySelection에 없으므로 unpin 없음
            sim.UndoDelete(adapter, node);

            Assert.Equal(0, sim.PinCount(id));
            Assert.Equal(0, sim.UnpinCount(id));
            Assert.False(sim.IsPinnedBySelection(id));
            Assert.True(adapter.Snapshots.ContainsKey(id));
        }

        /// <summary>
        /// 아키텍처 불변조건 문서화: Undo 경로에는 pin/unpin 코드 경로가 없다.
        ///
        /// PinRequested / UnpinRequested는 오직 다음 경로에서만 발행된다:
        ///   - DagEditor.FinalizeSelection (selection pin/unpin)
        ///   - DagEditor.HandleNodeDragStarted (drag pin)
        ///   - DagEditor.HandleNodeDragEnded (drag unpin, if not selected)
        ///   - DagEditor.HandleKeyDown Delete path, H-7 (pre-delete unpin)
        ///
        /// DagEditorViewModel.Undo() → DelNodeCommand.Undo() → Dag.RestoreDagNodeItem()
        /// 는 이 경로들 중 어느 것도 거치지 않는다.
        /// </summary>
        [Fact]
        public void ArchitecturalInvariant_UndoPathHasNoPinUnpinCodePath()
        {
            var adapter = new DagViewerProjectionAdapter();
            var node = MakeNode();
            using var sim = new UndoAfterDeleteSim();
            sim.AddToAdapter(adapter, node);
            var id = node.NodeId!.Value;

            // 여러 개의 select/delete/undo 사이클을 반복해도 Undo 자체에서 pin 이벤트 없음
            for (var i = 0; i < 3; i++)
            {
                sim.Select(id);
                sim.DeleteFixed(id, adapter);
                var pinsBefore = sim.PinCount(id);
                var unpinsBefore = sim.UnpinCount(id);

                sim.UndoDelete(adapter, node); // Undo: pin/unpin 없음

                Assert.Equal(pinsBefore, sim.PinCount(id));
                Assert.Equal(unpinsBefore, sim.UnpinCount(id));
            }
        }
    }
}
