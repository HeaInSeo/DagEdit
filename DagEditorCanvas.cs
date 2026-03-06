using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DagEdit
{
    public sealed class DagEditorCanvas : Canvas, IDisposable
    {
        #region Dependency Properties

        public static readonly StyledProperty<Point> ViewportLocationProperty =
            AvaloniaProperty.Register<DagEditorCanvas, Point>(
                nameof(ViewportLocation), Constants.ZeroPoint);

        public Point ViewportLocation
        {
            get => GetValue(ViewportLocationProperty);
            set => SetValue(ViewportLocationProperty, value);
        }

        public static readonly StyledProperty<double> ScaleProperty =
            AvaloniaProperty.Register<DagEditorCanvas, double>(nameof(Scale), 1.0);

        public double Scale
        {
            get => GetValue(ScaleProperty);
            set => SetValue(ScaleProperty, value);
        }

        #endregion

        #region Fields

        private readonly ScaleTransform _scaleTransform = new(1.0, 1.0);
        private readonly TranslateTransform _translateTransform = new();
        private readonly CompositeDisposable _disposables = new();

        #endregion

        #region Constructor

        public DagEditorCanvas()
        {
            // ScaleTransform 먼저 적용 후 TranslateTransform:
            // 캔버스 점 (cx, cy) → 스크린 = (cx * scale - VL.X, cy * scale - VL.Y)
            // 역변환(스크린 → 캔버스): cx = (sx + VL.X) / scale
            var group = new TransformGroup();
            group.Children.Add(_scaleTransform);
            group.Children.Add(_translateTransform);
            RenderTransform = group;

            ViewportLocationProperty.Changed
                .Subscribe(OnViewportLocationChanged)
                .DisposeWith(_disposables);

            ScaleProperty.Changed
                .Subscribe(OnScaleChanged)
                .DisposeWith(_disposables);
        }

        #endregion

        //TODO 사이즈에 대한 것은 디버깅해서 살펴보자.
        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var child in Children)
            {
                // ILocatable 인터페이스를 구현하는지 확인
                if (child is ILocatable locatableChild)
                {
                    Point location = locatableChild.Location;
                    child.Arrange(new Rect(location, child.DesiredSize));
                }
                else
                {
                    // ILocatable을 구현하지 않는 경우, 기본 위치나 다른 로직을 사용하여 Arrange를 수행
                    // 기본 위치를 (0, 0)으로 설정
                    child.Arrange(new Rect(0, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                }
            }

            // TODO finalSize 한번 디버깅해서 봐야 한다.
            return finalSize;
        }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size constraint)
        {
            foreach (var child in Children)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }

            return default;
        }

        #region Methods

        private void OnViewportLocationChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is Point pointValue)
            {
                _translateTransform.X = -pointValue.X;
                _translateTransform.Y = -pointValue.Y;
            }
        }

        private void OnScaleChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is double scale)
            {
                _scaleTransform.ScaleX = scale;
                _scaleTransform.ScaleY = scale;
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        #endregion
    }
}
