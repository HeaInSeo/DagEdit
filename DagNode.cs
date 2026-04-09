using System;
using Avalonia;
using Avalonia.Collections;

namespace DagEdit
{
    public class DagNode
    {
        public Guid? NodeId { get; set; }

        public Node? NodeInstance { get; set; }

        public Point? Location { get; set; }

        // node 의 anchor 를 나타냄.
        public Point? SourceAnchor { get; set; }

        public Point? TargetAnchor { get; set; }

        // TODO 이름은 추후 생각하자. Source, Target 으로 고치다. 현재는 start, end 로 되어 있음.
        // 이 녀석을 통해서 connection 을 검색할 수 있어야 한다.
        public AvaloniaList<DagConnection> SourceConnections { get; } = new();

        public AvaloniaList<DagConnection> TargetConnections { get; } = new();

        public DagItemsType DAGItemType { get; set; }
    }
}
