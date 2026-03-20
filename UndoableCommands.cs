using System;
using System.Collections.Generic;
using Avalonia;

namespace DagEdit
{
    /// <summary>
    /// 노드 추가 명령. Undo = RemoveDagItem. Redo = RestoreDagNodeItem.
    /// </summary>
    public sealed class AddNodeCommand : IUndoableCommand
    {
        private readonly Dag _dag;
        private readonly Point _location;
        private DagItems? _item;

        public AddNodeCommand(Dag dag, Point location)
        {
            _dag = dag;
            _location = location;
        }

        public void Execute()
        {
            if (_item == null)
            {
                _item = _dag.AddDagNodeItem(_location);
            }
            else
            {
                _dag.RestoreDagNodeItem(_item);
            }
        }

        public void Undo()
        {
            if (_item != null)
            {
                _dag.RemoveDagItem(_item);
            }
        }
    }

    /// <summary>
    /// 노드 삭제 명령. 생성자에서 삭제 전 스냅샷을 캡처한다.
    /// Undo = RestoreDagNodeItem + 연결된 커넥션 복원.
    /// </summary>
    public sealed class DelNodeCommand : IUndoableCommand
    {
        private readonly Dag _dag;
        private readonly DagItems _nodeItem;
        private readonly List<DagItems> _connectionItems;

        public DelNodeCommand(Dag dag, DagItems nodeItem, List<DagItems> connectionItems)
        {
            _dag = dag;
            _nodeItem = nodeItem;
            _connectionItems = connectionItems;
        }

        public void Execute()
        {
            _dag.DelDagNodeItem(_nodeItem.NodeItem!.NodeId);
        }

        public void Undo()
        {
            _dag.RestoreDagNodeItem(_nodeItem);
            foreach (var connItem in _connectionItems)
            {
                _dag.RestoreDagConnectionItem(connItem);
            }
        }
    }

    /// <summary>
    /// 커넥션 추가 명령. Undo = RemoveDagItem. Redo = RestoreDagConnectionItem.
    /// </summary>
    public sealed class AddConnectionCommand : IUndoableCommand
    {
        private readonly Dag _dag;
        private readonly Point _source;
        private readonly Guid? _sourceNodeId;
        private readonly Point _target;
        private readonly Guid? _targetNodeId;
        private DagItems? _item;

        public AddConnectionCommand(Dag dag, Point source, Guid? sourceNodeId, Point target, Guid? targetNodeId)
        {
            _dag = dag;
            _source = source;
            _sourceNodeId = sourceNodeId;
            _target = target;
            _targetNodeId = targetNodeId;
        }

        public void Execute()
        {
            if (_item == null)
            {
                _item = _dag.AddDagConnectionItem(_source, _sourceNodeId, _target, _targetNodeId);
            }
            else
            {
                _dag.RestoreDagConnectionItem(_item);
            }
        }

        public void Undo()
        {
            if (_item != null)
            {
                _dag.RemoveDagItem(_item);
            }
        }
    }

    /// <summary>
    /// 커넥션 삭제 명령. 생성자에서 스냅샷 캡처. Undo = RestoreDagConnectionItem.
    /// </summary>
    public sealed class DelConnectionCommand : IUndoableCommand
    {
        private readonly Dag _dag;
        private readonly DagItems _connectionItem;

        public DelConnectionCommand(Dag dag, DagItems connectionItem)
        {
            _dag = dag;
            _connectionItem = connectionItem;
        }

        public void Execute()
        {
            _dag.DelDagConnectionItem(_connectionItem.ConnectionItem!.ConnectionId);
        }

        public void Undo()
        {
            _dag.RestoreDagConnectionItem(_connectionItem);
        }
    }

    /// <summary>
    /// 노드 이동 명령. Execute/Undo = Node.MoveTo(new/old). 드래그 완료 시 생성된다.
    /// </summary>
    public sealed class MoveNodeCommand : IUndoableCommand
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
