using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using System.Reactive.Disposables;

namespace DagEdit
{
    public partial class MainWindow : Window
    {
        private readonly CompositeDisposable _disposables = new();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// G-0 Viewer wiring:
        ///   1. NodeViewItemVisualFactory 주입
        ///   2. ProjectionChanged → ViewerCanvas.Items = BuildSnapshot()
        ///   3. ViewportLocation/Scale → ViewerCanvas.Offset/Scale 단방향 동기화
        /// </summary>
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            var vm = (DagEditorViewModel)EditorTester.DataContext!;

            ViewerCanvas.VisualFactory = new NodeViewItemVisualFactory();

            // projection 변경 시 새 SpatialIndex snapshot을 VCA에 공급
            vm.ViewerAdapter.ProjectionChanged += (_, _) =>
                ViewerCanvas.Items = vm.ViewerAdapter.BuildSnapshot();

            // DagEditor viewport → VCA viewer 단방향 동기화 (read-only viewer)
            vm.WhenAnyValue(x => x.ViewportLocation)
                .Subscribe(loc => ViewerCanvas.Offset = loc)
                .DisposeWith(_disposables);

            vm.WhenAnyValue(x => x.ViewportScale)
                .Subscribe(s => ViewerCanvas.Scale = s)
                .DisposeWith(_disposables);
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            _disposables.Dispose();
        }
    }
}
