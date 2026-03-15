namespace DagEdit.Tests;

using Avalonia;
using Xunit;

/// <summary>
/// <see cref="Dag"/> 클래스의 단위 테스트.
///
/// 테스트 설계 원칙:
/// - Dag() 생성자는 빈 상태로 시작한다 (seed data 없음).
/// - DagItems.CreateDagNode()는 Node UI 컨트롤을 생성하지 않으므로
///   Avalonia 초기화 없이 [Fact]로 실행 가능하다.
/// - DelDagNodeItem()의 해피패스 테스트는 NodeInstance 설정이 필요하여
///   현재 모델 레이어에서 검증 불가. 향후 아키텍처 분리 후 추가 예정.
/// - Add* 메서드는 성공 시 DagItems를, 실패 시 null을 반환한다.
/// </summary>
public class DagTests
{
    // ─── AddDagNodeItem ──────────────────────────────────────────

    [Fact]
    public void AddDagNodeItem_WithValidPoint_ReturnsNotNull()
    {
        var dag = new Dag();

        var result = dag.AddDagNodeItem(new Point(100, 200));

        Assert.NotNull(result);
    }

    [Fact]
    public void AddDagNodeItem_WithValidPoint_IncreasesItemCount()
    {
        var dag = new Dag();
        var initialCount = dag.DAGItemsSource.Count;

        dag.AddDagNodeItem(new Point(100, 200));

        Assert.Equal(initialCount + 1, dag.DAGItemsSource.Count);
    }

    [Fact]
    public void AddDagNodeItem_WithNullPoint_ReturnsNull()
    {
        var dag = new Dag();

        var result = dag.AddDagNodeItem(null);

        Assert.Null(result);
    }

    [Fact]
    public void AddDagNodeItem_WithNullPoint_DoesNotChangeItemCount()
    {
        var dag = new Dag();
        var initialCount = dag.DAGItemsSource.Count;

        dag.AddDagNodeItem(null);

        Assert.Equal(initialCount, dag.DAGItemsSource.Count);
    }

    [Fact]
    public void AddDagNodeItem_AddedItem_HasNodeItemSet()
    {
        var dag = new Dag();

        dag.AddDagNodeItem(new Point(50, 75));

        var lastItem = dag.DAGItemsSource[dag.DAGItemsSource.Count - 1];
        Assert.NotNull(lastItem.NodeItem);
        Assert.Null(lastItem.ConnectionItem); // 노드 아이템은 ConnectionItem이 없어야 함
    }

    // ─── AddDagConnectionItem ────────────────────────────────────

    [Fact]
    public void AddDagConnectionItem_WithValidPoints_ReturnsNotNull()
    {
        var dag = new Dag();

        var result = dag.AddDagConnectionItem(
            new Point(0, 0), System.Guid.NewGuid(),
            new Point(100, 100), System.Guid.NewGuid());

        Assert.NotNull(result);
    }

    [Fact]
    public void AddDagConnectionItem_WithValidPoints_IncreasesItemCount()
    {
        var dag = new Dag();
        var initialCount = dag.DAGItemsSource.Count;

        dag.AddDagConnectionItem(
            new Point(0, 0), System.Guid.NewGuid(),
            new Point(100, 100), System.Guid.NewGuid());

        Assert.Equal(initialCount + 1, dag.DAGItemsSource.Count);
    }

