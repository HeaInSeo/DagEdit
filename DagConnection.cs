using System;
using Avalonia;

namespace DagEdit
{
    // TODO 추후 불필요한 것들 삭제
    internal class DagConnection
    {
        public Guid? ConnectionId { get; set; }

        public Guid? SourceNodeId { get; set; }

        public Guid? TargetNodeId { get; set; }

        // connection 의 source, target 을 나타냄.
        public Point? SourceAnchor { get; set; }

        public Point? TargetAnchor { get; set; }

        internal Connection? ConnectionInstance { get; set; }

        internal Node? SourceNodeInstance { get; set; }

        internal Node? TargetNodeInstance { get; set; }

        internal DagItemsType DAGItemType { get; set; }
    }
}
