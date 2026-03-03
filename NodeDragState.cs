using Avalonia;
using ReactiveUI;

namespace DagEdit
{
    /// <summary>
    /// 노드 드래그 상태를 보관하는 ReactiveObject.
    ///
    /// 이 클래스는 ReactiveUI의 WhenAnyValue 패턴을 위해 존재한다.
    /// Node(ContentControl)는 AvaloniaObject를 상속하므로 직접 IReactiveObject를
    /// 구현할 수 없다. 별도의 ReactiveObject로 드래그 상태를 분리함으로써
    /// WhenAnyValue를 통한 반응형 구독이 가능해진다.
    ///
    /// [Go Analogy]
    /// Go 채널과 비교하면:
    ///   NodeDragState.Position = 채널(chan Point)
    ///   HandlePointerMoved     = 채널에 값을 보내는 고루틴 (ch &lt;- newPos)
    ///   WhenAnyValue 구독      = 채널에서 값을 읽는 고루틴 (for pos := range ch)
    /// </summary>
    internal sealed class NodeDragState : ReactiveObject
    {
        private Point _position;

        /// <summary>
        /// 드래그 중인 노드의 현재 절대 좌표.
        /// 값이 변경될 때마다 WhenAnyValue 구독자에게 새 값이 전파된다.
        /// </summary>
        public Point Position
        {
            get => _position;
            set => this.RaiseAndSetIfChanged(ref _position, value);
        }
    }
}
