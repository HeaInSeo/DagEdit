using System;
using Avalonia;
using Avalonia.Interactivity;

namespace DagEdit
{
    /// <summary>
    /// 노드 드래그 완료 시 발행되는 이벤트 인수.
    /// DagEditor가 수신하여 MoveNodeCommand를 undo 스택에 push한다.
    /// </summary>
    public class NodeMovedEventArgs : RoutedEventArgs
    {
        public NodeMovedEventArgs(RoutedEvent routedEvent, Guid nodeId, Point oldLocation, Point newLocation)
            : base(routedEvent)
        {
            NodeId = nodeId;
            OldLocation = oldLocation;
            NewLocation = newLocation;
        }

        public Guid NodeId { get; }

        public Point OldLocation { get; }

        public Point NewLocation { get; }
    }
}
