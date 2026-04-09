using Avalonia;

namespace DagEdit
{
    /// <summary>
    /// 노드 추가 명령. Undo = RemoveDagItem. Redo = RestoreDagNodeItem.
    /// </summary>
    internal sealed class AddNodeCommand : IUndoableCommand
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
}
