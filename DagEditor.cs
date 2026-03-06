using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;

namespace DagEdit
{
    public class DagEditor : SelectingItemsControl, IDisposable
    {
        #region Dependency Properties

        public static readonly StyledProperty<Point> ViewportLocationProperty =
            AvaloniaProperty.Register<DagEditor, Point>(
                nameof(ViewportLocation), Constants.ZeroPoint);

        public Point ViewportLocation
        {
            get => GetValue(ViewportLocationProperty);
            set => SetValue(ViewportLocationProperty, value);
        }

        public static readonly StyledProperty<bool> DisablePanningProperty =
            AvaloniaProperty.Register<DagEditor, bool>(nameof(DisablePanning));

        public bool DisablePanning
        {
            get => GetValue(DisablePanningProperty);
            set => SetValue(DisablePanningProperty, value);
        }

        public static readonly DirectProperty<DagEditor, bool> IsSelectingProperty =
            AvaloniaProperty.RegisterDirect<DagEditor, bool>(
                nameof(IsSelecting),
                o => o.IsSelecting);

        private bool _isSelecting;

        public bool IsSelecting
        {
            get => _isSelecting;
            internal set => SetAndRaise(IsSelectingProperty, ref _isSelecting, value);
        }

        public static readonly StyledProperty<bool> EnableRealtimeSelectionProperty =
            AvaloniaProperty.Register<DagEditor, bool>(
                nameof(EnableRealtimeSelection));

        public bool EnableRealtimeSelection
        {
            get => GetValue(EnableRealtimeSelectionProperty);
            set => SetValue(EnableRealtimeSelectionProperty, value);
        }

        public static readonly DirectProperty<DagEditor, Rect> SelectedAreaProperty =
            AvaloniaProperty.RegisterDirect<DagEditor, Rect>(
                nameof(SelectedArea),
                o => o.SelectedArea);

        private Rect _selectedArea;

        public Rect SelectedArea
        {
            get => _selectedArea;
            internal set => SetAndRaise(SelectedAreaProperty, ref _selectedArea, value);
        }

        public static readonly DirectProperty<DagEditor, bool?> IsPreviewingSelectionProperty =
            AvaloniaProperty.RegisterDirect<DagEditor, bool?>(
                nameof(IsPreviewingSelection),
                o => o.IsPreviewingSelection);

        private bool? _isPreviewingSelection;

        public bool? IsPreviewingSelection
        {
            get => _isPreviewingSelection;
            internal set => SetAndRaise(IsPreviewingSelectionProperty, ref _isPreviewingSelection, value);
        }

        public static readonly DirectProperty<DagEditor, bool> IsPanningProperty =
            AvaloniaProperty.RegisterDirect<DagEditor, bool>(
                nameof(IsPanning),
                o => o.IsPanning);

        private bool _isPanning;

        public bool IsPanning
        {
            get => _isPanning;
            protected internal set => SetAndRaise(IsPanningProperty, ref _isPanning, value);
        }

        public static readonly StyledProperty<DataTemplate?> PendingConnectionTemplateProperty =
            AvaloniaProperty.Register<DagEditor, DataTemplate?>(
                nameof(PendingConnectionTemplate));

        public DataTemplate? PendingConnectionTemplate
        {
            get => GetValue(PendingConnectionTemplateProperty);
            set => SetValue(PendingConnectionTemplateProperty, value);
        }

        public static readonly StyledProperty<object?> PendingConnectionProperty =
            AvaloniaProperty.Register<DagEditor, object?>(
                nameof(PendingConnection));

        public object? PendingConnection
        {
            get => GetValue(PendingConnectionProperty);
            set => SetValue(PendingConnectionProperty, value);
        }

        public static readonly StyledProperty<Point> SourceAnchorProperty =
            AvaloniaProperty.Register<DagEditor, Point>(nameof(SourceAnchor));

        public Point SourceAnchor
        {
            get => GetValue(SourceAnchorProperty);
            set => SetValue(SourceAnchorProperty, value);
        }

