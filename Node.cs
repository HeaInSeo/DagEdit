using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;

namespace DagEdit
{
    /// <summary>
    /// 드래그 가능한 DAG 노드 컨트롤.
    ///
    /// ─── ReactiveUI WhenAnyValue 패턴 (노드 드래그 좌표 업데이트) ───────────────
    ///
    /// [What it does]
    /// 노드를 드래그할 때 마우스 이동 이벤트(PointerMoved)는 매우 빈번하게 발생한다.
    /// 과거 코드는 HandlePointerMoved 안에서 Transform 갱신·앵커 재계산·이벤트 발행을
    /// 순차적으로 수행했다. 관심사가 뒤섞여 테스트와 유지보수가 어려웠다.
    ///
    /// 리팩토링 후에는 HandlePointerMoved가 '입력 처리'(그리드 스냅 계산 + 위치 확정)만
    /// 담당하고, 그 결과(_dragState.Position)를 구독하는 Rx 체인이 '부수 효과'
    /// (TranslateTransform 갱신, Anchor 재계산, ConnectionChangedEvent 발행)를
    /// 반응형으로 처리한다.
    ///
    /// [Go Analogy]
    /// Go 채널 관점에서 이 패턴을 이해하면:
    ///
    ///   // 생산자 (HandlePointerMoved)
    ///   posCh := make(chan Point, 1)
    ///   go func() {
    ///       posCh &lt;- newPosition   // 그리드 스냅 후 새 위치만 전달
    ///   }()
    ///
    ///   // 소비자 (WhenAnyValue 구독)
    ///   go func() {
    ///       for pos := range posCh {
    ///           updateTransform(pos)    // 렌더링 갱신
    ///           recalcAnchors(pos)      // 커넥션 앵커 재계산
    ///           raiseEvent(pos)         // 이벤트 발행
    ///       }
    ///   }()
    ///
    ///   DistinctUntilChanged ≈ "채널에 이전과 동일한 값은 보내지 않는다"
    ///
    /// [Operator Breakdown]
    ///
    ///   WhenAnyValue(x => x.Position)
    ///     → _dragState.Position 프로퍼티의 변경 스트림을 IObservable&lt;Point&gt;로 변환.
    ///       ReactiveObject가 RaiseAndSetIfChanged를 호출할 때마다 새 값을 방출.
    ///       구독 시 현재 값(기본값 Point())을 즉시 한 번 방출한다.
    ///
    ///   Skip(1)
    ///     → 구독 직후 방출되는 초기값(Point())을 무시.
    ///       드래그가 시작되기 전 초기화 상태의 빈 좌표가 처리되는 것을 방지.
    ///
    ///   DistinctUntilChanged()
    ///     → 이전과 동일한 위치가 연속으로 들어올 경우 무시.
    ///       그리드 스냅으로 인해 Position이 실제로 바뀌지 않으면 구독자를 호출하지 않음.
    ///       Go 채널의 "값이 바뀔 때만 보낸다" 관용구와 동일한 의미.
    ///
    ///   Subscribe(UpdateFromDragPosition)
    ///     → 새 위치가 확정될 때마다 TranslateTransform 갱신, 앵커 재계산,
    ///       ConnectionChangedEvent 발행을 순서대로 수행.
    ///
    ///   DisposeWith(_disposables)
    ///     → Node가 소멸할 때 구독을 자동 해제. 메모리 누수 방지.
    ///        Go의 defer cancel() 패턴과 동일한 역할.
    /// </summary>
    public class Node : BaseNode
    {
        #region Dependency Properties

        public static readonly StyledProperty<Control?> ParentControlProperty =
            AvaloniaProperty.Register<Node, Control?>(nameof(ParentControl));

        public Control? ParentControl
        {
            get => GetValue(ParentControlProperty);
            set => SetValue(ParentControlProperty, value);
        }

        public static readonly DirectProperty<Node, Guid> IdProperty =
            AvaloniaProperty.RegisterDirect<Node, Guid>(
                nameof(Id),
                o => o.Id,
                (o, v) => o.Id = v);

        // TODO 중요. 아래 내용 잊지말자. 기존 Node(GUID) 와 StartNode(int type), EndNode(int type) 는 다른 ID 쳬계를 가져갈려고 한다.
        // Id 추가 BaseNode 에 않넣는 이유는 StartNode, EndNode 는 다른 ID 체계로 사용할려고 한다.
        private Guid _id;

        public Guid Id
        {
            get => _id;
            set => SetAndRaise(IdProperty, ref _id, value);
        }

        #endregion

        #region Routed Events

        public static readonly RoutedEvent<ConnectionChangedEventArgs> ConnectionChangedEvent =
            RoutedEvent.Register<Node, ConnectionChangedEventArgs>(
                nameof(ConnectionChanged),
                RoutingStrategies.Bubble);

