using System;
using Avalonia;

namespace DagEdit
{
    /// <summary>
    /// 커넥션 추가 명령. Undo = RemoveDagItem. Redo = RestoreDagConnectionItem.
    /// </summary>
    internal sealed class AddConnectionCommand : IUndoableCommand
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
}
