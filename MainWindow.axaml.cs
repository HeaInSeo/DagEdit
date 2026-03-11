using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;
using System.Reactive.Disposables;

namespace DagEdit
{
    public partial class MainWindow : Window
    {
        private readonly CompositeDisposable _disposables = new();
        private NodeViewItemVisualFactory? _viewerFactory;

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
        ///
        /// H-0 Hardening additions:
        ///   4. _viewerFactory 를 필드로 유지 → realize/virtualize 카운터 관찰 가능
        ///   5. ProjectionChanged 마다 stats overlay 갱신
        /// </summary>
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            var vm = (DagEditorViewModel)EditorTester.DataContext!;

            _viewerFactory = new NodeViewItemVisualFactory();
            ViewerCanvas.VisualFactory = _viewerFactory;

            // projection 변경 시 새 SpatialIndex snapshot을 VCA에 공급하고 stats 갱신
            vm.ViewerAdapter.ProjectionChanged += (_, _) =>
            {
                ViewerCanvas.Items = vm.ViewerAdapter.BuildSnapshot();
                UpdateViewerStats(vm);
            };

            // DagEditor viewport → VCA viewer 단방향 동기화 (read-only viewer)
            vm.WhenAnyValue(x => x.ViewportLocation)
                .Subscribe(loc => ViewerCanvas.Offset = loc)
                .DisposeWith(_disposables);

            vm.WhenAnyValue(x => x.ViewportScale)
                .Subscribe(s => ViewerCanvas.Scale = s)
                .DisposeWith(_disposables);
        }

        /// <summary>
        /// H-0: debug stats overlay 갱신.
        ///
        /// 표시 항목:
        ///   flush  — ProjectionChanged 누적 발생 횟수 (per-op: add/remove/move 각 1)
        ///   built  — BuildSnapshot() 누적 호출 횟수 (flush 와 1:1이어야 정상)
        ///   items  — 현재 viewer projection item 수 (add = +1, remove = -1)
        ///   new    — factory.Realize 에서 새 Border 생성 횟수 (add 1회당 1 증가)
        ///   hit    — factory._pool 히트 횟수 (virtualize 후 재실현; 정상 흐름에서 0)
        ///   virt   — factory.Virtualize 호출 횟수 (remove 1회당 1 증가)
        ///
        /// 기대 패턴 (IsVirtualizing=False, stable ref):
        ///   add  1노드: flush+1, built+1, items+1, new+1, hit=0, virt=0
        ///   move 1노드: flush+1, built+1, items=same, new=0, hit=0, virt=0  ← key proof
        ///   del  1노드: flush+1, built+1, items-1, new=0, hit=0, virt+1
        /// </summary>
        private void UpdateViewerStats(DagEditorViewModel vm)
        {
            if (_viewerFactory == null)
            {
                return;
            }

            var a = vm.ViewerAdapter;
            var f = _viewerFactory;
            ViewerStatsText.Text =
                $"flush={a.ProjectionChangedCount} built={a.SnapshotBuildCount} items={a.Snapshots.Count}"
                + Environment.NewLine
                + $"new={f.RealizeNewCount} hit={f.RealizeHitCount} virt={f.VirtualizeCount}";
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            _disposables.Dispose();
        }
    }
}
