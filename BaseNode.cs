using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace DagEdit
{
    public class BaseNode : ContentControl, IDisposable, ILocatable
    {
        #region Fields

        public static readonly StyledProperty<Point> LocationProperty =
            AvaloniaProperty.Register<BaseNode, Point>(nameof(Location), Constants.ZeroPoint);

        public static readonly StyledProperty<Point?> SourceAnchorProperty =
            AvaloniaProperty.Register<BaseNode, Point?>(nameof(SourceAnchor));

        public static readonly StyledProperty<Point?> TargetAnchorProperty =
            AvaloniaProperty.Register<BaseNode, Point?>(nameof(TargetAnchor));

        private readonly CompositeDisposable _disposables = new();

        #endregion

        #region Static Constructor

        static BaseNode()
        {
            // Location 변경 시 부모 캔버스의 Arrange를 무효화한다.
            // DagEditorCanvas.ArrangeOverride가 재실행되어 노드가 새 위치에 배치됨.
            // 이 방식은 Canvas.Left/Top 첨부 프로퍼티 패턴과 동일하다.
            LocationProperty.Changed.AddClassHandler<BaseNode>((node, _) =>
                (node.GetVisualParent() as Layoutable)?.InvalidateArrange());
        }

        #endregion

        #region Constructors

        protected BaseNode()
        {
            InitializeSubscriptions();

            // 비주얼 트리에서 제거될 때 Rx 구독을 스스로 해제한다.
            // Dag 같은 모델 계층이 직접 Dispose를 호출할 필요 없게 만드는 장치.
            this.Unloaded += (_, _) => Dispose();
        }

        #endregion

        #region Finalizers

        // 종료자
        ~BaseNode()
        {
            Dispose(false);
        }

        #endregion

        #region Properties

        public Point Location
        {
            get => GetValue(LocationProperty);
            set => SetValue(LocationProperty, value);
        }

        /// <summary>
        /// StartNode는 OutAnchor 가 있고 EndNode 는 InAnchor 가 있다.
        /// 일반 Node 는 OutAnchor 와 InAnchor 가 있다.
        /// </summary>
        public Point? SourceAnchor
        {
            get => GetValue(SourceAnchorProperty);
            set => SetValue(SourceAnchorProperty, value);
        }

        public Point? TargetAnchor
        {
            get => GetValue(TargetAnchorProperty);
            set => SetValue(TargetAnchorProperty, value);
        }

        protected bool IsDragging { get; set; }

        #endregion

        #region Methods

        // TODO Dispose 관련해서 테스트 해봐야 함.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // 종료자 호출 억제
        }

        public void Hide()
        {
            if (IsVisible)
            {
                IsVisible = false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 관리되는 자원 해제
                _disposables.Dispose();
            }

            // 관리되지 않는 자원 해제 코드가 필요한 경우 여기에 추가
        }

        #endregion

        #region Event Handlers

        protected virtual void HandlePointerPressed(object? sender, PointerPressedEventArgs args)
        {
        }

        protected virtual void HandlePointerMoved(object? sender, PointerEventArgs args)
        {
        }

        protected virtual void HandlePointerReleased(object? sender, PointerReleasedEventArgs args)
        {
        }

        /*protected virtual void HandleKeyDown(object? sender, KeyEventArgs args)
        {
        }*/

        protected virtual void HandleLoaded(object? sender, RoutedEventArgs args)
        {
        }

        #endregion

        #region Helpers

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

            /*Observable.FromEventPattern<KeyEventArgs>(
                    h => this.KeyDown += h,
                    h => this.KeyDown -= h)
                .Subscribe(args => HandleKeyDown(args.Sender, args.EventArgs))
                .DisposeWith(_disposables); */
        }

        #endregion
    }
}
