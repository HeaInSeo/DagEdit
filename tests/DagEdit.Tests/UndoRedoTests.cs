namespace DagEdit.Tests;

using Avalonia;
using Xunit;

/// <summary>
/// UndoRedoStack + Undo/Redo 명령 단위 테스트.
///
/// - Avalonia 초기화 없이 [Fact]로 실행 가능.
/// - AddNode/DelConnection의 Execute/Undo/Redo 라이프사이클 검증.
/// </summary>
public class UndoRedoTests
{
    // ─── UndoRedoStack 기본 동작 ─────────────────────────────────

    [Fact]
    public void Stack_InitialState_CannotUndoOrRedo()
    {
        using var stack = new UndoRedoStack();

        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Stack_AfterExecute_CanUndo()
    {
        using var stack = new UndoRedoStack();
        var cmd = new CounterCommand();

        stack.Execute(cmd);

        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Stack_AfterUndo_CanRedo()
    {
        using var stack = new UndoRedoStack();
        stack.Execute(new CounterCommand());

        stack.Undo();

        Assert.False(stack.CanUndo);
        Assert.True(stack.CanRedo);
    }

    [Fact]
    public void Stack_AfterRedo_CanUndo()
    {
        using var stack = new UndoRedoStack();
        stack.Execute(new CounterCommand());
        stack.Undo();

        stack.Redo();

        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Stack_NewExecuteAfterUndo_ClearsRedoStack()
    {
        using var stack = new UndoRedoStack();
        stack.Execute(new CounterCommand());
        stack.Undo();

        stack.Execute(new CounterCommand());

        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Stack_UndoOnEmpty_DoesNotThrow()
    {
        using var stack = new UndoRedoStack();

        Exception? ex = Record.Exception(() => stack.Undo());

        Assert.Null(ex);
    }

    [Fact]
    public void Stack_RedoOnEmpty_DoesNotThrow()
    {
        using var stack = new UndoRedoStack();

        Exception? ex = Record.Exception(() => stack.Redo());

        Assert.Null(ex);
    }

    // ─── Execute/Undo/Redo 호출 횟수 검증 ─────────────────────────

    [Fact]
    public void Command_Execute_CalledOnce()
    {
        using var stack = new UndoRedoStack();
        var cmd = new CounterCommand();

        stack.Execute(cmd);

        Assert.Equal(1, cmd.ExecuteCount);
    }

    [Fact]
    public void Command_Undo_CalledOnce()
    {
        using var stack = new UndoRedoStack();
        var cmd = new CounterCommand();
        stack.Execute(cmd);

        stack.Undo();

        Assert.Equal(1, cmd.UndoCount);
    }

    [Fact]
    public void Command_Redo_ExecutesAgain()
    {
        using var stack = new UndoRedoStack();
        var cmd = new CounterCommand();
        stack.Execute(cmd);
        stack.Undo();

        stack.Redo();

        Assert.Equal(2, cmd.ExecuteCount);
        Assert.Equal(1, cmd.UndoCount);
    }

    // ─── AddNodeCommand (Dag 레이어 통합) ─────────────────────────

    [Fact]
    public void AddNodeCommand_Execute_AddsNode()
    {
        using var stack = new UndoRedoStack();
        var dag = new Dag();
        var cmd = new AddNodeCommand(dag, new Point(0, 0));

        stack.Execute(cmd);

        Assert.Single(dag.DAGItemsSource);
    }

    [Fact]
    public void AddNodeCommand_Undo_RemovesNode()
    {
        using var stack = new UndoRedoStack();
        var dag = new Dag();
        stack.Execute(new AddNodeCommand(dag, new Point(0, 0)));

        stack.Undo();

        Assert.Empty(dag.DAGItemsSource);
    }

    [Fact]
    public void AddNodeCommand_Redo_RestoresNode()
    {
        using var stack = new UndoRedoStack();
        var dag = new Dag();
        stack.Execute(new AddNodeCommand(dag, new Point(0, 0)));
        stack.Undo();

        stack.Redo();

        Assert.Single(dag.DAGItemsSource);
    }

    // ─── DelConnectionCommand (Dag 레이어 통합) ───────────────────

    [Fact]
    public void DelConnectionCommand_Execute_RemovesConnection()
    {
        using var stack = new UndoRedoStack();
        var dag = new Dag();
        DagItems connItem = dag.AddDagConnectionItem(
            new Point(0, 0), Guid.NewGuid(),
            new Point(100, 0), Guid.NewGuid())!;

        stack.Execute(new DelConnectionCommand(dag, connItem));

        Assert.Empty(dag.DAGItemsSource);
    }

    [Fact]
    public void DelConnectionCommand_Undo_RestoresConnection()
    {
        using var stack = new UndoRedoStack();
        var dag = new Dag();
        DagItems connItem = dag.AddDagConnectionItem(
            new Point(0, 0), Guid.NewGuid(),
            new Point(100, 0), Guid.NewGuid())!;
        stack.Execute(new DelConnectionCommand(dag, connItem));

        stack.Undo();

        Assert.Single(dag.DAGItemsSource);
    }

    // ─── AddConnectionCommand (Dag 레이어 통합) ───────────────────

    [Fact]
    public void AddConnectionCommand_Execute_AddsConnection()
    {
        using var stack = new UndoRedoStack();
        var dag = new Dag();

        stack.Execute(new AddConnectionCommand(
            dag,
            new Point(0, 0), Guid.NewGuid(),
            new Point(100, 0), Guid.NewGuid()));

        Assert.Single(dag.DAGItemsSource);
    }

    [Fact]
    public void AddConnectionCommand_Undo_RemovesConnection()
    {
        using var stack = new UndoRedoStack();
        var dag = new Dag();
        stack.Execute(new AddConnectionCommand(
            dag,
            new Point(0, 0), Guid.NewGuid(),
            new Point(100, 0), Guid.NewGuid()));

        stack.Undo();

        Assert.Empty(dag.DAGItemsSource);
    }

    // ─── DagEditorViewModel Undo/Redo 통합 ───────────────────────

    [Fact]
    public void ViewModel_ExecuteAddNode_CanUndoAfter()
    {
        using var vm = new DagEditorViewModel();

        vm.ExecuteAddNode(new Point(0, 0));

        Assert.True(vm.CanUndo);
    }

    [Fact]
    public void ViewModel_Undo_RemovesAddedNode()
    {
        using var vm = new DagEditorViewModel();
        vm.ExecuteAddNode(new Point(0, 0));

        vm.Undo();

        Assert.Equal(0, vm.NodeCount);
    }

    [Fact]
    public void ViewModel_Redo_RestoresNode()
    {
        using var vm = new DagEditorViewModel();
        vm.ExecuteAddNode(new Point(0, 0));
        vm.Undo();

        vm.Redo();

        Assert.Equal(1, vm.NodeCount);
    }

    [Fact]
    public void ViewModel_ExecuteDelNode_NodeInstanceNull_RemovesAndUndoRestores()
    {
        using var vm = new DagEditorViewModel();
        vm.ExecuteAddNode(new Point(0, 0));
        Guid nodeId = vm.Items[0].NodeItem!.NodeId!.Value;

        vm.ExecuteDelNode(nodeId);

        Assert.Equal(0, vm.NodeCount);

        vm.Undo();

        Assert.Equal(1, vm.NodeCount);
        Assert.Equal(nodeId, vm.Items[0].NodeItem!.NodeId);
        Assert.Null(vm.Items[0].NodeItem!.NodeInstance);
    }
}

/// <summary>테스트용 더미 명령: Execute/Undo 호출 횟수를 추적한다.</summary>
internal sealed class CounterCommand : IUndoableCommand
{
    public int ExecuteCount { get; private set; }
    public int UndoCount { get; private set; }

    public void Execute() => ExecuteCount++;
    public void Undo() => UndoCount++;
}
