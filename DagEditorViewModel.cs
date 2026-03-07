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

        private Point _viewportLocation = Constants.ZeroPoint;

        /// <summary>
        /// 현재 뷰포트 오프셋 (ViewportLocation = VCA.Offset).
        /// DagEditorCanvas TranslateTransform(-vl.X, -vl.Y) 의 소스.
        /// </summary>
        public Point ViewportLocation
        {
            get => _viewportLocation;
            set => this.RaiseAndSetIfChanged(ref _viewportLocation, value);
        }

        private double _viewportScale = 1.0;

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

        private readonly ObservableAsPropertyHelper<int> _nodeCount;
        private readonly ObservableAsPropertyHelper<int> _connectionCount;

        /// <summary>현재 그래프에 포함된 노드 수 (반응형 파생값).</summary>
        public int NodeCount => _nodeCount.Value;

        /// <summary>현재 그래프에 포함된 연결 수 (반응형 파생값).</summary>
        public int ConnectionCount => _connectionCount.Value;

        // ─── Undo / Redo (Feature 2) ──────────────────────────────────────────

        private readonly UndoRedoStack _undoRedo = new();

        public bool CanUndo => _undoRedo.CanUndo;

        public bool CanRedo => _undoRedo.CanRedo;

        public void Undo() => _undoRedo.Undo();

        public void Redo() => _undoRedo.Redo();

        // ─── Constructor ──────────────────────────────────────────────────────

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
        }

        // ─── Execute (undo 스택에 push하는 사용자 동작) ───────────────────────

        /// <summary>노드 추가. Undo/Redo 스택에 등록된다.</summary>
        public void ExecuteAddNode(Point? location)
        {
            if (!location.HasValue)
            {
                return;
            }

            _undoRedo.Execute(new AddNodeCommand(Dag, location.Value));
        }

        /// <summary>커넥션 추가. Undo/Redo 스택에 등록된다.</summary>
        public void ExecuteAddConnection(Point? source, Guid? sourceNodeId, Point? target, Guid? targetNodeId)
        {
            if (source == null || target == null)
            {
                return;
            }

            _undoRedo.Execute(new AddConnectionCommand(Dag, source.Value, sourceNodeId, target.Value, targetNodeId));
        }

        /// <summary>노드 삭제. 삭제 전 스냅샷을 캡처하여 Undo/Redo 스택에 등록한다.</summary>
        public void ExecuteDelNode(Guid nodeId)
        {
            var nodeItem = Dag.GetDagItemForNode(nodeId);
            if (nodeItem == null)
            {
                return;
            }

            var connItems = Dag.GetConnectionItemsForNode(nodeId);
            _undoRedo.Execute(new DelNodeCommand(Dag, nodeItem, connItems));
        }

        /// <summary>커넥션 삭제. 삭제 전 스냅샷을 캡처하여 Undo/Redo 스택에 등록한다.</summary>
        public void ExecuteDelConnection(Guid connectionId)
        {
            var connItem = Dag.DAGItemsSource
                .FirstOrDefault(i => i.ConnectionItem?.ConnectionId == connectionId);
            if (connItem == null)
            {
                return;
            }

            _undoRedo.Execute(new DelConnectionCommand(Dag, connItem));
        }

        /// <summary>노드 이동 Undo/Redo. 드래그 완료 시 DagEditor에서 호출한다.</summary>
        public void PushMoveNode(Guid nodeId, Point oldLocation, Point newLocation)
        {
            // Execute()를 호출하지 않고 스택에만 push (이동은 이미 완료됨).
            // AlreadyExecutedCommand는 첫 번째 Execute() 호출을 건너뛰고,
            // 이후 Redo 경로에서만 inner.Execute()를 수행한다.
            _undoRedo.Execute(new AlreadyExecutedCommand(new MoveNodeCommand(this, nodeId, oldLocation, newLocation)));
        }

        // ─── Direct Dag Access (명령 없이 직접 접근; 주로 내부/테스트용) ────────

        public DagItems? AddDagNodeItem(Point? location) => Dag.AddDagNodeItem(location);

        public DagItems? AddDagConnectionItem(Point? source, Guid? sourceNodeId, Point? target, Guid? targetNodeId) =>
            Dag.AddDagConnectionItem(source, sourceNodeId, target, targetNodeId);

        public bool DelDagNodeItem(Guid? nodeId) => Dag.DelDagNodeItem(nodeId);

        public bool DelDagConnectionItem(Guid connectionId) => Dag.DelDagConnectionItem(connectionId);

        public DagNode? FindNode(Guid nodeId) => Dag.FindNode(nodeId);

        // ─── Dispose ──────────────────────────────────────────────────────────

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }

    /// <summary>
    /// 이미 수행된 동작을 Undo/Redo 스택에 등록하기 위한 래퍼.
    /// Execute()는 첫 번째 호출(등록 시)을 건너뛰고, 이후 Redo 경로에서만 inner를 수행한다.
    /// </summary>
    public sealed class AlreadyExecutedCommand : IUndoableCommand
    {
        private readonly IUndoableCommand _inner;
        private bool _firstTime = true;

        public AlreadyExecutedCommand(IUndoableCommand inner)
        {
            _inner = inner;
        }

        public void Execute()
        {
            if (_firstTime)
            {
                _firstTime = false;
                return;
            }

            _inner.Execute();
        }

        public void Undo() => _inner.Undo();
    }
}
