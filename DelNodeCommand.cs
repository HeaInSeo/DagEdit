using System.Collections.Generic;

namespace DagEdit
{
    /// <summary>
    /// 노드 삭제 명령. 생성자에서 삭제 전 스냅샷을 캡처한다.
    /// Undo = RestoreDagNodeItem + 연결된 커넥션 복원.
    /// </summary>
    internal sealed class DelNodeCommand : IUndoableCommand
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
}
