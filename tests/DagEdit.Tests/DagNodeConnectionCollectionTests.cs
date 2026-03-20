namespace DagEdit.Tests;

using System.Reflection;
using Xunit;

public class DagNodeConnectionCollectionTests
{
    [Fact]
    public void SourceConnections_DoesNotExposePublicSetter()
    {
        PropertyInfo? property = typeof(DagNode).GetProperty(nameof(DagNode.SourceConnections), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        Assert.False(property!.CanWrite);
        Assert.Null(property.SetMethod);
    }

    [Fact]
    public void TargetConnections_DoesNotExposePublicSetter()
    {
        PropertyInfo? property = typeof(DagNode).GetProperty(nameof(DagNode.TargetConnections), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        Assert.False(property!.CanWrite);
        Assert.Null(property.SetMethod);
    }

    [Fact]
    public void ConnectionCollections_StillAllowAddAndRemove()
    {
        var node = new DagNode();
        var sourceConnection = new DagConnection();
        var targetConnection = new DagConnection();

        node.SourceConnections.Add(sourceConnection);
        node.TargetConnections.Add(targetConnection);

        Assert.Single(node.SourceConnections);
        Assert.Single(node.TargetConnections);
        Assert.Same(sourceConnection, node.SourceConnections.Single());
        Assert.Same(targetConnection, node.TargetConnections.Single());

        node.SourceConnections.Remove(sourceConnection);
        node.TargetConnections.Clear();

        Assert.Empty(node.SourceConnections);
        Assert.Empty(node.TargetConnections);
    }
}
