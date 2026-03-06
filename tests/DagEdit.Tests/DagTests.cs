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
/// </summary>
public class DagTests
{
    // ─── AddDagNodeItem ──────────────────────────────────────────

    [Fact]
    public void AddDagNodeItem_WithValidPoint_ReturnsTrue()
    {
        var dag = new Dag();

        var result = dag.AddDagNodeItem(new Point(100, 200));

        Assert.True(result);
    }

    [Fact]
    public void AddDagNodeItem_WithValidPoint_IncreasesItemCount()
    {
        var dag = new Dag();
        var initialCount = dag.DAGItemsSource.Count; // 생성자에서 3개 추가됨

        dag.AddDagNodeItem(new Point(100, 200));

        Assert.Equal(initialCount + 1, dag.DAGItemsSource.Count);
    }

    [Fact]
    public void AddDagNodeItem_WithNullPoint_ReturnsFalse()
    {
        var dag = new Dag();

        var result = dag.AddDagNodeItem(null);

        Assert.False(result);
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
    public void AddDagConnectionItem_WithValidPoints_ReturnsTrue()
    {
        var dag = new Dag();

        var result = dag.AddDagConnectionItem(
            new Point(0, 0), System.Guid.NewGuid(),
            new Point(100, 100), System.Guid.NewGuid());

        Assert.True(result);
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
    public void AddDagConnectionItem_WithNullSource_ReturnsFalse()
    {
        var dag = new Dag();

        var result = dag.AddDagConnectionItem(
            null, System.Guid.NewGuid(),
            new Point(100, 100), System.Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public void AddDagConnectionItem_WithNullTarget_ReturnsFalse()
    {
        var dag = new Dag();

        var result = dag.AddDagConnectionItem(
            new Point(0, 0), System.Guid.NewGuid(),
            null, System.Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public void AddDagConnectionItem_WithBothNullPoints_ReturnsFalse()
    {
        var dag = new Dag();

        var result = dag.AddDagConnectionItem(null, null, null, null);

        Assert.False(result);
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
    public void DelDagNodeItem_WithValidIdButNoNodeInstance_ReturnsFalse()
    {
        // 현재 구현: NodeInstance가 null인 경우 삭제 거부 (안전 장치)
        // CreateDagNode()는 NodeInstance를 설정하지 않으므로
        // 단순 모델 레이어 테스트에서는 항상 false를 반환한다.
        // 해피패스 테스트는 UI와 통합된 [AvaloniaFact] 테스트로 작성 예정.
        var dag = new Dag();
        dag.AddDagNodeItem(new Point(10, 10)); // 테스트 전제: 노드 1개 명시적 추가
        var firstNodeId = dag.DAGItemsSource[0].NodeItem!.NodeId;

        var result = dag.DelDagNodeItem(firstNodeId);

        Assert.False(result); // NodeInstance == null이므로 false 반환 (현재 설계상 정상)
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

    // ─── 생성자 초기 상태 ─────────────────────────────────────

    [Fact]
    public void Constructor_StartsEmpty()
    {
        var dag = new Dag();

        Assert.Empty(dag.DAGItemsSource);
    }
}
