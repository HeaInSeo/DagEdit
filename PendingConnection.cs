using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using ReactiveUI;

namespace DagEdit
{
    /// <summary>
    /// SourceConnector 드래그 중에 표시되는 미리보기 연결선 컨트롤.
    ///
    /// ─── ReactiveUI WhenAnyValue 패턴 (PendingConnection 좌표 업데이트) ──────────
    ///
    /// [What it does]
    /// DagEditor가 SourceAnchor/TargetAnchor AvaloniaProperty를 설정하면,
    /// 그 변경을 PendingConnectionState로 전달하고,
    /// WhenAnyValue 구독이 PART_Connection의 Source/Target을 반응형으로 갱신한다.
    ///
    /// [Data Flow]
    ///   DagEditor (AXAML 바인딩)
    ///     → PendingConnection.SourceAnchor / TargetAnchor (AvaloniaProperty)
    ///       → _state.SourceAnchor / _state.TargetAnchor  (OnApplyTemplate 이전에도 안전)
    ///         → WhenAnyValue 체인 (OnApplyTemplate 이후 구독 시작)
    ///           → _partConnection.Source / _partConnection.Target
    ///
    ///   SetFillAndStroke 변경 → _partConnection.Fill / .Stroke (직접 갱신)
    ///   ViewportLocation  변경 → TranslateTransform.X / .Y    (직접 갱신)
    ///
    /// [Go Analogy]
    ///   _state.SourceAnchor  = 채널(chan Point) — 생산자: AvaloniaProperty 핸들러
    ///   WhenAnyValue 구독    = 소비자 고루틴    — for pt := range ch { partConn.Source = pt }
    ///   DistinctUntilChanged = "이전과 같으면 채널에 보내지 않는다" 관용구
    /// </summary>
    public sealed class PendingConnection : ContentControl, IDisposable
    {
        #region Dependency Properties

        public static readonly StyledProperty<Point> SourceAnchorProperty =
            AvaloniaProperty.Register<PendingConnection, Point>(nameof(SourceAnchor));

        public static readonly StyledProperty<Point> TargetAnchorProperty =
            AvaloniaProperty.Register<PendingConnection, Point>(nameof(TargetAnchor));

        public static readonly StyledProperty<object?> SourceConnectorProperty =
            AvaloniaProperty.Register<PendingConnection, object?>(nameof(SourceConnector));

        public static readonly StyledProperty<object?> TargetConnectorProperty =
            AvaloniaProperty.Register<PendingConnection, object?>(nameof(TargetConnector));

        public static readonly StyledProperty<bool> EnablePreviewProperty =
            AvaloniaProperty.Register<PendingConnection, bool>(nameof(EnablePreview));

        public static readonly StyledProperty<object?> PreviewTargetProperty =
            AvaloniaProperty.Register<PendingConnection, object?>(nameof(PreviewTarget));

        public static readonly StyledProperty<double> StrokeThicknessProperty =
            Shape.StrokeThicknessProperty.AddOwner<PendingConnection>();

        public static readonly StyledProperty<bool> EnableSnappingProperty =
            AvaloniaProperty.Register<PendingConnection, bool>(nameof(EnableSnapping), true);

        public static readonly StyledProperty<ConnectionDirection> DirectionProperty =
            Connection.DirectionProperty.AddOwner<PendingConnection>();

        public static readonly StyledProperty<IBrush?> SetFillAndStrokeProperty =
            AvaloniaProperty.Register<PendingConnection, IBrush?>(nameof(SetFillAndStroke), defaultValue: null);

        public static readonly StyledProperty<Point> ViewportLocationProperty =
            AvaloniaProperty.Register<PendingConnection, Point>(
                nameof(ViewportLocation), Constants.ZeroPoint);

        public static readonly StyledProperty<double> ViewportScaleProperty =
            AvaloniaProperty.Register<PendingConnection, double>(nameof(ViewportScale), 1.0);

        public Point SourceAnchor
        {
            get => GetValue(SourceAnchorProperty);
            set => SetValue(SourceAnchorProperty, value);
        }

        public Point TargetAnchor
        {
            get => GetValue(TargetAnchorProperty);
            set => SetValue(TargetAnchorProperty, value);
        }

        public object? SourceConnector
        {
            get => GetValue(SourceConnectorProperty);
            set => SetValue(SourceConnectorProperty, value);
        }

        public object? TargetConnector
        {
            get => GetValue(TargetConnectorProperty);
            set => SetValue(TargetConnectorProperty, value);
        }

        public bool EnablePreview
        {
            get => GetValue(EnablePreviewProperty);
            set => SetValue(EnablePreviewProperty, value);
        }

