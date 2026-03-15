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
        private readonly ProjectionChangedSubscription _projectionChangedSubscription = new();
        private NodeViewItemVisualFactory? _viewerFactory;

        // H-2 pool cleanup: unsubscribe를 위해 adapter 참조와 delegate 저장
        private DagViewerProjectionAdapter? _viewerAdapterRef;
        private EventHandler<NodeViewItem>? _onItemRemoved;
        private EventHandler? _onProjectionChanged;

        // H-3 Pin/Unpin wiring
        private DagEditorViewModel? _viewModelRef;
        private EventHandler<Guid>? _onPinRequested;
        private EventHandler<Guid>? _onUnpinRequested;

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
        ///
        /// H-2 Pool cleanup:
        ///   6. ItemRemoved → factory.RemoveFromPool 연결 (명시적 unsubscribe 포함)
        /// </summary>
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            var vm = (DagEditorViewModel)EditorTester.DataContext!;

            _viewerFactory = new NodeViewItemVisualFactory();
            ViewerCanvas.VisualFactory = _viewerFactory;

            // H-2: ItemRemoved 이벤트 wiring — adapter가 factory를 직접 알지 못하도록 분리
            _viewerAdapterRef = vm.ViewerAdapter;
            _onItemRemoved = (_, item) => _viewerFactory?.RemoveFromPool(item);
            _viewerAdapterRef.ItemRemoved += _onItemRemoved;

            // H-3: Pin/Unpin wiring — ViewModel이 ViewerCanvas를 직접 알지 못하도록 분리
            _viewModelRef = vm;
            _onPinRequested = (_, nodeId) =>
            {
                if (vm.ViewerAdapter.Snapshots.TryGetValue(nodeId, out var item))
                {
                    ViewerCanvas.Pin(item);
                }
            };
            _onUnpinRequested = (_, nodeId) =>
            {
                if (vm.ViewerAdapter.Snapshots.TryGetValue(nodeId, out var item))
                {
                    ViewerCanvas.Unpin(item);
                }
            };
            vm.PinRequested += _onPinRequested;
            vm.UnpinRequested += _onUnpinRequested;

            // projection 변경 시 새 SpatialIndex snapshot을 VCA에 공급하고 stats 갱신
            _onProjectionChanged = HandleProjectionChanged;
            _projectionChangedSubscription.Attach(vm.ViewerAdapter, _onProjectionChanged);

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
        ///   virt   — factory.Virtualize 호출 횟수 (IsVirtualizing=False에서는 0)
        ///   pool   — H-2: factory._pool 현재 크기 (remove+cleanup 후 감소해야 함)
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
                + $"new={f.RealizeNewCount} hit={f.RealizeHitCount} virt={f.VirtualizeCount} pool={f.PoolCount}";
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            // H-2: ItemRemoved 명시적 unsubscribe
            if (_viewerAdapterRef != null && _onItemRemoved != null)
            {
                _viewerAdapterRef.ItemRemoved -= _onItemRemoved;
            }

            // H-3: Pin/Unpin 명시적 unsubscribe
            if (_viewModelRef != null)
            {
                if (_onPinRequested != null) { _viewModelRef.PinRequested -= _onPinRequested; }
                if (_onUnpinRequested != null) { _viewModelRef.UnpinRequested -= _onUnpinRequested; }
            }

            _projectionChangedSubscription.Detach();

            _disposables.Dispose();
        }

        private void HandleProjectionChanged(object? sender, EventArgs e)
        {
            if (_viewModelRef == null)
            {
                return;
            }

            ViewerCanvas.Items = _viewModelRef.ViewerAdapter.BuildSnapshot();
            UpdateViewerStats(_viewModelRef);
        }
    }

    internal sealed class ProjectionChangedSubscription
    {
        private DagViewerProjectionAdapter? _adapter;
        private EventHandler? _handler;

        public void Attach(DagViewerProjectionAdapter adapter, EventHandler handler)
        {
            if (ReferenceEquals(_adapter, adapter) && ReferenceEquals(_handler, handler))
            {
                return;
            }

            Detach();
            _adapter = adapter;
            _handler = handler;
            _adapter.ProjectionChanged += _handler;
        }

        public void Detach()
        {
            if (_adapter != null && _handler != null)
            {
                _adapter.ProjectionChanged -= _handler;
            }

            _adapter = null;
            _handler = null;
        }
    }
}
