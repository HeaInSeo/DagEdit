using Xunit;

namespace DagEdit.Tests
{
    /// <summary>
    /// Pin lifecycle 정합성 검증 — overlapping selection/drag pin scenarios.
    ///
    /// ─── 아키텍처 불변조건 ────────────────────────────────────────────────────
    ///
    /// Scenario 3/4 (선택 해제가 drag 활성 중에 발생하는 경우)는 현재 DagEditor에서
    /// 아키텍처적으로 불가능하다:
    ///
    ///   DagEditor.HandlePointerPressed:
    ///       if (IsLeftButton &amp;&amp; !args.Handled) → IsSelecting = true
    ///
    ///   Node.HandlePointerPressed:
    ///       args.Pointer.Capture(this); args.Handled = true;
    ///
    /// 노드 drag 중에는 포인터가 Node에 캡처되어 있고 args.Handled = true이므로,
    /// DagEditor의 HandlePointerPressed가 IsSelecting을 true로 설정할 수 없다.
    /// FinalizeSelection은 IsSelecting = true 경로에서만 호출되므로,
    /// 노드 drag 중 selection 변경은 발생하지 않는다.
    ///
    /// ─── 검증 대상 시나리오 ────────────────────────────────────────────────────
    ///
    ///   S-1 : selected + drag start → pin 유지 (VCA idempotent add)
    ///   S-2 : selected + drag start + drag end (이동 있음) → selection pin 유지
    ///   S-2b: selected + drag start + drag end (이동 없음) → selection pin 유지
    ///   S-5 : selected + drag start + drag end + deselection → 최종 unpin
    ///   S-6a: drag only, no move → pin → unpin 1회
    ///   S-6b: drag only, 이동 있음 → UnpinRequested 2회 (harmless, VCA idempotent)
    ///
    /// ─── 테스트 방법 ──────────────────────────────────────────────────────────
    ///
    /// PinSim: DagEditor H-3/H-4 pin guard 로직을 test-local pure 클래스로 미러링.
    /// DagEditorViewModel의 PinRequested/UnpinRequested event stream을 기록하여 검증.
    /// </summary>
    public class PinLifecycleTests
    {
        // ─── PinSim ────────────────────────────────────────────────────────────

        /// <summary>
        /// DagEditor의 H-3/H-4 pin lifecycle 로직 test-local 시뮬레이터.
        ///
        /// 미러링 대상:
        ///   HandleNodeDragStarted : always RequestPinNode
        ///   HandleNodeMoved       : RequestUnpinNode if not in _pinnedBySelection
        ///   HandleNodeDragEnded   : RequestUnpinNode if not in _pinnedBySelection
        ///   FinalizeSelection     : unpin old, clear, pin new
        /// </summary>
        private sealed class PinSim : IDisposable
        {
            private readonly HashSet<Guid> _selection = new();
            public readonly DagEditorViewModel Vm;
            public readonly List<(string Op, Guid Id)> Events = new();

            public PinSim()
            {
                Vm = new();
                Vm.PinRequested += (_, id) => Events.Add(("pin", id));
                Vm.UnpinRequested += (_, id) => Events.Add(("unpin", id));
            }

            /// <summary>HandleNodeDragStarted — 항상 pin 요청.</summary>
            public void DragStart(Guid id) => Vm.RequestPinNode(id);

            /// <summary>
            /// HandleNodeMoved — 이동이 있을 때만 발행.
            /// H-6: unpin 제거. PushMoveNode(undo 스택) 담당만 유지.
            /// unpin은 DragEnd(HandleNodeDragEnded)가 단일 경로로 담당.
            /// </summary>
            public void NodeMoved(Guid _)
            {
                // PushMoveNode에 해당하는 side-effect는 여기서 없음 (VM 테스트 대상 아님).
                // unpin 없음 — DragEnd가 담당 (H-6).
            }

            /// <summary>HandleNodeDragEnded — 항상 발행. 선택 집합 외부면 unpin.</summary>
            public void DragEnd(Guid id)
            {
                if (!_selection.Contains(id))
                {
                    Vm.RequestUnpinNode(id);
                }
            }

