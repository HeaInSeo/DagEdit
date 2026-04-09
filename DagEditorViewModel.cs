using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using DynamicData;
using ReactiveUI;

namespace DagEdit
{
    /// <summary>
    /// DagEditor의 비즈니스 로직을 담당하는 ViewModel.
    ///
    /// ─── 아키텍처 분리 원칙 ──────────────────────────────────────────────────
    ///
    /// [역할]
    /// - Dag(데이터 모델)를 소유하고 Add/Del/Execute 연산을 위임한다.
    /// - ViewportLocation/ViewportScale: 뷰포트 상태의 Source of Truth.
    ///   DagEditor(View)는 이 값을 읽고 쓰며, 레이아웃 코드는 ViewModel을 통한다.
    /// - UndoRedoStack: 모든 사용자 동작의 명령 이력을 관리한다.
    ///
    /// [DagEditor와의 역할 분리]
    /// - DagEditorViewModel : 데이터 조작, 뷰포트 상태, Undo/Redo 이력
    /// - DagEditor           : UI 입력 처리(PointerPressed 등), 렌더링 계약(AvaloniaProperty)
    /// </summary>
    public sealed class DagEditorViewModel : ReactiveObject, IDisposable
    {
        private readonly CompositeDisposable _disposables = new();
        private readonly ObservableAsPropertyHelper<int> _nodeCount;
        private readonly ObservableAsPropertyHelper<int> _connectionCount;
        private readonly DagViewerProjectionAdapter _viewerAdapter = new();
        private readonly UndoRedoStack _undoRedo = new();

        private Point _viewportLocation = Constants.ZeroPoint;
        private double _viewportScale = 1.0;

        public DagEditorViewModel()
        {
            _disposables.Add(Dag);
            _disposables.Add(_undoRedo);

            _nodeCount = Dag.Connect()
                .Filter(x => x.NodeItem != null)
                .ToCollection()
                .Select(c => c.Count)
                .ToProperty(this, x => x.NodeCount, initialValue: 0);
            _disposables.Add(_nodeCount);

            _connectionCount = Dag.Connect()
                .Filter(x => x.ConnectionItem != null)
                .ToCollection()
                .Select(c => c.Count)
                .ToProperty(this, x => x.ConnectionCount, initialValue: 0);
            _disposables.Add(_connectionCount);

            // G-0: viewer adapter 동기화 — 노드 add/remove를 projection cache에 반영
            Dag.Connect()
                .Filter(x => x.NodeItem != null)
                .Subscribe(changes =>
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.Add:
                                _viewerAdapter.OnNodeAdded(change.Item.Current.NodeItem!);
                                break;
                            case ListChangeReason.Remove:
                                var removedId = change.Item.Current.NodeItem?.NodeId;
                                if (removedId.HasValue)
                                {
                                    _viewerAdapter.OnNodeRemoved(removedId.Value);
                                }

                                break;
                        }
                    }

