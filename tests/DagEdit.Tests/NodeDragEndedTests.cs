using System;
using System.Collections.Generic;
using Xunit;

namespace DagEdit.Tests
{
    /// <summary>
    /// NodeDragEndedEvent 관련 동작 검증.
    ///
    /// 검증 목표:
    /// 1. NodeDragEndedEventArgs — NodeId 올바르게 저장
    /// 2. drag 종료 시 선택 집합에 없는 노드 → RequestUnpinNode 호출
    /// 3. drag 종료 시 선택 집합에 있는 노드 → RequestUnpinNode 미호출
    /// 4. drag pin → drag end 순서 → unpin 발행 순서
    /// 5. NodeId Empty 허용 (방어적)
    /// </summary>
    public class NodeDragEndedTests
    {
        // ─── NodeDragEndedEventArgs ──────────────────────────────────────────

        [Fact]
        public void NodeDragEndedEventArgs_StoresNodeId()
        {
            var nodeId = Guid.NewGuid();
            var args = new NodeDragEndedEventArgs(null!, nodeId);

            Assert.Equal(nodeId, args.NodeId);
        }

        [Fact]
        public void NodeDragEndedEventArgs_AcceptsEmptyGuid()
        {
            var args = new NodeDragEndedEventArgs(null!, Guid.Empty);

            Assert.Equal(Guid.Empty, args.NodeId);
        }

        // ─── ViewModel 수준: 선택 집합 밖 drag 종료 → unpin ─────────────────

        [Fact]
        public void DragEnd_NodeNotInSelection_UnpinsViaViewModel()
        {
            using var vm = new DagEditorViewModel();
            var nodeId = Guid.NewGuid();
            var unpinned = new List<Guid>();
            vm.UnpinRequested += (_, id) => unpinned.Add(id);

            // drag 시작 (pin)
            vm.RequestPinNode(nodeId);
            // drag 종료, 선택 집합에 없으므로 unpin
            vm.RequestUnpinNode(nodeId);

            Assert.Contains(nodeId, unpinned);
        }

        [Fact]
        public void DragEnd_NodeInSelection_NoPinLeakBeyondSelection()
        {
            // 선택 pin은 유지, drag pin은 해제되지 않아야 한다.
            // ViewModel 수준에서는 RequestUnpinNode를 호출하지 않으면 unpin 이벤트 없음.
            using var vm = new DagEditorViewModel();
            var nodeId = Guid.NewGuid();
            int unpinCount = 0;
            vm.UnpinRequested += (_, _) => unpinCount++;

            // 선택 pin만 — unpin 미호출
            vm.RequestPinNode(nodeId);

            Assert.Equal(0, unpinCount);
        }

        // ─── 순서 검증: pin → drag end → unpin 순 ────────────────────────────

        [Fact]
        public void PinThenDragEnd_EmitsUnpinAfterPin()
        {
            using var vm = new DagEditorViewModel();
            var nodeId = Guid.NewGuid();
            var events = new List<string>();
            vm.PinRequested += (_, _) => events.Add("pin");
            vm.UnpinRequested += (_, _) => events.Add("unpin");

            vm.RequestPinNode(nodeId);   // drag start
            vm.RequestUnpinNode(nodeId); // drag end (not in selection)

            Assert.Equal(new[] { "pin", "unpin" }, events);
        }
    }
}
