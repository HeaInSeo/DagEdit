namespace DagEdit
{
    /// <summary>
    /// 커넥션 삭제 명령. 생성자에서 스냅샷 캡처. Undo = RestoreDagConnectionItem.
    /// </summary>
    internal sealed class DelConnectionCommand : IUndoableCommand
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
}