        public static readonly StyledProperty<Point> TargetAnchorProperty =
            AvaloniaProperty.Register<DagEditor, Point>(nameof(TargetAnchor));

        public Point TargetAnchor
        {
            get => GetValue(TargetAnchorProperty);
            set => SetValue(TargetAnchorProperty, value);
        }

        // PendingConnection visible 설정에 사용
        public static readonly StyledProperty<bool> IsVisiblePendingConnectionProperty =
            AvaloniaProperty.Register<DagEditor, bool>(
                nameof(IsVisiblePendingConnection));

        public bool IsVisiblePendingConnection
        {
            get => GetValue(IsVisiblePendingConnectionProperty);
            set => SetValue(IsVisiblePendingConnectionProperty, value);
        }

        // TODO 필요 없을 듯 향후 코드 정리 시 지운다.
        public static readonly StyledProperty<Point?> ContextMenuPointProperty =
            AvaloniaProperty.Register<DagEditor, Point?>(nameof(ContextMenuPoint));

        public Point? ContextMenuPoint
        {
            get => GetValue(ContextMenuPointProperty);
            set => SetValue(ContextMenuPointProperty, value);
        }

        public static readonly StyledProperty<double> ViewportScaleProperty =
            AvaloniaProperty.Register<DagEditor, double>(nameof(ViewportScale), 1.0);

        public double ViewportScale
        {
            get => GetValue(ViewportScaleProperty);
            set => SetValue(ViewportScaleProperty, value);
        }

        #endregion

        #region Fields

        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        // 이건 connector 에서 올라오는 event
        private EventHandler<PendingConnectionEventArgs>? _connectionStartedHandler;
        private EventHandler<PendingConnectionEventArgs>? _connectionDragHandler;
        private EventHandler<PendingConnectionEventArgs>? _connectionCompleteHandler;
        // 이건 node 에서 올라오는 event
        private EventHandler<ConnectionChangedEventArgs>? _connectionChangedHandler;

        private bool _IsRightBtnClicked;
        private readonly DagEditorViewModel _viewModel = new();

        // TODO 아래 변수들 코드 정리시 지운다.
        private bool _isLoaded = true;
        private Canvas? topLayer;
        private DagEditorCanvas? editorCanvas;

        // Panning 관련 포인터 위치 값 
        private Point _previousPointerPosition;
        private Point _currentPointerPosition;

        // TODO 일단 이렇게 남겨 두는데, Menu 디자인시 수정 해야 함.
        private EditorContextFlyout _contextMenu;

        #endregion

        #region Constructors

        public DagEditor()
        {
            DataContext = _viewModel;
            InitializeSubscriptions();
            _contextMenu = new EditorContextFlyout(this);
            this.Unloaded += (_, _) => this.Dispose();
        }

        #endregion

        #region Event Handlers

