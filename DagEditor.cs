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
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using ReactiveUI;

namespace DagEdit
{
    public class DagEditor : SelectingItemsControl, IDisposable
    {
        #region Dependency Properties

        // Feature 3: ViewportLocation/Scale는 ViewModel이 Source of Truth.
        // DagEditor 속성은 PendingConnectionTemplate의 $parent 바인딩을 위한 패스스루다.
        // DagEditor 생성자에서 ViewModel.WhenAnyValue를 구독하여 자동 동기화한다.
        public static readonly StyledProperty<Point> ViewportLocationProperty =
            AvaloniaProperty.Register<DagEditor, Point>(
                nameof(ViewportLocation), Constants.ZeroPoint);

        public Point ViewportLocation
        {
            get => GetValue(ViewportLocationProperty);
            set => SetValue(ViewportLocationProperty, value);
        }

        public static readonly StyledProperty<double> ViewportScaleProperty =
            AvaloniaProperty.Register<DagEditor, double>(nameof(ViewportScale), 1.0);

        public double ViewportScale
        {
            get => GetValue(ViewportScaleProperty);
            set => SetValue(ViewportScaleProperty, value);
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

        #endregion

        #region Fields

        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        // 이건 connector 에서 올라오는 event
        private EventHandler<PendingConnectionEventArgs>? _connectionStartedHandler;
        private EventHandler<PendingConnectionEventArgs>? _connectionDragHandler;
        private EventHandler<PendingConnectionEventArgs>? _connectionCompleteHandler;
        // 이건 node 에서 올라오는 event
        private EventHandler<ConnectionChangedEventArgs>? _connectionChangedHandler;
        private EventHandler<NodeMovedEventArgs>? _nodeMovedHandler;

        private bool _IsRightBtnClicked;
        private readonly DagEditorViewModel _viewModel = new();

        // TODO 아래 변수들 코드 정리시 지운다.
        private bool _isLoaded = true;
        private Canvas? topLayer;
        private DagEditorCanvas? editorCanvas;

        // Panning 관련 포인터 위치 값
        private Point _previousPointerPosition;
        private Point _currentPointerPosition;

        // Selection Rectangle 관련 (Feature 1)
        private Point _selectionStart;
        private Rectangle? _selectionRect;

        // Viewport 양방향 동기화 재진입 방지 플래그 (Step 19)
        private bool _syncingViewport;

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

            // ─── Viewport 양방향 동기화 (Step 19: Viewport Contract Hardening) ───────
            // ViewModel이 Source of Truth. DagEditor StyledProperty는 PendingConnectionTemplate
            // $parent 바인딩 전용 패스스루다. 양방향 동기화는 외부 코드가 DagEditor 속성에
            // 직접 쓸 때(예: 테스트 하네스, 미래 VCA 바인딩)에도 ViewModel이 일치하도록 보장한다.
            // Avalonia StyledProperty와 ReactiveUI RaiseAndSetIfChanged는 모두
            // 값이 변하지 않으면 알림을 억제하므로 순환 루프가 발생하지 않는다.
            _viewModel.WhenAnyValue(x => x.ViewportLocation)
                .Subscribe(v =>
                {
                    if (_syncingViewport) { return; }
                    _syncingViewport = true;
                    ViewportLocation = v;
                    _syncingViewport = false;
                })
                .DisposeWith(_disposables);
            _viewModel.WhenAnyValue(x => x.ViewportScale)
                .Subscribe(v =>
                {
                    if (_syncingViewport) { return; }
                    _syncingViewport = true;
                    ViewportScale = v;
                    _syncingViewport = false;
                })
                .DisposeWith(_disposables);
            this.GetObservable(ViewportLocationProperty)
                .Subscribe(v =>
                {
                    if (_syncingViewport) { return; }
                    _syncingViewport = true;
                    _viewModel.ViewportLocation = v;
                    _syncingViewport = false;
                })
                .DisposeWith(_disposables);
            this.GetObservable(ViewportScaleProperty)
                .Subscribe(v =>
                {
                    if (_syncingViewport) { return; }
                    _syncingViewport = true;
                    _viewModel.ViewportScale = v;
                    _syncingViewport = false;
                })
                .DisposeWith(_disposables);
        }

        #endregion

        #region Event Handlers

