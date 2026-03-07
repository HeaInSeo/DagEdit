using System;
using System.Collections.Generic;
using ReactiveUI;

namespace DagEdit
{
    public interface IUndoableCommand
    {
        void Execute();

        void Undo();
    }

    /// <summary>
    /// Undo/Redo 스택. Execute → Undo → Redo 사이클을 관리한다.
    ///
    /// - Execute: 명령 수행 후 undoStack에 push, redoStack 초기화
    /// - Undo: undoStack에서 pop → 명령 되돌리기 → redoStack에 push
    /// - Redo: redoStack에서 pop → 명령 재수행 → undoStack에 push
    /// </summary>
    public sealed class UndoRedoStack : ReactiveObject, IDisposable
    {
        private readonly Stack<IUndoableCommand> _undoStack = new();
        private readonly Stack<IUndoableCommand> _redoStack = new();

        private bool _canUndo;

        public bool CanUndo
        {
            get => _canUndo;
            private set => this.RaiseAndSetIfChanged(ref _canUndo, value);
        }

        private bool _canRedo;

        public bool CanRedo
        {
            get => _canRedo;
            private set => this.RaiseAndSetIfChanged(ref _canRedo, value);
        }

        public void Execute(IUndoableCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();
            UpdateCanState();
        }

        public void Undo()
        {
            if (!_undoStack.TryPop(out var cmd))
            {
                return;
            }

            cmd.Undo();
            _redoStack.Push(cmd);
            UpdateCanState();
        }

        public void Redo()
        {
            if (!_redoStack.TryPop(out var cmd))
            {
                return;
            }

            cmd.Execute();
            _undoStack.Push(cmd);
            UpdateCanState();
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            UpdateCanState();
        }

        private void UpdateCanState()
        {
            CanUndo = _undoStack.Count > 0;
            CanRedo = _redoStack.Count > 0;
        }

        public void Dispose() => Clear();
    }
}