    [Fact]
    public void AddDagConnectionItem_WithNullSource_ReturnsNull()
    {
        var dag = new Dag();

        var result = dag.AddDagConnectionItem(
            null, System.Guid.NewGuid(),
            new Point(100, 100), System.Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void AddDagConnectionItem_WithNullTarget_ReturnsNull()
    {
        var dag = new Dag();

        var result = dag.AddDagConnectionItem(
            new Point(0, 0), System.Guid.NewGuid(),
            null, System.Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void AddDagConnectionItem_WithBothNullPoints_ReturnsNull()
    {
        var dag = new Dag();

        var result = dag.AddDagConnectionItem(null, null, null, null);

        Assert.Null(result);
    }

    [Fact]
    public void AddDagConnectionItem_AddedItem_HasConnectionItemSet()
    {
        var dag = new Dag();

        dag.AddDagConnectionItem(
            new Point(10, 20), System.Guid.NewGuid(),
            new Point(30, 40), System.Guid.NewGuid());

        var lastItem = dag.DAGItemsSource[dag.DAGItemsSource.Count - 1];
        Assert.NotNull(lastItem.ConnectionItem);
        Assert.Null(lastItem.NodeItem); // 커넥션 아이템은 NodeItem이 없어야 함
    }

    // ─── DelDagNodeItem ──────────────────────────────────────────

    [Fact]
    public void DelDagNodeItem_WithNullId_ReturnsFalse()
    {
        var dag = new Dag();

        var result = dag.DelDagNodeItem(null);

        Assert.False(result);
    }

    [Fact]
    public void DelDagNodeItem_WithNonExistentId_ReturnsFalse()
    {
        var dag = new Dag();

        var result = dag.DelDagNodeItem(System.Guid.NewGuid()); // 존재하지 않는 ID

        Assert.False(result);
    }

    [Fact]
    public void DelDagNodeItem_WithValidIdButNoNodeInstance_ReturnsTrue()
    {
        var dag = new Dag();
        dag.AddDagNodeItem(new Point(10, 10));
        var firstNodeId = dag.DAGItemsSource[0].NodeItem!.NodeId;

        var result = dag.DelDagNodeItem(firstNodeId);

        Assert.True(result);
    }

    [Fact]
    public void DelDagNodeItem_WithValidIdButNoNodeInstance_RemovesNode()
    {
        var dag = new Dag();
        dag.AddDagNodeItem(new Point(10, 10));
        var firstNodeId = dag.DAGItemsSource[0].NodeItem!.NodeId!.Value;

        dag.DelDagNodeItem(firstNodeId);

        Assert.Empty(dag.DAGItemsSource);
        Assert.Null(dag.FindNode(firstNodeId));
    }

    [Fact]
    public void DelDagNodeItem_WithValidIdButNoNodeInstance_RemovesConnectedItems()
    {
        var dag = new Dag();
        var source = dag.AddDagNodeItem(new Point(10, 10))!;
        var target = dag.AddDagNodeItem(new Point(40, 10))!;
        dag.AddDagConnectionItem(
            new Point(20, 20), source.NodeItem!.NodeId,
            new Point(50, 20), target.NodeItem!.NodeId);

        dag.DelDagNodeItem(source.NodeItem.NodeId);

        Assert.Single(dag.DAGItemsSource);
        Assert.NotNull(dag.DAGItemsSource[0].NodeItem);
        Assert.Empty(target.NodeItem.TargetConnections);
    }

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var dag = new Dag();

        var ex = Record.Exception(() =>
        {
            dag.Dispose();
            dag.Dispose();
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_DisposesUnderlyingSourceList()
    {
        var dag = new Dag();
        dag.Dispose();

        var ex = Assert.Throws<ObjectDisposedException>(() => dag.AddDagNodeItem(new Point(1, 1)));
        Assert.Contains(nameof(Dag), ex.ObjectName ?? string.Empty);
    }

    // ─── DelDagConnectionItem ────────────────────────────────────

    [Fact]
    public void DelDagConnectionItem_WithNullId_ReturnsFalse()
    {
        var dag = new Dag();

        var result = dag.DelDagConnectionItem(null);

        Assert.False(result);
    }

    [Fact]
    public void DelDagConnectionItem_WithNonExistentId_ReturnsFalse()
    {
        var dag = new Dag();

        var result = dag.DelDagConnectionItem(System.Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public void DelDagConnectionItem_WithValidId_ReturnsTrue()
    {
        var dag = new Dag();
        dag.AddDagConnectionItem(new Point(0, 0), System.Guid.NewGuid(), new Point(100, 100), System.Guid.NewGuid());
        var connectionId = dag.DAGItemsSource[0].ConnectionItem!.ConnectionId!.Value;

        var result = dag.DelDagConnectionItem(connectionId);

        Assert.True(result);
    }

    [Fact]
    public void DelDagConnectionItem_WithValidId_DecreasesItemCount()
    {
        var dag = new Dag();
        dag.AddDagConnectionItem(new Point(0, 0), System.Guid.NewGuid(), new Point(100, 100), System.Guid.NewGuid());
        var initialCount = dag.DAGItemsSource.Count;
        var connectionId = dag.DAGItemsSource[0].ConnectionItem!.ConnectionId!.Value;

        dag.DelDagConnectionItem(connectionId);

        Assert.Equal(initialCount - 1, dag.DAGItemsSource.Count);
    }

    [Fact]
    public void DelDagConnectionItem_WithValidId_ConnectionNoLongerInItems()
    {
        var dag = new Dag();
        dag.AddDagConnectionItem(new Point(0, 0), System.Guid.NewGuid(), new Point(100, 100), System.Guid.NewGuid());
        var connectionId = dag.DAGItemsSource[0].ConnectionItem!.ConnectionId!.Value;

        dag.DelDagConnectionItem(connectionId);

        Assert.DoesNotContain(dag.DAGItemsSource, i => i.ConnectionItem?.ConnectionId == connectionId);
    }

    [Fact]
    public void DelDagConnectionItem_TwiceWithSameId_SecondReturnsFalse()
    {
        var dag = new Dag();
        dag.AddDagConnectionItem(new Point(0, 0), System.Guid.NewGuid(), new Point(100, 100), System.Guid.NewGuid());
        var connectionId = dag.DAGItemsSource[0].ConnectionItem!.ConnectionId!.Value;

        dag.DelDagConnectionItem(connectionId);
        var result = dag.DelDagConnectionItem(connectionId);

        Assert.False(result);
    }

    // ─── FindNode ────────────────────────────────────────────────

    [Fact]
    public void FindNode_ReturnsNodeAfterAdding()
    {
        var dag = new Dag();
        dag.AddDagNodeItem(new Point(10, 20));
        var nodeId = dag.DAGItemsSource[0].NodeItem!.NodeId!.Value;

        var result = dag.FindNode(nodeId);

        Assert.NotNull(result);
        Assert.Equal(nodeId, result.NodeId);
    }

    [Fact]
    public void FindNode_ReturnsNullForNonExistentId()
    {
        var dag = new Dag();

        var result = dag.FindNode(System.Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void FindNode_ReturnsCorrectNodeAmongMultiple()
    {
        var dag = new Dag();
        dag.AddDagNodeItem(new Point(0, 0));
        dag.AddDagNodeItem(new Point(100, 0));
        dag.AddDagNodeItem(new Point(200, 0));
        var targetId = dag.DAGItemsSource[1].NodeItem!.NodeId!.Value;

        var result = dag.FindNode(targetId);

        Assert.NotNull(result);
        Assert.Equal(targetId, result.NodeId);
    }

    [Fact]
    public void FindNode_AfterAddConnectionItem_SourceNodeHasConnection()
    {
        var dag = new Dag();
        dag.AddDagNodeItem(new Point(0, 0));
        dag.AddDagNodeItem(new Point(100, 0));
        var sourceId = dag.DAGItemsSource[0].NodeItem!.NodeId!.Value;
        var targetId = dag.DAGItemsSource[1].NodeItem!.NodeId!.Value;

        dag.AddDagConnectionItem(new Point(0, 0), sourceId, new Point(100, 0), targetId);

        var sourceNode = dag.FindNode(sourceId);
        Assert.NotNull(sourceNode);
        Assert.Single(sourceNode.SourceConnections);
    }

    [Fact]
    public void FindNode_AfterAddConnectionItem_TargetNodeHasConnection()
    {
        var dag = new Dag();
        dag.AddDagNodeItem(new Point(0, 0));
        dag.AddDagNodeItem(new Point(100, 0));
        var sourceId = dag.DAGItemsSource[0].NodeItem!.NodeId!.Value;
        var targetId = dag.DAGItemsSource[1].NodeItem!.NodeId!.Value;

        dag.AddDagConnectionItem(new Point(0, 0), sourceId, new Point(100, 0), targetId);

        var targetNode = dag.FindNode(targetId);
        Assert.NotNull(targetNode);
        Assert.Single(targetNode.TargetConnections);
    }

    // ─── RemoveDagItem / Restore (Undo/Redo 보조) ────────────────

    [Fact]
    public void RemoveDagItem_RemovesNodeFromSourceList()
    {
        var dag = new Dag();
        var item = dag.AddDagNodeItem(new Point(0, 0))!;

        dag.RemoveDagItem(item);

        Assert.Empty(dag.DAGItemsSource);
    }

    [Fact]
    public void RemoveDagItem_RemovesConnectionFromNodeLists()
    {
        var dag = new Dag();
        dag.AddDagNodeItem(new Point(0, 0));
        dag.AddDagNodeItem(new Point(100, 0));
        var sourceId = dag.DAGItemsSource[0].NodeItem!.NodeId!.Value;
        var targetId = dag.DAGItemsSource[1].NodeItem!.NodeId!.Value;
        var connItem = dag.AddDagConnectionItem(new Point(0, 0), sourceId, new Point(100, 0), targetId)!;

        dag.RemoveDagItem(connItem);

        Assert.Empty(dag.FindNode(sourceId)!.SourceConnections);
        Assert.Empty(dag.FindNode(targetId)!.TargetConnections);
    }

    [Fact]
    public void RestoreDagNodeItem_RestoresItemToSourceList()
    {
        var dag = new Dag();
        var item = dag.AddDagNodeItem(new Point(0, 0))!;
        dag.RemoveDagItem(item);

        dag.RestoreDagNodeItem(item);

        Assert.Single(dag.DAGItemsSource);
    }

    [Fact]
    public void RestoreDagConnectionItem_RestoresConnectionAndNodeLists()
    {
        var dag = new Dag();
        dag.AddDagNodeItem(new Point(0, 0));
        dag.AddDagNodeItem(new Point(100, 0));
        var sourceId = dag.DAGItemsSource[0].NodeItem!.NodeId!.Value;
        var targetId = dag.DAGItemsSource[1].NodeItem!.NodeId!.Value;
        var connItem = dag.AddDagConnectionItem(new Point(0, 0), sourceId, new Point(100, 0), targetId)!;

        dag.RemoveDagItem(connItem);
        dag.RestoreDagConnectionItem(connItem);

        Assert.Single(dag.FindNode(sourceId)!.SourceConnections);
        Assert.Single(dag.FindNode(targetId)!.TargetConnections);
        Assert.Contains(dag.DAGItemsSource, i => i.ConnectionItem?.ConnectionId == connItem.ConnectionItem!.ConnectionId);
    }

    // ─── GetConnectionItemsForNode ───────────────────────────────

    [Fact]
    public void GetConnectionItemsForNode_ReturnsRelatedConnections()
    {
        var dag = new Dag();
        dag.AddDagNodeItem(new Point(0, 0));
        dag.AddDagNodeItem(new Point(100, 0));
        var sourceId = dag.DAGItemsSource[0].NodeItem!.NodeId!.Value;
        var targetId = dag.DAGItemsSource[1].NodeItem!.NodeId!.Value;
        dag.AddDagConnectionItem(new Point(0, 0), sourceId, new Point(100, 0), targetId);

        var result = dag.GetConnectionItemsForNode(sourceId);

        Assert.Single(result);
    }

    // ─── 생성자 초기 상태 ─────────────────────────────────────

    [Fact]
    public void Constructor_StartsEmpty()
    {
        var dag = new Dag();

        Assert.Empty(dag.DAGItemsSource);
    }
}
