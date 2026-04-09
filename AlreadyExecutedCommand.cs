namespace DagEdit
{
    /// <summary>
    /// 이미 수행된 동작을 Undo/Redo 스택에 등록하기 위한 래퍼.
    /// Execute()는 첫 번째 호출(등록 시)을 건너뛰고, 이후 Redo 경로에서만 inner를 수행한다.
    /// </summary>
    internal sealed class AlreadyExecutedCommand : IUndoableCommand
    {
        private readonly IUndoableCommand _inner;
        private bool _firstTime = true;

        public AlreadyExecutedCommand(IUndoableCommand inner)
        {
            _inner = inner;
        }

        public void Execute()
        {
            if (_firstTime)
            {
                _firstTime = false;
                return;
            }

            _inner.Execute();
        }

        public void Undo() => _inner.Undo();
    }
}