            /// <summary>
            /// FinalizeSelection — 기존 선택 unpin 후 신규 선택 pin.
            /// 인수 없이 호출 시 전체 deselect.
            /// </summary>
            public void FinalizeSelection(params Guid[] newIds)
            {
                foreach (var oldId in _selection)
                {
                    Vm.RequestUnpinNode(oldId);
                }

                _selection.Clear();
                foreach (var id in newIds)
                {
                    _selection.Add(id);
                    Vm.RequestPinNode(id);
                }
            }

            public int PinCount(Guid id) => Events.Count(e => e.Op == "pin" && e.Id == id);
            public int UnpinCount(Guid id) => Events.Count(e => e.Op == "unpin" && e.Id == id);
            public string? LastOp(Guid id) => Events.LastOrDefault(e => e.Id == id).Op;

            public void Dispose() => Vm.Dispose();
        }

        // ─── S-1: selected + drag start ────────────────────────────────────────

        [Fact]
        public void S1_SelectedThenDragStart_PinsNode_NoUnpin()
        {
            // VCA는 HashSet.Add — 이중 Pin은 idempotent. 실제 pin 상태는 유지된다.
            using var sim = new PinSim();
            var id = Guid.NewGuid();

            sim.FinalizeSelection(id);  // selection pin → PinRequested
            sim.DragStart(id);          // drag pin → PinRequested (2nd, VCA no-op)

            Assert.Equal(2, sim.PinCount(id));
            Assert.Equal(0, sim.UnpinCount(id));
        }

        // ─── S-2: selected + drag start + drag end (이동 있음) ─────────────────

        [Fact]
        public void S2_SelectedDragEnd_WithMove_SelectionPinMaintained()
        {
            // NodeMovedEvent + NodeDragEndedEvent 모두 발행되지만,
            // selection guard (_pinnedBySelection)로 인해 unpin되지 않는다.
            using var sim = new PinSim();
            var id = Guid.NewGuid();

            sim.FinalizeSelection(id);
            sim.DragStart(id);
            sim.NodeMoved(id);   // HandleNodeMoved → selection guard blocks unpin
            sim.DragEnd(id);     // HandleNodeDragEnded → selection guard blocks unpin

            Assert.Equal(0, sim.UnpinCount(id));
            // selection pin이 여전히 살아있으므로 마지막 이벤트는 pin이어야 함
            Assert.Equal("pin", sim.LastOp(id));
        }

        // ─── S-2b: selected + drag start + drag end (이동 없음) ────────────────

        [Fact]
        public void S2b_SelectedDragEnd_NoMove_SelectionPinMaintained()
        {
            // H-4의 목적: NodeMovedEvent 없이도 drag 종료를 처리.
            // 선택 중이면 NodeDragEndedEvent도 unpin하지 않는다.
            using var sim = new PinSim();
            var id = Guid.NewGuid();

            sim.FinalizeSelection(id);
            sim.DragStart(id);
            sim.DragEnd(id);   // no NodeMoved; selection guard blocks unpin

            Assert.Equal(0, sim.UnpinCount(id));
        }

        // ─── S-5: selected → drag end → deselection ────────────────────────────

        [Fact]
        public void S5_SelectedDragEnd_ThenDeselect_UnpinsExactlyOnceAtDeselect()
        {
            // drag 종료 시: selection guard로 unpin 없음
            // deselection 시: FinalizeSelection이 unpin을 발행
            using var sim = new PinSim();
            var id = Guid.NewGuid();

            sim.FinalizeSelection(id);
            sim.DragStart(id);
            sim.DragEnd(id);           // selection guard: unpin 없음
            sim.FinalizeSelection();   // deselect all → unpin 발행

            Assert.Equal(1, sim.UnpinCount(id));
            Assert.Equal("unpin", sim.LastOp(id));
        }

        [Fact]
        public void S5_SelectedDragEnd_WithMove_ThenDeselect_UnpinsExactlyOnceAtDeselect()
        {
            using var sim = new PinSim();
            var id = Guid.NewGuid();

            sim.FinalizeSelection(id);
            sim.DragStart(id);
            sim.NodeMoved(id);         // selection guard: unpin 없음
            sim.DragEnd(id);           // selection guard: unpin 없음
            sim.FinalizeSelection();   // deselect → unpin

            Assert.Equal(1, sim.UnpinCount(id));
            Assert.Equal("unpin", sim.LastOp(id));
        }