        private void InitializeSubscriptions()
        {
            _connectionStartedHandler = HandleConnectionStarted;
            _connectionDragHandler = HandleConnectionDrag;
            _connectionCompleteHandler = HandleConnectionComplete;
            _connectionChangedHandler = HandleConnectionChanged;
            _nodeMovedHandler = HandleNodeMoved;

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
            // Node Moved (Undo/Redo 용)
            AddHandler(Node.NodeMovedEvent, _nodeMovedHandler);

            // 이벤트 핸들러 해제
            _disposables.Add(Disposable.Create(() =>
            {
                // PendingConnection
                RemoveHandler(Connector.PendingConnectionStartedEvent, _connectionStartedHandler);
                RemoveHandler(Connector.PendingConnectionDragEvent, _connectionDragHandler);
                RemoveHandler(Connector.PendingConnectionCompletedEvent, _connectionCompleteHandler);
                // Connection Changed
                RemoveHandler(Node.ConnectionChangedEvent, _connectionChangedHandler);
                // Node Moved
                RemoveHandler(Node.NodeMovedEvent, _nodeMovedHandler);
            }));
        }

        private void HandlePointerPressed(object? sender, PointerPressedEventArgs args)
        {
            var point = args.GetCurrentPoint(this);

            if (point.Properties.IsRightButtonPressed && !DisablePanning)
            {
                args.Pointer.Capture(this);
                // 클릭 위치를 캔버스 월드 좌표로 변환하여 저장한다.
                var rawPos = args.GetPosition(this);
                ContextMenuPoint = ViewportTransform.ScreenToWorld(rawPos, _viewModel.ViewportLocation, _viewModel.ViewportScale);
                _previousPointerPosition = rawPos;
                _IsRightBtnClicked = true;
                args.Handled = true;
            }
            else if (point.Properties.IsLeftButtonPressed && !args.Handled && topLayer != null)
            {
                // 빈 캔버스 영역 좌클릭 → Selection Rectangle 시작 (Feature 1)
                args.Pointer.Capture(this);
                _selectionStart = args.GetPosition(topLayer);
                SelectedArea = new Rect(_selectionStart, new Size(0, 0));
                IsSelecting = true;
                if (_selectionRect != null)
                {
                    Canvas.SetLeft(_selectionRect, _selectionStart.X);
                    Canvas.SetTop(_selectionRect, _selectionStart.Y);
                    _selectionRect.Width = 0;
                    _selectionRect.Height = 0;
                    _selectionRect.IsVisible = true;
                }

                args.Handled = true;
            }
        }

