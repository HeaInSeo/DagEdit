using System;
using Avalonia;
using Avalonia.Interactivity;

namespace DagEdit
{
    /// <summary>
    /// 노드 드래그 시작 시 발행되는 이벤트 인수.
    /// DagEditor가 수신하여 VCA Pin을 요청한다.
    /// </summary>
    public class NodeDragStartedEventArgs : RoutedEventArgs
    {
        public Guid NodeId { get; }

        public NodeDragStartedEventArgs(RoutedEvent routedEvent, Guid nodeId)
            : base(routedEvent)
        {
            NodeId = nodeId;
        }
    }

    /// <summary>
    /// 노드 드래그 종료 시 발행되는 이벤트 인수 (위치 변화 없는 경우 포함).
    /// DagEditor가 수신하여 drag pin을 해제한다.
    /// NodeMovedEvent와 달리 항상 발행된다 — drag pin leak 방지.
    /// </summary>
    public class NodeDragEndedEventArgs : RoutedEventArgs
    {
        public Guid NodeId { get; }

        public NodeDragEndedEventArgs(RoutedEvent routedEvent, Guid nodeId)
            : base(routedEvent)
        {
            NodeId = nodeId;
        }
    }

    /// <summary>
    /// 노드 드래그 완료 시 발행되는 이벤트 인수.
    /// DagEditor가 수신하여 MoveNodeCommand를 undo 스택에 push한다.
    /// </summary>
    public class NodeMovedEventArgs : RoutedEventArgs
    {
        public Guid NodeId { get; }

        public Point OldLocation { get; }

        public Point NewLocation { get; }

        public NodeMovedEventArgs(RoutedEvent routedEvent, Guid nodeId, Point oldLocation, Point newLocation)
            : base(routedEvent)
        {
            NodeId = nodeId;
            OldLocation = oldLocation;
            NewLocation = newLocation;
        }
    }
}
