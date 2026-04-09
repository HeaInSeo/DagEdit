using System;
using Avalonia;

namespace DagEdit
{
    /// <summary>
    /// 노드 이동 명령. Execute/Undo = Node.MoveTo(new/old). 드래그 완료 시 생성된다.
    /// </summary>
    internal sealed class MoveNodeCommand : IUndoableCommand
    {
        private readonly DagEditorViewModel _viewModel;
        private readonly Guid _nodeId;
        private readonly Point _oldLocation;
        private readonly Point _newLocation;

        public MoveNodeCommand(DagEditorViewModel viewModel, Guid nodeId, Point oldLocation, Point newLocation)
        {
            _viewModel = viewModel;
            _nodeId = nodeId;
            _oldLocation = oldLocation;
            _newLocation = newLocation;
        }

        public void Execute()
        {
            _viewModel.FindNode(_nodeId)?.NodeInstance?.MoveTo(_newLocation);

            // H-1 viewer sync: Redo 시 viewer 위치 갱신. Undo/Redo batch 안이면 Flush suppressed.
            _viewModel.NotifyViewerNodeMoved(_nodeId, _newLocation);
        }

        public void Undo()
        {
            _viewModel.FindNode(_nodeId)?.NodeInstance?.MoveTo(_oldLocation);

            // H-1 viewer sync: Undo 시 viewer 위치 복원. Undo batch 안이면 Flush suppressed.
            _viewModel.NotifyViewerNodeMoved(_nodeId, _oldLocation);
        }
    }
}
