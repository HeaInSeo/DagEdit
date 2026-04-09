using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DagEdit
{
    public class Connector : TemplatedControl, IDisposable
    {
        #region Fields

        public static readonly StyledProperty<Point> AnchorProperty =
            AvaloniaProperty.Register<Connector, Point>(nameof(Anchor));

        // 추가
        public static readonly StyledProperty<IBrush?> FillProperty =
            AvaloniaProperty.Register<Connector, IBrush?>(nameof(Fill));

        public static readonly RoutedEvent<PendingConnectionEventArgs> PendingConnectionStartedEvent =
            RoutedEvent.Register<Connector, PendingConnectionEventArgs>(
                nameof(PendingConnectionStarted),
                RoutingStrategies.Bubble);

        public static readonly RoutedEvent<PendingConnectionEventArgs> PendingConnectionCompletedEvent =
            RoutedEvent.Register<Connector, PendingConnectionEventArgs>(
                nameof(PendingConnectionCompleted),
                RoutingStrategies.Bubble);

        public static readonly RoutedEvent<PendingConnectionEventArgs> PendingConnectionDragEvent =
            RoutedEvent.Register<Connector, PendingConnectionEventArgs>(
                nameof(PendingConnectionDrag),
                RoutingStrategies.Bubble);

        private readonly CompositeDisposable _disposables = new();

        #endregion

        #region Constructors

        public Connector()
        {
            InitializeSubscriptions();

            // TODO axaml 에서 생성한 경우 Dispose 할 수 없는데 이렇게 하면 될까?
            this.Unloaded += (_, _) => this.Dispose();
        }

        #endregion

        #region Finalizer

        // 종료자
        ~Connector()
        {
            Dispose(false);
        }

        #endregion

        #region Events

        public event EventHandler<PendingConnectionEventArgs> PendingConnectionStarted
        {
            add => AddHandler(PendingConnectionStartedEvent, value);
            remove => RemoveHandler(PendingConnectionStartedEvent, value);
        }

        public event EventHandler<PendingConnectionEventArgs> PendingConnectionCompleted
        {
            add => AddHandler(PendingConnectionCompletedEvent, value);
            remove => RemoveHandler(PendingConnectionCompletedEvent, value);
        }

        public event EventHandler<PendingConnectionEventArgs> PendingConnectionDrag
        {
            add => AddHandler(PendingConnectionDragEvent, value);
            remove => RemoveHandler(PendingConnectionDragEvent, value);
        }

        #endregion

        #region Properties

        public Point Anchor
        {
            get => GetValue(AnchorProperty);
            set => SetValue(AnchorProperty, value);
        }

        public IBrush? Fill
        {
            get => GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        protected bool IsPointerPressed { get; set; }

        protected Connector? PreviousConnector { get; set; }

        #endregion

        #region Methods

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // 종료자 호출 억제
        }

        protected virtual void HandlePointerPressed(object? sender, PointerPressedEventArgs args)
        {
        }

        protected virtual void HandlePointerMoved(object? sender, PointerEventArgs args)
        {
        }

        protected virtual void HandlePointerReleased(object? sender, PointerReleasedEventArgs args)
        {
        }

        protected virtual void RaiseConnectionStartEvent(Connector? connector, Point? anchor)
        {
        }

        protected virtual void RaiseConnectionDragEvent(Connector? connector, Point? anchor, Vector? offset)
        {
        }

        protected virtual void RaiseConnectionCompletedEvent(
            Connector? connector,
            Point? inAnchor,
            Guid? inNodeId,
            Point? outAnchor,
            Guid? outNodeId)
        {
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 관리되는 자원 해제
                _disposables.Dispose();
            }

            // 관리되지 않는 자원 해제 코드가 필요한 경우 여기에 추가
        }

        private void InitializeSubscriptions()
        {
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
        }
    }
}