        private void InitializeSubscriptions()
        {
            _connectionStartedHandler = HandleConnectionStarted;
            _connectionDragHandler = HandleConnectionDrag;
            _connectionCompleteHandler = HandleConnectionComplete;
            _connectionChangedHandler = HandleConnectionChanged;
           
            Observable.FromEventPattern<PointerPressedEventArgs>(
                    h => this.PointerPressed += h,
                    h => this.PointerPressed -= h)
                .Subscribe(args => HandlePointerPressed(args.Sender, args.EventArgs))
                .DisposeWith(_disposables);

            Observable.FromEventPattern<PointerEventArgs>(
                    h => this.PointerMoved += h,
                    h => this.PointerMoved -= h)
                .Subscribe(args => HandlePointerMoved(args.Sender, args.EventArgs))
                .DisposeWith(_disposables);

            Observable.FromEventPattern<PointerReleasedEventArgs>(
                    h => this.PointerReleased += h,
                    h => this.PointerReleased -= h)
                .Subscribe(args => HandlePointerReleased(args.Sender, args.EventArgs))
                .DisposeWith(_disposables);

            Observable.FromEventPattern<RoutedEventArgs>(
                    h => this.Loaded += h,
                    h => this.Loaded -= h)
                .Subscribe(args => HandleLoaded(args.Sender, args.EventArgs))
                .DisposeWith(_disposables);

            Observable.FromEventPattern<KeyEventArgs>(
                    h => this.KeyDown += h,
                    h => this.KeyDown -= h)
                .Subscribe(args => HandleKeyDown(args.Sender, args.EventArgs))
                .DisposeWith(_disposables);

            Observable.FromEventPattern<PointerWheelEventArgs>(
                    h => this.PointerWheelChanged += h,
                    h => this.PointerWheelChanged -= h)
                .Subscribe(args => HandlePointerWheelChanged(args.Sender, args.EventArgs))
                .DisposeWith(_disposables);

            // 이벤트 핸들러 등록
            // PendingConnection
            AddHandler(Connector.PendingConnectionStartedEvent, _connectionStartedHandler);
            AddHandler(Connector.PendingConnectionDragEvent, _connectionDragHandler);
            AddHandler(Connector.PendingConnectionCompletedEvent, _connectionCompleteHandler);
            // Connection Changed
            AddHandler(Node.ConnectionChangedEvent, _connectionChangedHandler);
           
            // 이벤트 핸들러 해제
            _disposables.Add(Disposable.Create(() =>
            {
                // PendingConnection
                RemoveHandler(Connector.PendingConnectionStartedEvent, _connectionStartedHandler);
                RemoveHandler(Connector.PendingConnectionDragEvent, _connectionDragHandler);
                RemoveHandler(Connector.PendingConnectionCompletedEvent, _connectionCompleteHandler);
                // Connection Changed
                RemoveHandler(Node.ConnectionChangedEvent, _connectionChangedHandler);
            }));
        }

        private void HandlePointerPressed(object? sender, PointerPressedEventArgs args)
        {
            if (args.GetCurrentPoint(this).Properties.IsRightButtonPressed && !DisablePanning)
            {
                args.Pointer.Capture(this);
                // 클릭 위치를 캔버스 월드 좌표로 변환하여 저장한다.
                // 패닝/줌 상태에서 노드 추가 위치가 올바르게 계산된다. (DEC-015 참조)
                var rawPos = args.GetPosition(this);
                ContextMenuPoint = ViewportTransform.ScreenToWorld(rawPos, ViewportLocation, ViewportScale);
                _previousPointerPosition = args.GetPosition(this);
                _IsRightBtnClicked = true;
                args.Handled = true;
            }
        }

        private void HandlePointerMoved(object? sender, PointerEventArgs args)
        {
            if (_IsRightBtnClicked)
            {
                _currentPointerPosition = args.GetPosition(this);
                // 패닝 델타는 줌 배율과 무관하게 스크린 픽셀 단위로 ViewportLocation에 적용한다.
                // WorldUnderCursor = (sx + VL) / s 에서 s가 약분되어 소거됨. (DEC-015 참조)
                ViewportLocation -= (_currentPointerPosition - _previousPointerPosition);
                _previousPointerPosition = _currentPointerPosition;
                IsPanning = true;
                args.Handled = true;
            }
        }

        private void HandlePointerReleased(object? sender, PointerReleasedEventArgs args)
        {
            if (_IsRightBtnClicked)
            {
                args.Handled = true;
                if (IsPanning)
                {
                    IsPanning = false;
                    if (this.Equals(args.Pointer.Captured))
                    {
                        args.Pointer.Capture(null);
                    }
                    _IsRightBtnClicked = false;
                    return;
                }

                _contextMenu.ShowAt(this, true);
                _IsRightBtnClicked = false;
            }
        }