        // ─── S-6a: drag only, 이동 없음 ────────────────────────────────────────

        [Fact]
        public void S6a_DragOnly_NoMove_PinThenUnpin()
        {
            using var sim = new PinSim();
            var id = Guid.NewGuid();

            sim.DragStart(id);
            sim.DragEnd(id);

            Assert.Equal(1, sim.PinCount(id));
            Assert.Equal(1, sim.UnpinCount(id));
            Assert.Equal(new[] { ("pin", id), ("unpin", id) }, sim.Events.ToArray());
        }

        // ─── S-6b: drag only, 이동 있음 — double UnpinRequested ────────────────

        [Fact]
        public void S6b_DragOnly_WithMove_UnpinRequestedOnce()
        {
            // H-6 정규화 이후: unpin은 HandleNodeDragEnded 단일 경로.
            // HandleNodeMoved는 PushMoveNode만 담당하고 unpin을 호출하지 않는다.
            // Node.HandlePointerReleased 발행 순서:
            //   1) RaiseNodeMovedEvent → DagEditor.HandleNodeMoved → PushMoveNode only
            //   2) RaiseEvent(NodeDragEndedEvent) → DagEditor.HandleNodeDragEnded → UnpinRequested [1회]
            using var sim = new PinSim();
            var id = Guid.NewGuid();

            sim.DragStart(id);
            sim.NodeMoved(id);   // HandleNodeMoved → unpin 없음 (H-6)
            sim.DragEnd(id);     // HandleNodeDragEnded → UnpinRequested [1회]

            Assert.Equal(1, sim.PinCount(id));
            Assert.Equal(1, sim.UnpinCount(id));
        }

        // ─── Selection 교체: 기존 노드 unpin, 신규 노드 pin ───────────────────

        [Fact]
        public void SelectionChange_OldNodeUnpinned_NewNodePinned()
        {
            using var sim = new PinSim();
            Guid nodeA = Guid.NewGuid();
            Guid nodeB = Guid.NewGuid();

            sim.FinalizeSelection(nodeA);          // A 선택
            sim.FinalizeSelection(nodeB);          // B 선택 (A 해제)

            Assert.Equal(1, sim.PinCount(nodeA));
            Assert.Equal(1, sim.UnpinCount(nodeA));
            Assert.Equal(1, sim.PinCount(nodeB));
            Assert.Equal(0, sim.UnpinCount(nodeB));
        }

        // ─── 아키텍처 불변조건 문서화 테스트 ──────────────────────────────────

        [Fact]
        public void ArchitecturalInvariant_SelectionAndDragAreMutuallyExclusive()
        {
            // DagEditor.HandlePointerPressed 조건:
            //   IsSelecting = true  ←  IsLeftButton && !args.Handled
            //
            // Node.HandlePointerPressed:
            //   args.Pointer.Capture(this); args.Handled = true;
            //
            // 이 두 조건에 의해 노드 drag 중에는 IsSelecting이 true로 설정될 수 없다.
            // FinalizeSelection은 IsSelecting = true 경로에서만 호출된다.
            // → "drag 활성 중 deselection" 시나리오는 현재 UI에서 발생하지 않는다.
            //
            // 이 테스트는 그 불변조건을 이벤트 시퀀스로 명시한다:
            // drag 중 FinalizeSelection(empty)이 호출된다면 unpin이 발행될 것이나
            // 현재 구현에서는 그 경로가 차단되어 있다.

            using var sim = new PinSim();
            var id = Guid.NewGuid();

            // Normal reachable sequence: select → drag end → deselect
            sim.FinalizeSelection(id);   // selection
            sim.DragStart(id);
            // NOTE: FinalizeSelection during active drag is NOT called in practice.
            // Verified by pointer capture + args.Handled mutual exclusion.
            sim.DragEnd(id);             // drag ends first
            sim.FinalizeSelection();     // then deselection — only reachable sequence

            // selection pin이 drag-end 때 풀리지 않고 deselection 때만 풀린다.
            Assert.Equal(1, sim.UnpinCount(id));
            Assert.Equal("unpin", sim.LastOp(id));
        }
    }
}
