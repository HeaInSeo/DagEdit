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
    /// - Dag(데이터 모델)를 소유하고 Add/Del 연산을 위임한다.
    /// - DagEditor(커스텀 컨트롤)의 DataContext로 사용된다.
    /// - Items: DynamicData SourceList에서 파생된 ReadOnlyObservableCollection.
    ///   AvaloniaList 대신 SourceList를 사용함으로써 Rx 파이프라인과 자연스럽게 연동된다.
    ///
    /// [DagEditor와의 역할 분리]
    /// - DagEditorViewModel : 데이터 조작, 아이템 목록 노출
    /// - DagEditor           : UI 입력 처리(PointerPressed 등), 렌더링 계약(AvaloniaProperty)
    /// </summary>
    public sealed class DagEditorViewModel : ReactiveObject, IDisposable
    {
        private readonly CompositeDisposable _disposables = new();

        public Dag Dag { get; } = new();

        /// <summary>
        /// DynamicData SourceList에서 파생된 읽기 전용 컬렉션.
        /// DagEditor.axaml의 ItemsSource 바인딩 대상.
        /// </summary>
        public ReadOnlyObservableCollection<DagItems> Items => Dag.DAGItemsSource;

        private readonly ObservableAsPropertyHelper<int> _nodeCount;
        private readonly ObservableAsPropertyHelper<int> _connectionCount;

        /// <summary>현재 그래프에 포함된 노드 수 (반응형 파생값).</summary>
        public int NodeCount => _nodeCount.Value;

        /// <summary>현재 그래프에 포함된 연결 수 (반응형 파생값).</summary>
        public int ConnectionCount => _connectionCount.Value;

        public DagEditorViewModel()
        {
            _disposables.Add(Dag);

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

        public bool AddDagNodeItem(Point? location) =>
            Dag.AddDagNodeItem(location);

        public bool AddDagConnectionItem(Point? source, Guid? sourceNodeId, Point? target, Guid? targetNodeId) =>
            Dag.AddDagConnectionItem(source, sourceNodeId, target, targetNodeId);

        public bool DelDagNodeItem(Guid? nodeId) =>
            Dag.DelDagNodeItem(nodeId);

        public bool DelDagConnectionItem(Guid connectionId) =>
            Dag.DelDagConnectionItem(connectionId);

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