        private void HandleConnectionStarted(object? sender, PendingConnectionEventArgs args)
        {
            if (args.Source is SourceConnector)
            {
                IsVisiblePendingConnection = true;

                if (args.SourceAnchor.HasValue)
                {
                    // SourceAnchor = 월드 좌표 (Node.FindAnchors에서 계산된 값)
                    SourceAnchor = args.SourceAnchor.Value;
                    // 드래그 시작 시 TargetAnchor는 SourceAnchor와 동일하게 초기화한다.
                    // RaiseConnectionStartEvent 는 Offset을 설정하지 않으므로 항상 이 분기에 진입한다.
                    TargetAnchor = SourceAnchor;
                }
                else
                {
                    // SourceAnchor가 없으면 IsVisiblePendingConnection = false가 "연결 없음" 신호
                    SourceAnchor = default;
                    TargetAnchor = default;
                }

                args.Handled = true;
            }

            Debug.WriteLine("Ok!!!");
        }

        private void HandleConnectionDrag(object? sender, PendingConnectionEventArgs args)
        {
            // args.Offset = SourceConnector.HandlePointerMoved에서 설정된 포인터의 월드 좌표.
            // SourceConnector는 GetPosition(PART_ItemsHost)를 사용하는데, Avalonia의 GetPosition은
            // DagEditorCanvas의 ScaleTransform을 역변환하여 월드 좌표를 반환한다.
            // 따라서 TargetAnchor = Offset(월드)으로 설정하는 것이 올바르다. (DEC-015 참조)
            if (IsVisiblePendingConnection)
            {
                if (args.Offset.HasValue)
                {
                    TargetAnchor = new Point(args.Offset.Value.X, args.Offset.Value.Y);
                }

                args.Handled = true;
            }
        }

        private void HandleConnectionComplete(object? sender, PendingConnectionEventArgs args)
        {
            args.Handled = true;
            if (args.ConnectedConnector == null || args.SourceAnchor == null || args.TargetAnchor == null)
            {
                IsVisiblePendingConnection = false;
                return;
            }
            Debug.WriteLine("Editor connection end");
            Debug.WriteLine(args.SourceAnchor.Value);
            // 선추가하는 구문.
            _viewModel.AddDagConnectionItem(args.SourceAnchor, args.SourceNodeId, args.TargetAnchor, args.TargetNodeId);
            IsVisiblePendingConnection = false;
        }

        private void HandleConnectionChanged(object? sender, ConnectionChangedEventArgs args)
        {
            if (args.NodeId is null)
            {
                args.Handled = true;
                return;
            }

            var dagNode = _viewModel.FindNode(args.NodeId.Value);
            if (dagNode is null)
            {
                args.Handled = true;
                return;
            }

            dagNode.Location = args.Location;
            dagNode.SourceAnchor = args.SourceAnchor;
            dagNode.TargetAnchor = args.TargetAnchor;

            if (args.SourceAnchor.HasValue)
            {
                foreach (var conn in dagNode.SourceConnections)
                {
                    conn.ConnectionInstance?.UpdateStart(args.SourceAnchor.Value);
                    conn.SourceAnchor = args.SourceAnchor.Value;
                }
            }

            if (args.TargetAnchor.HasValue)
            {
                foreach (var conn in dagNode.TargetConnections)
                {
                    conn.ConnectionInstance?.UpdateEnd(args.TargetAnchor.Value);
                    conn.TargetAnchor = args.TargetAnchor.Value;
                }
            }

            args.Handled = true;
        }

        // TODO 코드 정리 할때 이거 필요 없어짐. 삭제, 다만 backup 용으로 기록해 둬야 함.
        private void HandleLoaded(object? sender, RoutedEventArgs args)
        {
            if (_isLoaded)
            {
                editorCanvas = this.GetChildControlByName<DagEditorCanvas>("PART_ItemsHost");
                bool isMatched = Extension.IsCanvasMatched(topLayer, editorCanvas);

                if (!isMatched)
                {
                    Extension.WriteErrorsToFile(
                        "The coordinate systems do not match, causing rendering issues in the application.");
                    throw new InvalidOperationException("The coordinate systems do not match, causing rendering issues in the application.");
                }

                // 한번만 실행되게 만드는 flag
                _isLoaded = false;
            }
        }

