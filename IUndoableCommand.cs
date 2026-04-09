namespace DagEdit
{
    internal interface IUndoableCommand
    {
        void Execute();

        void Undo();
    }
}