        public object? PreviewTarget
        {
            get => GetValue(PreviewTargetProperty);
            set => SetValue(PreviewTargetProperty, value);
        }

        public bool EnableSnapping
        {
            get => GetValue(EnableSnappingProperty);
            set => SetValue(EnableSnappingProperty, value);
        }

        public double StrokeThickness
        {
            get => GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public ConnectionDirection Direction
        {
            get => GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }

        public IBrush? SetFillAndStroke
        {
            get => GetValue(SetFillAndStrokeProperty);
            set => SetValue(SetFillAndStrokeProperty, value);
        }

        public Point ViewportLocation
        {
            get => GetValue(ViewportLocationProperty);
            set => SetValue(ViewportLocationProperty, value);
        }

        public double ViewportScale
        {
            get => GetValue(ViewportScaleProperty);
            set => SetValue(ViewportScaleProperty, value);
        }

        #endregion

        #region Fields

        // ── Reactive 상태 ──────────────────────────────────────────────────────
        // _state: SourceAnchor/TargetAnchor의 반응형 미러.
        //   AvaloniaProperty 변경 → _state 갱신 → WhenAnyValue → PART_Connection 갱신.
        private readonly PendingConnectionState _state = new();

        // _disposables: 모든 Rx 구독의 수명을 관리한다.
        //   Dispose() 호출 시 일괄 해제 → 메모리 누수 방지.
        private readonly CompositeDisposable _disposables = new();

        // _scaleTransform / _translateTransform: DagEditorCanvas와 동일한 TransformGroup을 구성한다.
        // TransformGroup(Scale(s), Translate(-vl.X, -vl.Y)) 덕분에
        // 월드 좌표(SourceAnchor/TargetAnchor)로 지정한 선이 줌/패닝 상태에서 노드와 정확히 일치한다.
        private readonly ScaleTransform _scaleTransform = new(1.0, 1.0);
        private readonly TranslateTransform _translateTransform = new();

        // _partConnection: OnApplyTemplate에서 채워지는 PART_Connection 참조.
        //   WhenAnyValue 구독의 대상이다.
        private Connection? _partConnection;

        // _templateDisposables: OnApplyTemplate마다 리셋되는 구독 관리.
        //   템플릿이 재적용될 때 이전 구독을 정리해 누적을 방지한다.
        private CompositeDisposable _templateDisposables = new();

        #endregion

        #region Static Constructor — Default Property Values

        static PendingConnection()
        {
            // AXAML <Setter> 이관: 런타임 스타일에 덮어쓰일 수 없는 metadata 기본값.
            // EnablePreviewProperty / EnableSnappingProperty 는 Register<PendingConnection,...> 로 선언되어
            // 있으므로 PendingConnection metadata가 Register 시점에 이미 설정된다.
            // OverrideDefaultValue<PendingConnection> 를 재호출하면 "Metadata is already set" 예외 발생 →
            // 제거하고 defaultValue를 Register 호출부에서 직접 지정한다.
            IsHitTestVisibleProperty.OverrideDefaultValue<PendingConnection>(false);
            StrokeThicknessProperty.OverrideDefaultValue<PendingConnection>(3.0);
            DirectionProperty.OverrideDefaultValue<PendingConnection>(ConnectionDirection.Forward);

            // AXAML <Template> 이관: C# FuncControlTemplate으로 완전 대체.
            TemplateProperty.OverrideDefaultValue<PendingConnection>(BuildTemplate());
        }

        #endregion

        #region Constructors

        public PendingConnection()
        {
            // DagEditorCanvas와 동일한 TransformGroup으로 줌/패닝을 동기화한다.
            var group = new TransformGroup();
            group.Children.Add(_scaleTransform);
            group.Children.Add(_translateTransform);
            RenderTransform = group;

            // ── AvaloniaProperty 변경 → PendingConnectionState 갱신 ─────────
            // AvaloniaObject는 IReactiveObject가 아니므로 WhenAnyValue 직접 불가.
            // GetObservable로 인스턴스 스코프 IObservable을 얻어 _state에 미러링.

            // ── ViewportScale 변경 → ScaleTransform 갱신 ─────────────────────
            this.GetObservable(ViewportScaleProperty)
                .Subscribe(s =>
                {
                    _scaleTransform.ScaleX = s;
                    _scaleTransform.ScaleY = s;
                })
                .DisposeWith(_disposables);

            this.GetObservable(SourceAnchorProperty)
                .Subscribe(pt => _state.SourceAnchor = pt)
                .DisposeWith(_disposables);

            this.GetObservable(TargetAnchorProperty)
                .Subscribe(pt => _state.TargetAnchor = pt)
                .DisposeWith(_disposables);

            // ── SetFillAndStroke 변경 → _partConnection 직접 갱신 ───────────
            // 상태 클래스 불필요: 계산 없이 브러시를 PART_Connection에 그대로 전달.
            this.GetObservable(SetFillAndStrokeProperty)
                .Subscribe(brush =>
                {
                    if (_partConnection != null)
                    {
                        _partConnection.Fill = brush;
                        _partConnection.Stroke = brush;
                    }
                })
                .DisposeWith(_disposables);

            // ── ViewportLocation 변경 → TranslateTransform 직접 갱신 ─────────
            // 패닝 오프셋은 단순 선형 변환이므로 상태 클래스 없이 직접 처리.
            this.GetObservable(ViewportLocationProperty)
                .Subscribe(pt =>
                {
                    _translateTransform.X = -pt.X;
                    _translateTransform.Y = -pt.Y;
                })
                .DisposeWith(_disposables);

            this.Unloaded += (_, _) => Dispose();
        }

        #endregion

        #region Template

        /// <summary>
        /// C# FuncControlTemplate: AXAML PendingConnection.axaml의 &lt;Template&gt; 완전 대체.
        ///
        /// TemplateLayoutCanvas(PART_Canvas)
        ///   └─ Connection(PART_Connection)
        ///
        /// Source/Target/Fill/Stroke는 OnApplyTemplate의 WhenAnyValue 구독이 갱신한다.
        /// 초기값은 SetFillAndStroke(DodgerBlue), Padding(0,0,5,5)로 설정한다.
        /// </summary>
        private static FuncControlTemplate BuildTemplate()
        {
            return new FuncControlTemplate<PendingConnection>((pc, ns) =>
            {
                var connection = new Connection
                {
                    Fill = Brushes.DodgerBlue,
                    Stroke = Brushes.DodgerBlue,
                    StrokeThickness = 3.0,
                };

                ns.Register("PART_Connection", connection);

                var canvas = new TemplateLayoutCanvas
                {
                    Background = Brushes.Transparent,
                };

                canvas.Children.Add(connection);
                ns.Register("PART_Canvas", canvas);

                return canvas;
            });
        }

        #endregion

        #region OnApplyTemplate — WhenAnyValue 구독

        /// <inheritdoc />
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // 이전 템플릿 구독 정리 — 테마 변경 등으로 재호출 시 누적 방지(#2)
            _templateDisposables.Dispose();
            _templateDisposables = new();

            _partConnection = e.NameScope.Find<Connection>("PART_Connection");

            if (_partConnection == null)
            {
                return;
            }

            // 현재 SetFillAndStroke 값으로 초기 브러시 적용
            var initialBrush = SetFillAndStroke ?? Brushes.DodgerBlue;
            _partConnection.Fill = initialBrush;
            _partConnection.Stroke = initialBrush;

            // ── WhenAnyValue: _state 변경 → PART_Connection 갱신 ─────────────
            //
            // Skip(1) 제거(#1): 구독 직후 현재 _state 값이 즉시 방출되어
            //   partConn.Source/Target을 초기화한다.
            //   템플릿 적용 전에 DagEditor.SourceAnchor가 이미 설정된 경우(드래그 중
            //   ContentPresenter 지연 렌더링)에도 첫 프레임부터 정확한 좌표가 반영된다.
            // DistinctUntilChanged: 동일 값 연속 수신 시 무시 (불필요한 렌더링 방지).
            //
            // 로컬 변수 partConn을 캡처해 null 안전성을 보장한다.
            var partConn = _partConnection;

            _state
                .WhenAnyValue(x => x.SourceAnchor)
                .DistinctUntilChanged()
                .Subscribe(pt => partConn.Source = pt)
                .DisposeWith(_templateDisposables);

            _state
                .WhenAnyValue(x => x.TargetAnchor)
                .DistinctUntilChanged()
                .Subscribe(pt => partConn.Target = pt)
                .DisposeWith(_templateDisposables);

            // ── Direction 변경 → PART_Connection 갱신 ────────────────────────
            this.GetObservable(DirectionProperty)
                .Subscribe(dir => partConn.Direction = dir)
                .DisposeWith(_templateDisposables);

            // ── StrokeThickness 변경 → PART_Connection 갱신 ──────────────────
            this.GetObservable(StrokeThicknessProperty)
                .Subscribe(thickness => partConn.StrokeThickness = thickness)
                .DisposeWith(_templateDisposables);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _templateDisposables.Dispose();
            _disposables.Dispose();
        }

        #endregion
    }
}