        private void HandlePointerMoved(object? sender, PointerEventArgs args)
        {
            if (_IsRightBtnClicked)
            {
                _currentPointerPosition = args.GetPosition(this);
                // 패닝 델타는 줌 배율과 무관하게 스크린 픽셀 단위로 ViewportLocation에 적용한다.
                _viewModel.ViewportLocation -= (_currentPointerPosition - _previousPointerPosition);
                _previousPointerPosition = _currentPointerPosition;
                IsPanning = true;
                args.Handled = true;
            }
            else if (IsSelecting && topLayer != null)
            {
                // Selection Rectangle 크기 업데이트 (Feature 1)
                var current = args.GetPosition(topLayer);
                SelectedArea = MakeNormalizedRect(_selectionStart, current);
                UpdateSelectionRect(SelectedArea);
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
            else if (IsSelecting)
            {
                // Selection Rectangle 확정 (Feature 1)
                IsSelecting = false;
                if (_selectionRect != null)
                {
                    _selectionRect.IsVisible = false;
                }
                args.Pointer.Capture(null);

                if (SelectedArea.Width > 2 || SelectedArea.Height > 2)
                {
                    FinalizeSelection();
                }

                args.Handled = true;
            }
        }

        private void HandleConnectionStarted(object? sender, PendingConnectionEventArgs args)
        {
            if (args.Source is SourceConnector)
            {
                IsVisiblePendingConnection = true;

                if (args.SourceAnchor.HasValue)
                {
                    SourceAnchor = args.SourceAnchor.Value;
                    TargetAnchor = SourceAnchor;
                }
                else
                {
                    SourceAnchor = default;
                    TargetAnchor = default;
                }

                args.Handled = true;
            }

            Debug.WriteLine("Ok!!!");
        }

        private void HandleConnectionDrag(object? sender, PendingConnectionEventArgs args)
        {
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
            // Undo/Redo 스택에 등록 (Feature 2)
            _viewModel.ExecuteAddConnection(args.SourceAnchor, args.SourceNodeId, args.TargetAnchor, args.TargetNodeId);
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

        private void HandleNodeMoved(object? sender, NodeMovedEventArgs args)
        {
            // 드래그 완료 후 MoveNodeCommand를 Undo 스택에 push한다 (Feature 2).
            _viewModel.PushMoveNode(args.NodeId, args.OldLocation, args.NewLocation);
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

                _isLoaded = false;
            }
        }

        private void HandlePointerWheelChanged(object? sender, PointerWheelEventArgs args)
        {
            // 마우스 휠로 줌 인/아웃. 커서 위치를 중심으로 확대/축소한다.
            var zoomFactor = args.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
            var oldScale = _viewModel.ViewportScale;
            var newScale = Math.Clamp(oldScale * zoomFactor, 0.1, 10.0);
            var cursorPos = args.GetPosition(this);

            var worldUnderCursor = ViewportTransform.ScreenToWorld(cursorPos, _viewModel.ViewportLocation, oldScale);
            _viewModel.ViewportLocation = new Point(
                worldUnderCursor.X * newScale - cursorPos.X,
                worldUnderCursor.Y * newScale - cursorPos.Y);

            _viewModel.ViewportScale = newScale;
            args.Handled = true;
        }

        private void HandleKeyDown(object? sender, KeyEventArgs args)
        {
            // ── Undo / Redo (Feature 2) ────────────────────────────────────────
            if (EditorGestures.Undo.Matches(args))
            {
                _viewModel.Undo();
                args.Handled = true;
                return;
            }

            if (EditorGestures.Redo.Matches(args) || EditorGestures.RedoAlt.Matches(args))
            {
                _viewModel.Redo();
                args.Handled = true;
                return;
            }

            // ── Delete ────────────────────────────────────────────────────────
            if (!EditorGestures.Delete.Matches(args))
            {
                return;
            }

            if (args.Source is Node node)
            {
                _viewModel.ExecuteDelNode(node.Id); // Undo/Redo 스택에 등록
                args.Handled = true;
            }
            else if (args.Source is Connection connection)
            {
                _viewModel.ExecuteDelConnection(connection.ConnectionId); // Undo/Redo 스택에 등록
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

            _viewModel.ExecuteAddNode(ContextMenuPoint); // Undo/Redo 스택에 등록
        }

        // ─── Selection Rectangle 보조 (Feature 1) ─────────────────────────────

        private static Rect MakeNormalizedRect(Point a, Point b)
        {
            var x = Math.Min(a.X, b.X);
            var y = Math.Min(a.Y, b.Y);
            var w = Math.Abs(a.X - b.X);
            var h = Math.Abs(a.Y - b.Y);
            return new Rect(x, y, w, h);
        }

        private void UpdateSelectionRect(Rect area)
        {
            if (_selectionRect == null)
            {
                return;
            }

            Canvas.SetLeft(_selectionRect, area.X);
            Canvas.SetTop(_selectionRect, area.Y);
            _selectionRect.Width = area.Width;
            _selectionRect.Height = area.Height;
        }

        private void FinalizeSelection()
        {
            // SelectedArea는 PART_TopLayer 로컬 좌표.
            // 월드 좌표계로 변환하여 노드 위치와 비교한다.
            var worldRect = new Rect(
                ViewportTransform.ScreenToWorld(SelectedArea.TopLeft, _viewModel.ViewportLocation, _viewModel.ViewportScale),
                ViewportTransform.ScreenToWorld(SelectedArea.BottomRight, _viewModel.ViewportLocation, _viewModel.ViewportScale));

            Selection.BeginBatchUpdate();
            Selection.Clear();
            var items = _viewModel.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var dagItem = items[i];
                if (dagItem.NodeItem?.Location is { } loc)
                {
                    var nodeWorldRect = new Rect(loc, new Size(Constants.NodeWidth, Constants.NodeHeight));
                    if (worldRect.Intersects(nodeWorldRect))
                    {
                        Selection.Select(i);
                    }
                }
            }

            Selection.EndBatchUpdate();
        }

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

            // Selection Rectangle을 PART_TopLayer에 프로그래매틱하게 추가 (Feature 1)
            _selectionRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(70, 130, 220)),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(40, 70, 130, 220)),
                IsHitTestVisible = false,
                IsVisible = false
            };
            topLayer.Children.Add(_selectionRect);
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
                        var node = new Node(dagItems.NodeItem.Location.Value);
                        dagItems.NodeItem.SourceAnchor = node.SourceAnchor;
                        dagItems.NodeItem.TargetAnchor = node.TargetAnchor;
                        dagItems.NodeItem.NodeInstance = node;
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
