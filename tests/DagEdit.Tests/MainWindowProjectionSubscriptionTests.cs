namespace DagEdit.Tests;

using Avalonia;
using Xunit;

public class MainWindowProjectionSubscriptionTests
{
    [Fact]
    public void AttachDetachAttach_DoesNotDuplicateProjectionRefresh()
    {
        var adapter = new DagViewerProjectionAdapter();
        var subscription = new ProjectionChangedSubscription();
        var buildCount = 0;

        EventHandler handler = (_, _) =>
        {
            adapter.BuildSnapshot();
            buildCount++;
        };

        subscription.Attach(adapter, handler);
        subscription.Detach();
        subscription.Attach(adapter, handler);

        var node = new DagNode { NodeId = Guid.NewGuid(), Location = new Point(10, 20) };
        adapter.OnNodeAdded(node);
        adapter.Flush();

        Assert.Equal(1, buildCount);
        Assert.Equal(1, adapter.SnapshotBuildCount);
    }

    [Fact]
    public void Attach_SameAdapterAndHandler_Twice_RemainsSingleSubscription()
    {
        var adapter = new DagViewerProjectionAdapter();
        var subscription = new ProjectionChangedSubscription();
        var callbackCount = 0;

        EventHandler handler = (_, _) => callbackCount++;

        subscription.Attach(adapter, handler);
        subscription.Attach(adapter, handler);

        adapter.OnNodeAdded(new DagNode { NodeId = Guid.NewGuid(), Location = new Point(1, 1) });
        adapter.Flush();

        Assert.Equal(1, callbackCount);
    }
}
