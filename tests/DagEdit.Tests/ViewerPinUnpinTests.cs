using Avalonia;
using Xunit;

namespace DagEdit.Tests
{
    /// <summary>
    /// H-3 VCA Pin/Unpin 연동 — DagEditorViewModel.PinRequested/UnpinRequested 이벤트 검증.
    ///
    /// 검증 목표:
    /// 1. RequestPinNode → PinRequested 이벤트 발생
    /// 2. RequestUnpinNode → UnpinRequested 이벤트 발생
    /// 3. PinRequested 발생 시 올바른 nodeId 전달
    /// 4. UnpinRequested 발생 시 올바른 nodeId 전달
    /// 5. PinRequested 없는 구독자 → no-op (null safe)
    /// 6. 연속 Pin 호출 → 여러 번 발생
    /// 7. 연속 Pin + Unpin 순서 검증
    /// </summary>
    public class ViewerPinUnpinTests
    {
        private static DagEditorViewModel MakeVm() => new DagEditorViewModel();

        // ─── RequestPinNode → PinRequested ───────────────────────────────────

        [Fact]
        public void RequestPinNode_FiresPinRequested()
        {
            using var vm = MakeVm();
            var nodeId = Guid.NewGuid();
            int callCount = 0;
            vm.PinRequested += (_, _) => callCount++;

            vm.RequestPinNode(nodeId);

            Assert.Equal(1, callCount);
        }

        [Fact]
        public void RequestPinNode_DeliversCorrectNodeId()
        {
            using var vm = MakeVm();
            var nodeId = Guid.NewGuid();
            Guid received = Guid.Empty;
            vm.PinRequested += (_, id) => received = id;

            vm.RequestPinNode(nodeId);

            Assert.Equal(nodeId, received);
        }

        [Fact]
        public void RequestPinNode_NoSubscriber_IsNoOp()
        {
            using var vm = MakeVm();
            var ex = Record.Exception(() => vm.RequestPinNode(Guid.NewGuid()));
            Assert.Null(ex);
        }

        // ─── RequestUnpinNode → UnpinRequested ───────────────────────────────

        [Fact]
        public void RequestUnpinNode_FiresUnpinRequested()
        {
            using var vm = MakeVm();
            var nodeId = Guid.NewGuid();
            int callCount = 0;
            vm.UnpinRequested += (_, _) => callCount++;

            vm.RequestUnpinNode(nodeId);

            Assert.Equal(1, callCount);
        }

        [Fact]
        public void RequestUnpinNode_DeliversCorrectNodeId()
        {
            using var vm = MakeVm();
            var nodeId = Guid.NewGuid();
            Guid received = Guid.Empty;
            vm.UnpinRequested += (_, id) => received = id;

            vm.RequestUnpinNode(nodeId);

            Assert.Equal(nodeId, received);
        }

        [Fact]
        public void RequestUnpinNode_NoSubscriber_IsNoOp()
        {
            using var vm = MakeVm();
            var ex = Record.Exception(() => vm.RequestUnpinNode(Guid.NewGuid()));
            Assert.Null(ex);
        }

        // ─── 순서 검증 ────────────────────────────────────────────────────────

        [Fact]
        public void PinThenUnpin_OrderIsCorrect()
        {
            using var vm = MakeVm();
            var nodeId = Guid.NewGuid();
            var events = new List<string>();
            vm.PinRequested += (_, _) => events.Add("pin");
            vm.UnpinRequested += (_, _) => events.Add("unpin");

            vm.RequestPinNode(nodeId);
            vm.RequestPinNode(nodeId); // 중복 pin — 이벤트는 발생한다 (집합 관리는 caller 책임)
            vm.RequestUnpinNode(nodeId);

            Assert.Equal(new[] { "pin", "pin", "unpin" }, events);
        }

        // ─── DagViewerProjectionAdapter 연동: Snapshot에서 NodeViewItem 조회 ─

        [Fact]
        public void PinRequested_CanLookupNodeViewItemFromAdapter()
        {
            using var vm = MakeVm();
            var node = new DagNode { NodeId = Guid.NewGuid(), Location = new Point(10, 20) };

            // adapter에 노드 추가 (ViewModel 내부 DynamicData 구독이 아닌 직접 접근)
            vm.ViewerAdapter.OnNodeAdded(node);

            NodeViewItem? pinnedItem = null;
            vm.PinRequested += (_, id) =>
            {
                vm.ViewerAdapter.Snapshots.TryGetValue(id, out pinnedItem);
            };

            vm.RequestPinNode(node.NodeId!.Value);

            Assert.NotNull(pinnedItem);
            Assert.Equal(node.NodeId!.Value, pinnedItem!.NodeId);
        }
    }
}
