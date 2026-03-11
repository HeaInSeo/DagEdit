using System;
using Avalonia;
using DagEdit;
using VirtualCanvas.Core.Geometry;
using Xunit;

namespace DagEdit.Tests
{
    /// <summary>
    /// Phase 1 Viewer spike — NodeViewItem projection 검증.
    ///
    /// 검증 목표:
    /// 1. DagNode.Location이 VCRect Bounds로 정확히 매핑되는가
    /// 2. Constants.NodeWidth/NodeHeight가 Bounds 크기로 반영되는가
    /// 3. NodeId identity가 보존되는가
    /// 4. null guard(NodeId/Location)가 null을 반환하는가
    /// 5. editor 상태(NodeInstance 등)가 projection에 포함되지 않는가
    /// </summary>
    public class NodeViewItemTests
    {
        [Fact]
        public void From_WithValidNode_BoundsTopLeftMatchesLocation()
        {
            var node = new DagNode
            {
                NodeId = Guid.NewGuid(),
                Location = new Point(100, 200),
            };

            var item = NodeViewItem.From(node);

            Assert.NotNull(item);
            Assert.Equal(100, item.Bounds.X);
            Assert.Equal(200, item.Bounds.Y);
        }

        [Fact]
        public void From_WithValidNode_BoundsSizeMatchesConstants()
        {
            var node = new DagNode
            {
                NodeId = Guid.NewGuid(),
                Location = new Point(0, 0),
            };

            var item = NodeViewItem.From(node);

            Assert.NotNull(item);
            Assert.Equal(Constants.NodeWidth, item.Bounds.Width);
            Assert.Equal(Constants.NodeHeight, item.Bounds.Height);
        }

        [Fact]
        public void From_WithValidNode_NodeIdPreserved()
        {
            var id = Guid.NewGuid();
            var node = new DagNode
            {
                NodeId = id,
                Location = new Point(50, 50),
            };

            var item = NodeViewItem.From(node);

            Assert.NotNull(item);
            Assert.Equal(id, item.NodeId);
        }

        [Fact]
        public void From_WithNullNodeId_ReturnsNull()
        {
            var node = new DagNode
            {
                NodeId = null,
                Location = new Point(0, 0),
            };

            var item = NodeViewItem.From(node);

            Assert.Null(item);
        }

        [Fact]
        public void From_WithNullLocation_ReturnsNull()
        {
            var node = new DagNode
            {
                NodeId = Guid.NewGuid(),
                Location = null,
            };

            var item = NodeViewItem.From(node);

            Assert.Null(item);
        }

        [Fact]
        public void From_WithValidNode_IsVisibleAndDefaultPriority()
        {
            var node = new DagNode
            {
                NodeId = Guid.NewGuid(),
                Location = new Point(10, 20),
            };

            var item = NodeViewItem.From(node);

            Assert.NotNull(item);
            Assert.True(item.IsVisible);
            Assert.Equal(0.0, item.Priority);
            Assert.Equal(0, item.ZIndex);
        }

        [Fact]
        public void UpdateLocation_ChangesBoundsInPlace()
        {
            var node = new DagNode { NodeId = Guid.NewGuid(), Location = new Point(0, 0) };
            var item = NodeViewItem.From(node)!;

            item.UpdateLocation(new Point(150, 250));

            Assert.Equal(150, item.Bounds.X);
            Assert.Equal(250, item.Bounds.Y);
            Assert.Equal(Constants.NodeWidth, item.Bounds.Width);
            Assert.Equal(Constants.NodeHeight, item.Bounds.Height);
        }
    }
}
