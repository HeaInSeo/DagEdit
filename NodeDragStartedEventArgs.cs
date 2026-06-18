using System;
using Avalonia.Interactivity;

namespace DagEdit
{
    /// <summary>
    /// 노드 드래그 시작 시 발행되는 이벤트 인수.
    /// DagEditor가 수신하여 VCA Pin을 요청한다.
    /// </summary>
    internal class NodeDragStartedEventArgs : RoutedEventArgs
    {
        public NodeDragStartedEventArgs(RoutedEvent routedEvent, Guid nodeId)
            : base(routedEvent)
        {
            NodeId = nodeId;
        }

        public Guid NodeId { get; }
    }
}
