using System;
using Avalonia;

namespace DagEdit
{
    // TODO 추후 불필요한 것들 삭제
    public class DagConnection
    {
        public Guid? ConnectionId { get; set; }

        public Connection? ConnectionInstance { get; set; }

        public Guid? SourceNodeId { get; set; }

        public Node? SourceNodeInstance { get; set; }

        public Guid? TargetNodeId { get; set; }

        public Node? TargetNodeInstance { get; set; }

        public DagItemsType DAGItemType { get; set; }

        // connection 의 source, target 을 나타냄.
        public Point? SourceAnchor { get; set; }

        public Point? TargetAnchor { get; set; }
    }
}
