namespace DagEdit.Tests;

using Avalonia;
using Xunit;

/// <summary>
/// <see cref="DagEditorViewModel"/> 단위 테스트.
///
/// - Avalonia 초기화 없이 [Fact]로 실행 가능.
/// - NodeCount/ConnectionCount는 DynamicData OAPH로 파생된 반응형 상태.
/// </summary>
public class DagEditorViewModelTests
{
    // ─── 초기 상태 ───────────────────────────────────────────────

    [Fact]
    public void Constructor_NodeCount_StartsAtZero()
    {
        using var vm = new DagEditorViewModel();

        Assert.Equal(0, vm.NodeCount);
    }

    [Fact]
    public void Constructor_ConnectionCount_StartsAtZero()
    {
        using var vm = new DagEditorViewModel();

        Assert.Equal(0, vm.ConnectionCount);
    }

    // ─── NodeCount 반응형 갱신 ───────────────────────────────────

    [Fact]
    public void NodeCount_IncreasesWhenNodeAdded()
    {
        using var vm = new DagEditorViewModel();

        vm.AddDagNodeItem(new Point(0, 0));

        Assert.Equal(1, vm.NodeCount);
    }

    [Fact]
    public void NodeCount_ReflectsMultipleAdditions()
    {
        using var vm = new DagEditorViewModel();

        vm.AddDagNodeItem(new Point(0, 0));
        vm.AddDagNodeItem(new Point(100, 0));
        vm.AddDagNodeItem(new Point(200, 0));

        Assert.Equal(3, vm.NodeCount);
    }

    [Fact]
    public void NodeCount_NotAffectedByConnectionAddition()
    {
        using var vm = new DagEditorViewModel();

        vm.AddDagConnectionItem(new Point(0, 0), System.Guid.NewGuid(), new Point(100, 100), System.Guid.NewGuid());

        Assert.Equal(0, vm.NodeCount);
    }

    // ─── ConnectionCount 반응형 갱신 ─────────────────────────────

    [Fact]
    public void ConnectionCount_IncreasesWhenConnectionAdded()
    {
        using var vm = new DagEditorViewModel();

        vm.AddDagConnectionItem(new Point(0, 0), System.Guid.NewGuid(), new Point(100, 100), System.Guid.NewGuid());

        Assert.Equal(1, vm.ConnectionCount);
    }

    [Fact]
    public void ConnectionCount_ReflectsMultipleAdditions()
    {
        using var vm = new DagEditorViewModel();

        vm.AddDagConnectionItem(new Point(0, 0), System.Guid.NewGuid(), new Point(100, 100), System.Guid.NewGuid());
        vm.AddDagConnectionItem(new Point(0, 0), System.Guid.NewGuid(), new Point(200, 100), System.Guid.NewGuid());

        Assert.Equal(2, vm.ConnectionCount);
    }

    [Fact]
    public void ConnectionCount_NotAffectedByNodeAddition()
    {
        using var vm = new DagEditorViewModel();

        vm.AddDagNodeItem(new Point(0, 0));

        Assert.Equal(0, vm.ConnectionCount);
    }

    [Fact]
    public void ConnectionCount_DecreasesWhenConnectionDeleted()
    {
        using var vm = new DagEditorViewModel();
        vm.AddDagConnectionItem(new Point(0, 0), System.Guid.NewGuid(), new Point(100, 100), System.Guid.NewGuid());
        var connectionId = vm.Items[0].ConnectionItem!.ConnectionId!.Value;

        vm.DelDagConnectionItem(connectionId);

        Assert.Equal(0, vm.ConnectionCount);
    }

    // ─── 노드+연결 혼합 시나리오 ──────────────────────────────────

    [Fact]
    public void NodeCountAndConnectionCount_TrackIndependently()
    {
        using var vm = new DagEditorViewModel();

        vm.AddDagNodeItem(new Point(0, 0));
        vm.AddDagNodeItem(new Point(100, 0));
        vm.AddDagConnectionItem(new Point(0, 0), System.Guid.NewGuid(), new Point(100, 100), System.Guid.NewGuid());

        Assert.Equal(2, vm.NodeCount);
        Assert.Equal(1, vm.ConnectionCount);
    }
}