        public event EventHandler<ConnectionChangedEventArgs> ConnectionChanged
        {
            add => AddHandler(ConnectionChangedEvent, value);
            remove => RemoveHandler(ConnectionChangedEvent, value);
        }

        /// <summary>
        /// 노드 드래그 완료 시 발행. DagEditor가 수신하여 MoveNodeCommand를 undo 스택에 push한다.
        /// </summary>
        public static readonly RoutedEvent<NodeMovedEventArgs> NodeMovedEvent =
            RoutedEvent.Register<Node, NodeMovedEventArgs>(
                nameof(NodeMoved),
                RoutingStrategies.Bubble);

        public event EventHandler<NodeMovedEventArgs> NodeMoved
        {
            add => AddHandler(NodeMovedEvent, value);
            remove => RemoveHandler(NodeMovedEvent, value);
        }

        /// <summary>
        /// 노드 드래그 시작 시 발행. DagEditor가 수신하여 VCA Pin을 요청한다.
        /// </summary>
        public static readonly RoutedEvent<NodeDragStartedEventArgs> NodeDragStartedEvent =
            RoutedEvent.Register<Node, NodeDragStartedEventArgs>(
                nameof(NodeDragStarted),
                RoutingStrategies.Bubble);

        public event EventHandler<NodeDragStartedEventArgs> NodeDragStarted
        {
            add => AddHandler(NodeDragStartedEvent, value);
            remove => RemoveHandler(NodeDragStartedEvent, value);
        }

        /// <summary>
        /// 노드 드래그 종료 시 항상 발행 (위치 변화 없는 경우 포함).
        /// DagEditor가 수신하여 drag pin을 해제한다 — pin leak 방지.
        /// </summary>
        public static readonly RoutedEvent<NodeDragEndedEventArgs> NodeDragEndedEvent =
            RoutedEvent.Register<Node, NodeDragEndedEventArgs>(
                nameof(NodeDragEnded),
                RoutingStrategies.Bubble);

        public event EventHandler<NodeDragEndedEventArgs> NodeDragEnded
        {
            add => AddHandler(NodeDragEndedEvent, value);
            remove => RemoveHandler(NodeDragEndedEvent, value);
        }

        #endregion

        #region fields

        // ── 드래그 상태 ──────────────────────────────────────────────────────
        private readonly NodeDragState _dragState = new();
        private readonly CompositeDisposable _disposables = new();

        private Point _initialPointerPosition;  // 드래그 중 포인터 위치 (이전 프레임)
        private Vector _dragAccumulator;         // 그리드 스냅을 위한 누적 이동량
        private Point _dragStartLocation;        // 드래그 시작 시 노드 위치 (Undo용)

        private const int GridCellSize = 15;

        #endregion

        #region Constructor

        public Node()
        {
            Focusable = true;

            ParentControlProperty.Changed
                .Subscribe(HandleParentControlChanged)
                .DisposeWith(_disposables);

            _dragState
                .WhenAnyValue(x => x.Position)
                .Skip(1)
                .DistinctUntilChanged()
                .Subscribe(UpdateFromDragPosition)
                .DisposeWith(_disposables);
        }

        public Node(Point location) : this()
        {
            Location = location;
            (SourceAnchor, TargetAnchor) = FindAnchors(location);
        }

        #endregion

        #region Event Handlers

        protected override void HandlePointerPressed(object? sender, PointerPressedEventArgs args)
        {
            if (ParentControl == null)
                throw new InvalidOperationException(
                    "Node cannot move because a Canvas parent is not found.");

            if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                args.Pointer.Capture(this);
                Debug.Print("Dragging Start");
                _initialPointerPosition = args.GetPosition(ParentControl);
                _dragAccumulator = new();
                _dragStartLocation = Location; // Undo용 시작 위치 기록
                IsDragging = true;
                RaiseEvent(new NodeDragStartedEventArgs(NodeDragStartedEvent, _id));
                args.Handled = true;
            }
        }

        protected override void HandlePointerMoved(object? sender, PointerEventArgs args)
        {
            if (ParentControl == null)
                throw new InvalidOperationException(
                    "Node cannot move because a Canvas parent is not found.");

            if (!IsDragging || !Equals(args.Pointer.Captured))
            {
                return;
            }

            Debug.Print("Dragging...");

            // ── 1. 그리드 스냅 계산 ──────────────────────────────────────────
            var currentPointerPosition = args.GetPosition(ParentControl);
            var delta = currentPointerPosition - _initialPointerPosition;
            _dragAccumulator += delta;

            var effectiveDelta = new Vector(
                Math.Floor(_dragAccumulator.X / GridCellSize) * GridCellSize,
                Math.Floor(_dragAccumulator.Y / GridCellSize) * GridCellSize);

            if (effectiveDelta != Vector.Zero)
            {
                _dragAccumulator -= effectiveDelta;

                // ── 2. Location 직접 업데이트 ──────────────────────────────────
                var newPosition = new Point(Location.X + effectiveDelta.X, Location.Y + effectiveDelta.Y);
                Location = newPosition;

                // Position 설정 → _dragState.WhenAnyValue 체인이 반응한다.
                _dragState.Position = newPosition;
            }

            _initialPointerPosition = currentPointerPosition;
            args.Handled = true;
        }