                    _viewerAdapter.Flush();
                })
                .DisposeWith(_disposables);
        }

        // ─── H-3 Pin / Unpin (VCA 연동) ──────────────────────────────────────
        /// <summary>
        /// H-3: 노드를 VCA에 Pin 요청. DagEditor가 발생시키고 MainWindow가 ViewerCanvas.Pin()으로 처리.
        /// </summary>
        internal event EventHandler<Guid>? PinRequested;

        /// <summary>
        /// H-3: 노드의 VCA Pin 해제 요청. DagEditor가 발생시키고 MainWindow가 ViewerCanvas.Unpin()으로 처리.
        /// </summary>
        internal event EventHandler<Guid>? UnpinRequested;

        public Dag Dag { get; } = new();

        // ─── Items ────────────────────────────────────────────────────────────

        /// <summary>
        /// DynamicData SourceList에서 파생된 읽기 전용 컬렉션.
        /// DagEditor.axaml의 ItemsSource 바인딩 대상.
        /// </summary>
        public ReadOnlyObservableCollection<DagItems> Items => Dag.DAGItemsSource;

        // ─── Viewport State ────────────────────────────────────────────────────
        //
        // ViewportLocation / ViewportScale 은 뷰포트 상태의 Single Source of Truth.
        //
        // 책임 방향:
        //   ViewModel (여기)  ── WhenAnyValue ──▶  DagEditor StyledProperty (패스스루)
        //                     ◀── GetObservable ──  (양방향, _syncingViewport 가드)
        //
        // VCA 매핑:
        //   ViewportLocation  ≡  VirtualCanvas.Offset   (Point, 동일한 수식)
        //   ViewportScale     ≡  VirtualCanvas.Scale    (double, 동일한 수식)
        //   통합 시: VirtualCanvas.Offset ↔ ViewportLocation 을 양방향 바인딩하면 된다.

        /// <summary>
        /// 현재 뷰포트 오프셋 (ViewportLocation = VCA.Offset).
        /// DagEditorCanvas TranslateTransform(-vl.X, -vl.Y) 의 소스.
        /// </summary>
        public Point ViewportLocation
        {
            get => _viewportLocation;
            set => this.RaiseAndSetIfChanged(ref _viewportLocation, value);
        }

        /// <summary>
        /// 현재 줌 배율 (ViewportScale = VCA.Scale).
        /// DagEditorCanvas ScaleTransform(s, s) 의 소스.
        /// </summary>
        public double ViewportScale
        {
            get => _viewportScale;
            set => this.RaiseAndSetIfChanged(ref _viewportScale, value);
        }

        // ─── Reactive Counts ──────────────────────────────────────────────────

        /// <summary>현재 그래프에 포함된 노드 수 (반응형 파생값).</summary>
        public int NodeCount => _nodeCount.Value;

        /// <summary>현재 그래프에 포함된 연결 수 (반응형 파생값).</summary>
        public int ConnectionCount => _connectionCount.Value;

        // ─── Undo / Redo (Feature 2) ──────────────────────────────────────────
        public bool CanUndo => _undoRedo.CanUndo;

        public bool CanRedo => _undoRedo.CanRedo;

        // ─── Viewer Adapter (G-0) ─────────────────────────────────────────────

        /// <summary>
        /// Phase 1 Viewer wiring용 adapter. MainWindow.OnLoaded에서 ProjectionChanged를 구독한다.
        /// </summary>
        internal DagViewerProjectionAdapter ViewerAdapter => _viewerAdapter;

        public void Dispose()
        {
            _disposables.Dispose();
        }

        /// <summary>
        /// H-1 batch: command 실행 중 발생하는 N회 Flush를 1회로 압축한다.
        /// MoveNodeCommand.Undo/Execute 가 adapter.Flush()를 호출해도 batch 안에서 suppressed.
        /// </summary>
        public void Undo()
        {
            _viewerAdapter.BeginBatch();
            try
            {
                _undoRedo.Undo();
            }
            finally
            {
                _viewerAdapter.EndBatch();
            }
        }

        /// <summary>
        /// H-1 batch: command 실행 중 발생하는 N회 Flush를 1회로 압축한다.
        /// </summary>
        public void Redo()
        {
            _viewerAdapter.BeginBatch();
            try
            {
                _undoRedo.Redo();
            }
            finally
            {
                _viewerAdapter.EndBatch();
            }
        }

        // ─── Execute (undo 스택에 push하는 사용자 동작) ───────────────────────

        /// <summary>
        /// 노드 추가. Undo/Redo 스택에 등록된다.
        /// H-1: BeginBatch/EndBatch로 래핑 — 커맨드 내부에서 발생하는 복수 Flush를 1회로 압축.
        /// 외부에서 BeginBatch를 열면 중첩 batch로 동작하여 여러 ExecuteAddNode 호출도 1회 Flush.
        /// </summary>
        public void ExecuteAddNode(Point? location)
        {
            if (!location.HasValue)
            {
                return;
            }

            _viewerAdapter.BeginBatch();
            try
            {
                _undoRedo.Execute(new AddNodeCommand(Dag, location.Value));
            }
            finally
            {
                _viewerAdapter.EndBatch();
            }
        }

        /// <summary>
        /// 커넥션 추가. Undo/Redo 스택에 등록된다.
        /// H-1: BeginBatch/EndBatch 래핑.
        /// </summary>
        public void ExecuteAddConnection(Point? source, Guid? sourceNodeId, Point? target, Guid? targetNodeId)
        {
            if (source == null || target == null)
            {
                return;
            }

            _viewerAdapter.BeginBatch();
            try
            {
                _undoRedo.Execute(new AddConnectionCommand(Dag, source.Value, sourceNodeId, target.Value, targetNodeId));
            }
            finally
            {
                _viewerAdapter.EndBatch();
            }
        }

        /// <summary>
        /// 노드 삭제. 삭제 전 스냅샷을 캡처하여 Undo/Redo 스택에 등록한다.
        /// H-1: BeginBatch/EndBatch 래핑 — cascade connection 삭제 포함 복수 변경을 1회 Flush로.
        /// </summary>
        public void ExecuteDelNode(Guid nodeId)
        {
            var nodeItem = Dag.GetDagItemForNode(nodeId);
            if (nodeItem == null)
            {
                return;
            }

            var connItems = Dag.GetConnectionItemsForNode(nodeId);
            _viewerAdapter.BeginBatch();
            try
            {
                _undoRedo.Execute(new DelNodeCommand(Dag, nodeItem, connItems));
            }
            finally
            {
                _viewerAdapter.EndBatch();
            }
        }

        /// <summary>
        /// 커넥션 삭제. 삭제 전 스냅샷을 캡처하여 Undo/Redo 스택에 등록한다.
        /// H-1: BeginBatch/EndBatch 래핑.
        /// </summary>
        public void ExecuteDelConnection(Guid connectionId)
        {
            var connItem = Dag.DAGItemsSource
                .FirstOrDefault(i => i.ConnectionItem?.ConnectionId == connectionId);
            if (connItem == null)
            {
                return;
            }

            _viewerAdapter.BeginBatch();
            try
            {
                _undoRedo.Execute(new DelConnectionCommand(Dag, connItem));
            }
            finally
            {
                _viewerAdapter.EndBatch();
            }
        }

        /// <summary>노드 이동 Undo/Redo. 드래그 완료 시 DagEditor에서 호출한다.</summary>
        public void PushMoveNode(Guid nodeId, Point oldLocation, Point newLocation)
        {
            // Execute()를 호출하지 않고 스택에만 push (이동은 이미 완료됨).
            // AlreadyExecutedCommand는 첫 번째 Execute() 호출을 건너뛰고,
            // 이후 Redo 경로에서만 inner.Execute()를 수행한다.
            _undoRedo.Execute(new AlreadyExecutedCommand(new MoveNodeCommand(this, nodeId, oldLocation, newLocation)));
            _viewerAdapter.OnNodeMovedById(nodeId, newLocation);
            _viewerAdapter.Flush();
        }

        // ─── Direct Dag Access (명령 없이 직접 접근; 주로 내부/테스트용) ────────
        public DagItems? AddDagNodeItem(Point? location) => Dag.AddDagNodeItem(location);

        public DagItems? AddDagConnectionItem(Point? source, Guid? sourceNodeId, Point? target, Guid? targetNodeId) =>
            Dag.AddDagConnectionItem(source, sourceNodeId, target, targetNodeId);

        public bool DelDagNodeItem(Guid? nodeId) => Dag.DelDagNodeItem(nodeId);

        public bool DelDagConnectionItem(Guid connectionId) => Dag.DelDagConnectionItem(connectionId);

        public DagNode? FindNode(Guid nodeId) => Dag.FindNode(nodeId);

        /// <summary>
        /// H-1: 외부 batch scope를 연다. adapter.BeginBatch() 위임.
        /// 외부에서 여러 Execute* 호출을 하나의 ProjectionChanged로 묶고 싶을 때 사용.
        /// 중첩 가능 — 가장 바깥쪽 EndBatch()에서만 Flush가 발생한다.
        /// </summary>
        internal void BeginBatch() => _viewerAdapter.BeginBatch();

        /// <summary>H-1: 외부 batch scope를 닫는다. adapter.EndBatch() 위임.</summary>
        internal void EndBatch() => _viewerAdapter.EndBatch();

        /// <summary>
        /// H-1 viewer sync: MoveNodeCommand.Execute/Undo에서 직접 호출.
        /// Undo/Redo 래핑 안에서 호출되므로 Flush는 EndBatch까지 suppressed.
        /// </summary>
        internal void NotifyViewerNodeMoved(Guid nodeId, Point location)
        {
            _viewerAdapter.OnNodeMovedById(nodeId, location);
            _viewerAdapter.Flush();
        }

        internal void RequestPinNode(Guid nodeId) => PinRequested?.Invoke(this, nodeId);

        internal void RequestUnpinNode(Guid nodeId) => UnpinRequested?.Invoke(this, nodeId);
    }
}
