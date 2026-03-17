namespace DagEdit.Tests;

using Avalonia;
using Xunit;

/// <summary>
/// <see cref="DagItems"/>, <see cref="DagNode"/>, <see cref="DagConnection"/> 모델 클래스의 단위 테스트.
///
/// 이 클래스들은 순수 데이터 모델이며 Avalonia UI 컨트롤을 직접 생성하지 않으므로
/// Avalonia 초기화 없이 [Fact]로 테스트 가능하다.
/// </summary>
public class DagItemsTests
{
    // ─── DagItems.CreateDagNode ───────────────────────────────────

    [Fact]
    public void CreateDagNode_WithValidPoint_SetsNodeItemNotNull()
    {
        var item = new DagItems();

        item.CreateDagNode(new Point(10, 20));

        Assert.NotNull(item.NodeItem);
    }

    [Fact]
    public void CreateDagNode_WithValidPoint_AssignsNonEmptyGuid()
    {
        var item = new DagItems();

        item.CreateDagNode(new Point(10, 20));

        Assert.NotNull(item.NodeItem!.NodeId);
        Assert.NotEqual(Guid.Empty, item.NodeItem.NodeId!.Value);
    }

    [Fact]
    public void CreateDagNode_TwoCalls_ProduceUniqueNodeIds()
    {
        var item1 = new DagItems();
        var item2 = new DagItems();

        item1.CreateDagNode(new Point(0, 0));
        item2.CreateDagNode(new Point(0, 0));

        Assert.NotEqual(item1.NodeItem!.NodeId, item2.NodeItem!.NodeId);
    }

    [Fact]
    public void CreateDagNode_SetsLocation()
    {
        var location = new Point(42, 84);
        var item = new DagItems();

        item.CreateDagNode(location);

        Assert.Equal(location, item.NodeItem!.Location);
    }

    [Fact]
    public void CreateDagNode_SetsTypeAsRunnerNode()
    {
        var item = new DagItems();

        item.CreateDagNode(new Point(0, 0));

        Assert.Equal(DagItemsType.RunnerNode, item.NodeItem!.DAGItemType);
    }

    [Fact]
    public void CreateDagNode_DoesNotSetConnectionItem()
    {
        var item = new DagItems();

        item.CreateDagNode(new Point(0, 0));

        Assert.Null(item.ConnectionItem);
    }

    [Fact]
    public void CreateDagNode_InitializesEmptySourceConnections()
    {
        var item = new DagItems();

        item.CreateDagNode(new Point(0, 0));

        Assert.NotNull(item.NodeItem!.SourceConnections);
        Assert.Empty(item.NodeItem.SourceConnections);
    }

    [Fact]
    public void CreateDagNode_InitializesEmptyTargetConnections()
    {
        var item = new DagItems();

        item.CreateDagNode(new Point(0, 0));

        Assert.NotNull(item.NodeItem!.TargetConnections);
        Assert.Empty(item.NodeItem.TargetConnections);
    }

    // ─── DagItems.CreateDagConnection ────────────────────────────

    [Fact]
    public void CreateDagConnection_WithValidAnchors_SetsConnectionItemNotNull()
    {
        var item = new DagItems();

        item.CreateDagConnection(
            new Point(0, 0), Guid.NewGuid(),
            new Point(100, 100), Guid.NewGuid());

        Assert.NotNull(item.ConnectionItem);
    }

    [Fact]
    public void CreateDagConnection_AssignsNonEmptyConnectionId()
    {
        var item = new DagItems();

        item.CreateDagConnection(
            new Point(0, 0), Guid.NewGuid(),
            new Point(100, 100), Guid.NewGuid());

        Assert.NotNull(item.ConnectionItem!.ConnectionId);
        Assert.NotEqual(Guid.Empty, item.ConnectionItem.ConnectionId!.Value);
    }

    [Fact]
    public void CreateDagConnection_TwoCalls_ProduceUniqueConnectionIds()
    {
        var item1 = new DagItems();
        var item2 = new DagItems();

        item1.CreateDagConnection(
            new Point(0, 0), Guid.NewGuid(), new Point(100, 100), Guid.NewGuid());
        item2.CreateDagConnection(
            new Point(0, 0), Guid.NewGuid(), new Point(100, 100), Guid.NewGuid());

        Assert.NotEqual(item1.ConnectionItem!.ConnectionId, item2.ConnectionItem!.ConnectionId);
    }

    [Fact]
    public void CreateDagConnection_SetsSourceAnchor()
    {
        var source = new Point(10, 20);
        var item = new DagItems();

        item.CreateDagConnection(source, null, new Point(100, 100), null);

        Assert.Equal(source, item.ConnectionItem!.SourceAnchor);
    }

    [Fact]
    public void CreateDagConnection_SetsTargetAnchor()
    {
        var target = new Point(200, 300);
        var item = new DagItems();

        item.CreateDagConnection(new Point(0, 0), null, target, null);

        Assert.Equal(target, item.ConnectionItem!.TargetAnchor);
    }

    [Fact]
    public void CreateDagConnection_SetsSourceAndTargetNodeIds()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var item = new DagItems();

        item.CreateDagConnection(
            new Point(0, 0), sourceId,
            new Point(100, 100), targetId);

        Assert.Equal(sourceId, item.ConnectionItem!.SourceNodeId);
        Assert.Equal(targetId, item.ConnectionItem.TargetNodeId);
    }

    [Fact]
    public void CreateDagConnection_SetsTypeAsConnection()
    {
        var item = new DagItems();

        item.CreateDagConnection(
            new Point(0, 0), null, new Point(100, 100), null);

        Assert.Equal(DagItemsType.Connection, item.ConnectionItem!.DAGItemType);
    }

    [Fact]
    public void CreateDagConnection_WithNullNodeIds_StillCreatesConnection()
    {
        var item = new DagItems();

        item.CreateDagConnection(new Point(0, 0), null, new Point(100, 100), null);

        Assert.NotNull(item.ConnectionItem);
        Assert.Null(item.ConnectionItem!.SourceNodeId);
        Assert.Null(item.ConnectionItem.TargetNodeId);
    }

    [Fact]
    public void CreateDagConnection_DoesNotSetNodeItem()
    {
        var item = new DagItems();

        item.CreateDagConnection(
            new Point(0, 0), Guid.NewGuid(), new Point(100, 100), Guid.NewGuid());

        Assert.Null(item.NodeItem);
    }
}