        protected override void HandlePointerReleased(object? sender, PointerReleasedEventArgs args)
        {
            if (ParentControl == null)
                throw new InvalidOperationException(
                    "Node cannot move because a Canvas parent is not found.");

            if (sender != null && Equals(args.Pointer.Captured) && IsDragging)
            {
                Debug.Print("Finish");
                args.Pointer.Capture(null);
                IsDragging = false;

                // 위치가 실제로 바뀐 경우에만 NodeMovedEvent를 발행한다.
                if (Location != _dragStartLocation)
                {
                    RaiseNodeMovedEvent(_id, _dragStartLocation, Location);
                }

                // drag pin leak 방지 — 항상 발행 (NodeMovedEvent 발행 여부 무관)
                RaiseEvent(new NodeDragEndedEventArgs(NodeDragEndedEvent, _id));

                args.Handled = true;
            }
        }

        private void HandleParentControlChanged(AvaloniaPropertyChangedEventArgs e)
        {
            // Canvas 타입으로 조회 — concrete DagEditorCanvas 를 몰라도 됨.
            // VCA 통합 시 VCA host 가 Canvas 를 상속하면 이 경로도 그대로 작동한다.
            if (e.NewValue is Canvas canvas)
            {
                ParentControl = canvas;
            }
            else
            {
                ParentControl = this.GetParentVisualOfType<Canvas>();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// WhenAnyValue 구독자: 새 드래그 위치가 확정될 때 호출된다.
        /// 앵커 재계산과 ConnectionChangedEvent 발행을 담당한다.
        /// </summary>
        internal void UpdateFromDragPosition(Point newPosition)
        {
            Point? oldSourceAnchor = SourceAnchor;
            Point? oldTargetAnchor = TargetAnchor;
            (SourceAnchor, TargetAnchor) = FindAnchors(newPosition);

            RaiseConnectionChangedEvent(
                _id, Location,
                SourceAnchor, oldSourceAnchor,
                TargetAnchor, oldTargetAnchor,
                DagItemsType.RunnerNode);
        }

        /// <summary>
        /// 프로그래매틱하게 노드를 이동한다. Undo/Redo의 MoveNodeCommand에서 사용된다.
        /// Location 갱신 + 앵커 재계산 + ConnectionChangedEvent 발행을 수행한다.
        /// </summary>
        public void MoveTo(Point newLocation)
        {
            Location = newLocation;
            UpdateFromDragPosition(newLocation);
        }

        private void RaiseConnectionChangedEvent(Guid? nodeId, Point? location, Point? sourceAnchor,
            Point? oldSourceAnchor, Point? targetAnchor, Point? oldTargetAnchor, DagItemsType dagItemsType)
        {
            var args = new ConnectionChangedEventArgs(ConnectionChangedEvent, nodeId, location, sourceAnchor,
                oldSourceAnchor, targetAnchor, oldTargetAnchor, dagItemsType);
            RaiseEvent(args);
        }

        private void RaiseNodeMovedEvent(Guid nodeId, Point oldLocation, Point newLocation)
        {
            var args = new NodeMovedEventArgs(NodeMovedEvent, nodeId, oldLocation, newLocation);
            RaiseEvent(args);
        }

        private void NodeMove(Point point)
        {
            Location = point;
        }

        #endregion

        /// <inheritdoc />
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            ParentControl = this.GetParentVisualOfType<Canvas>();
        }

        public bool CanNodeMove()
        {
            var parentControl = this.GetParentVisualOfType<Canvas>();
            if (parentControl != null)
            {
                ParentControl = parentControl;
                return true;
            }
            else
            {
                ParentControl = null;
                return false;
            }
        }

        public void SetLocation(Point location)
        {
            Location = location;
        }

        /// <inheritdoc />
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposables.Dispose();
            }

            base.Dispose(disposing);
        }

        private (Point sourceAnchor, Point targetAnchor) FindAnchors(Point location)
        {
            var sourceAnchor = new Point(location.X + Constants.NodeWidth, location.Y + (Constants.NodeHeight / 2));
            var targetAnchor = new Point(location.X, location.Y + (Constants.NodeHeight / 2));
            return (sourceAnchor, targetAnchor);
        }
    }
}