        private void HandlePointerWheelChanged(object? sender, PointerWheelEventArgs args)
        {
            // 마우스 휠로 줌 인/아웃. 커서 위치를 중심으로 확대/축소한다.
            // 한 스텝당 약 10% 변화. 범위: 0.1x ~ 10x.
            var zoomFactor = args.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
            var oldScale = ViewportScale;
            var newScale = Math.Clamp(oldScale * zoomFactor, 0.1, 10.0);
            var cursorPos = args.GetPosition(this);

            // 커서 아래 월드 좌표를 줌 전후로 고정한다. (DEC-015 참조)
            // w = ScreenToWorld(cursor, vl1, s1)
            // 줌 후 조건: ScreenToWorld(cursor, vl2, s2) = w
            // → vl2 = w * s2 − cursor
            var worldUnderCursor = ViewportTransform.ScreenToWorld(cursorPos, ViewportLocation, oldScale);
            ViewportLocation = new Point(
                worldUnderCursor.X * newScale - cursorPos.X,
                worldUnderCursor.Y * newScale - cursorPos.Y);

            ViewportScale = newScale;
            args.Handled = true;
        }

        // node / connection 에서 bubble 로 올라옴.
        private void HandleKeyDown(object? sender, KeyEventArgs args)
        {
            if (!EditorGestures.Delete.Matches(args))
            {
                return;
            }

            if (args.Source is Node node)
            {
                var r = _viewModel.DelDagNodeItem(node.Id);
                if (!r)
                {
                    Debug.WriteLine("Failed");
                }
                args.Handled = true;
            }
            else if (args.Source is Connection connection)
            {
                var r = _viewModel.DelDagConnectionItem(connection.ConnectionId);
                if (!r)
                {
                    Debug.WriteLine("Failed");
                }
                args.Handled = true;
            }
        }

        #endregion

        #region Methods

        // TODO Unload 와 관련 및 GC 관련 해서 생각해보자.
        public void Dispose()
        {
            _disposables.Dispose();
            _viewModel.Dispose();
        }

        // 외부에 바인딩해야 해야 함. 입력 파라미터는 없어야 함.
        public void AddNode()
        {
            if (ContextMenuPoint is null)
            {
                return;
            }

            _viewModel.AddDagNodeItem(ContextMenuPoint);
        }

        // ContextMenu 말고 MenuFlyout 으로 해보자.

        #endregion

        /// <inheritdoc />
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            topLayer = e.NameScope.Find<Canvas>("PART_TopLayer");
            if (topLayer == null)
            {
                throw new InvalidOperationException("PART_TopLayer cannot be found in the template.");
            }
        }

        /// <inheritdoc />
        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            // TODO switch case 문이 좋을지 고민
            if (item is DagItems dagItems)
            {
                if (dagItems.NodeItem != null)
                {
                    if (dagItems.NodeItem.Location.HasValue)
                    {
                        // 여기서 실제로 SourceAnchor, TargetAnchor 가 생성된다.
                        // TODO 향후에 node 의 참조 해제 해야 한다.
                        var node = new Node(dagItems.NodeItem.Location.Value);
                        dagItems.NodeItem.SourceAnchor = node.SourceAnchor;
                        dagItems.NodeItem.TargetAnchor = node.TargetAnchor;
                        dagItems.NodeItem.NodeInstance = node;
                        // TODO node id update, NodeId 는 반드시 있어야 한다. 이거 nullable 하는 거에 대해서 생각해보자.
                        node.Id = dagItems.NodeItem.NodeId!.Value;
                        return node;
                    }
                }

                if (dagItems.ConnectionItem != null)
                {
                    if (dagItems.ConnectionItem.SourceAnchor.HasValue && dagItems.ConnectionItem.TargetAnchor.HasValue)
                    {
                        var connection = new Connection(dagItems.ConnectionItem.SourceAnchor.Value,
                            dagItems.ConnectionItem.TargetAnchor.Value);

                        connection.ConnectionId = dagItems.ConnectionItem.ConnectionId!.Value;
                        dagItems.ConnectionItem.ConnectionInstance = connection;

                        return connection;
                    }
                }
            }

            var emptyControl = new ContentControl { IsVisible = false };
            return emptyControl;
        }
    }
}
