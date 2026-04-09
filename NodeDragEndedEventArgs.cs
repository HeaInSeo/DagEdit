using System;
using Avalonia.Interactivity;

namespace DagEdit
{
    /// <summary>
    /// 노드 드래그 종료 시 발행되는 이벤트 인수 (위치 변화 없는 경우 포함).
    /// DagEditor가 수신하여 drag pin을 해제한다.
    /// NodeMovedEvent와 달리 항상 발행된다. drag pin leak 방지.
    /// </summary>
    public class NodeDragEndedEventArgs : RoutedEventArgs
    {
        public NodeDragEndedEventArgs(RoutedEvent routedEvent, Guid nodeId)
            : base(routedEvent)
        {
            NodeId = nodeId;
        }

        public Guid NodeId { get; }
    }
}
