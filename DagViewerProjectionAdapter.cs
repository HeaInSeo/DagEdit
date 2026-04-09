using System;
using System.Collections.Generic;
using VirtualCanvas.Core.Geometry;
using VirtualCanvas.Core.Spatial;

namespace DagEdit
{
    /// <summary>
    /// Phase 1 Viewer spike — DagEdit 모델 변화를 viewer projection으로 변환하는 최소 adapter.
    ///
    /// ─── 역할 ──────────────────────────────────────────────────────────────────
    /// DagNode 변경(add/remove/move)을 명시적 메서드로 받아 NodeViewItem 스냅샷 집합을 갱신하고,
    /// Flush() 호출 시 ProjectionChanged를 단 1회 발생시킨다.
    ///
    /// ─── 설계 결정: persistent SpatialIndex 미소유, snapshot 생성 ────────────────
    /// adapter는 SpatialIndex를 영구적으로 소유하지 않는다.
    /// 이유:
    ///   Clear() 내부 RaiseChanged() + 마지막 RaiseChanged() 중복 발생 위험
    ///   같은 index를 계속 비우고 채우면 stale bounds / tree 상태 해석이 어렵다
    ///
    /// 대신 ProjectionChanged 발생 시 BuildSnapshot()으로 새 SpatialIndex를 만들어 교체:
    ///   adapter.ProjectionChanged += (_, _) => virtualCanvas.Items = adapter.BuildSnapshot();
    ///
    /// projection item refs는 stable(same object)이므로 새 snapshot에 같은 refs를 넣어도
    /// VCA 쪽 Control reuse 기회가 유지될 수 있다 (F-0-prep stable ref 계약).
    ///
    /// ─── 설계 결정: 명시적 호출 기반 ───────────────────────────────────────────
    /// DynamicData/ReactiveUI observer 확장 없이 OnNode* 메서드를 명시적으로 호출한다.
    /// reactive 통합은 VCA PoC 결과를 보고 필요성을 확인한 후 결정한다.
    ///
    /// ─── 설계 결정: Flush() 정책 미확정 ────────────────────────────────────────
    /// Flush() 호출 빈도(per-operation vs batch-per-frame vs command-완료-시점)는
    /// VCA RaiseChanged() 비용 측정 후 결정한다. 이번 spike에서는 확정하지 않는다.
    ///
    /// ─── 미구현 / 다음 spike ─────────────────────────────────────────────────
    /// - reactive/DynamicData 통합 (필요성 확인 후)
    /// - connection viewer item (노드 먼저 검증 완료 후)
    /// - hide/show: DagNode에 visibility 필드 없음.
    ///   숨김이 필요하면 OnNodeRemoved / OnNodeAdded 경로 사용
    /// </summary>
    internal sealed class DagViewerProjectionAdapter
    {
        /// <summary>
        /// Viewer wiring용 기본 world extent.
        /// </summary>
        internal static readonly VCRect DefaultProjectionExtent =
            new(0, 0, 50_000, 50_000);

        // NodeId → 현재 viewer projection 스냅샷
        private readonly Dictionary<Guid, NodeViewItem> _snapshots = new();

        // OnNode* 호출 후 Flush() 전에 미반영 변경이 있으면 true
        private bool _pendingFlush;

        // H-1 batch: 0 이면 즉시 flush, > 0 이면 EndBatch까지 suppressed
        private int _batchDepth;

        // ─── Changed signal ───────────────────────────────────────────────────

        /// <summary>
        /// Flush() 호출 시 발생하는 이벤트. VCA.SpatialIndex.RaiseChanged()에 해당한다.
        ///
        /// 수신자 예시 (wiring 단계):
        ///   adapter.ProjectionChanged += (_, _) =>
        ///   {
        ///       spatialIndex.Clear();
        ///       foreach (var item in adapter.Snapshots.Values) spatialIndex.Insert(item);
        ///       spatialIndex.RaiseChanged();
        ///   };
        /// </summary>
        public event EventHandler? ProjectionChanged;

        /// <summary>
        /// H-2 pool cleanup: 노드가 _snapshots에서 제거될 때 해당 NodeViewItem을 인수로 발생.
        ///
        /// 수신자는 이 이벤트를 구독하여 자체 캐시(예: factory._pool)를 정리할 수 있다.
        /// 어댑터는 factory를 직접 알지 못하며, wiring은 호출 측(MainWindow)이 담당한다.
        ///
        /// 발생 시점: OnNodeRemoved() 에서 실제로 제거가 일어날 때.
        /// 발생하지 않는 경우: nodeId가 _snapshots에 없어 no-op인 경우.
        /// </summary>
        public event EventHandler<NodeViewItem>? ItemRemoved;

        // ─── 읽기 전용 노출 ───────────────────────────────────────────────────

        /// <summary>
        /// 현재 projection 스냅샷 집합. 읽기 전용.
        /// ProjectionChanged 수신자가 SpatialIndex 갱신 시 이 집합을 순회한다.
        /// </summary>
        public IReadOnlyDictionary<Guid, NodeViewItem> Snapshots => _snapshots;

        // ─── H-0 Observability counters ──────────────────────────────────────

        /// <summary>
        /// Flush() 호출 시 ProjectionChanged가 실제로 발생한 누적 횟수.
        /// per-operation flush 정책 하에서는 add/remove/move 각 1회씩 증가한다.
        /// </summary>
        public int ProjectionChangedCount { get; private set; }

        /// <summary>
        /// BuildSnapshot() 호출 누적 횟수 (ProjectionChanged 수신자가 호출하는 경우).
        /// ProjectionChangedCount와 1:1이어야 정상 — 수신자가 BuildSnapshot을 누락하면 diverge.
        /// </summary>
        public int SnapshotBuildCount { get; private set; }

        /// <summary>
        /// H-1 batch: BeginBatch/EndBatch 쌍으로 묶여서 1회로 압축된 Flush 누적 횟수.
        /// 예: 10 OnNodeAdded + 1 EndBatch → BatchedFlushCount + 1, ProjectionChangedCount + 1.
        /// </summary>
        public int BatchedFlushCount { get; private set; }

        // ─── Mutation methods ─────────────────────────────────────────────────

        /// <summary>
        /// 노드가 Dag에 추가되었을 때 호출한다.
        /// NodeId 또는 Location이 null이면 무시한다.
        /// </summary>
        public void OnNodeAdded(DagNode node)
        {
            var item = NodeViewItem.From(node);
            if (item == null)
            {
                return;
            }

            _snapshots[item.NodeId] = item;
            _pendingFlush = true;
        }

        /// <summary>
        /// 노드가 Dag에서 제거되었을 때 호출한다.
        /// 존재하지 않는 nodeId는 무시한다.
        ///
        /// 제거 성공 시 ItemRemoved 이벤트를 발생시켜 외부 캐시(factory pool 등)가
        /// 해당 projection item을 정리할 수 있게 한다.
        /// </summary>
        public void OnNodeRemoved(Guid nodeId)
        {
            if (_snapshots.Remove(nodeId, out NodeViewItem? removed))
            {
                ItemRemoved?.Invoke(this, removed);
                _pendingFlush = true;
            }
        }

        /// <summary>
        /// 노드 위치가 변경되었을 때 호출한다.
        /// cache에 이미 있으면 Bounds를 in-place 변경하여 stable reference를 유지한다.
        /// cache에 없으면 (방어적) 새 projection을 생성하여 등록한다.
        ///
        /// VCA identity contract: 같은 NodeId에 대해 항상 같은 NodeViewItem object를 반환해야
        /// VCA가 기존 Control을 재사용할 수 있다. object를 교체하면 Control이 재생성된다.
        /// </summary>
        public void OnNodeMovedById(Guid nodeId, Avalonia.Point newLocation)
        {
            if (_snapshots.TryGetValue(nodeId, out NodeViewItem? existing))
            {
                existing.UpdateLocation(newLocation);
                _pendingFlush = true;
            }
        }

        /// <summary>
        /// 노드 위치가 변경되었을 때 호출한다.
        /// cache에 이미 있으면 Bounds를 in-place 변경하여 stable reference를 유지한다.
        /// cache에 없으면 (방어적) 새 projection을 생성하여 등록한다.
        ///
        /// VCA identity contract: 같은 NodeId에 대해 항상 같은 NodeViewItem object를 반환해야
        /// VCA가 기존 Control을 재사용할 수 있다. object를 교체하면 Control이 재생성된다.
        /// </summary>
        public void OnNodeMoved(DagNode node)
        {
            if (node.NodeId == null || node.Location == null)
            {
                return;
            }

            if (_snapshots.TryGetValue(node.NodeId.Value, out NodeViewItem? existing))
            {
                // Stable reference: 기존 object의 위치만 갱신
                existing.UpdateLocation(node.Location.Value);
            }
            else
            {
                // 방어적 경로: cache miss (move before add 등)
                var item = NodeViewItem.From(node);
                if (item != null)
                {
                    _snapshots[item.NodeId] = item;
                }
            }

            _pendingFlush = true;
        }

        // ─── Batch scope (H-1) ───────────────────────────────────────────────

        /// <summary>
        /// batch scope를 연다. 이 호출 이후 Flush()는 suppressed된다.
        ///
        /// 중첩 가능 — 가장 바깥쪽 EndBatch()에서만 실제 Flush가 발생한다.
        ///
        /// 사용 패턴:
        ///   adapter.BeginBatch();
        ///   try { ... 여러 OnNode* + Flush() 호출 ... }
        ///   finally { adapter.EndBatch(); }
        ///
        /// 현재 사용처:
        ///   DagEditorViewModel.Undo() / Redo() — 하나의 command가 N개 SourceList 변경을
        ///   유발할 경우 N회 Flush를 1회로 줄인다.
        /// </summary>
        public void BeginBatch() => _batchDepth++;

        /// <summary>
        /// batch scope를 닫는다.
        /// 가장 바깥쪽 EndBatch() 시점에 pending flush가 있으면 1회 Flush한다.
        /// </summary>
        public void EndBatch()
        {
            if (_batchDepth > 0)
            {
                _batchDepth--;
            }

            if (_batchDepth == 0 && _pendingFlush)
            {
                BatchedFlushCount++;
                Flush();
            }
        }

        // ─── Flush ────────────────────────────────────────────────────────────

        /// <summary>
        /// 보류 중인 변경이 있을 경우 ProjectionChanged를 1회 발생시킨다.
        /// 변경이 없으면 발생하지 않는다.
        ///
        /// batch scope(_batchDepth > 0) 중에는 suppressed — EndBatch()가 대신 호출한다.
        ///
        /// 호출 시점:
        ///   - per-operation (기본): OnNode* 직후 호출 (단건 add/remove/move)
        ///   - per-batch: Undo/Redo 래핑 → EndBatch() 경유 (batch-per-command)
        /// </summary>
        public void Flush()
        {
            // batch scope 중이면 suppressed: _pendingFlush는 유지, 실제 신호는 EndBatch에서
            if (_batchDepth > 0)
            {
                return;
            }

            if (!_pendingFlush)
            {
                return;
            }

            _pendingFlush = false;
            ProjectionChangedCount++;
            ProjectionChanged?.Invoke(this, EventArgs.Empty);
        }

        // ─── Snapshot builder (F-0 wiring) ───────────────────────────────────

        /// <summary>
        /// 현재 stable projection refs로 새 SpatialIndex snapshot을 생성한다.
        ///
        /// F-0 wiring 패턴 (증명용):
        ///   adapter.ProjectionChanged += (_, _) => virtualCanvas.Items = adapter.BuildSnapshot();
        ///
        /// 호출 시마다 새 SpatialIndex 인스턴스를 반환한다.
        /// projection item refs는 stable하므로 새 snapshot에 같은 refs를 넣어도
        /// VCA 쪽 _visualMap lookup에서 Control reuse 기회가 유지된다.
        ///
        /// 이 방식은 증명용 wiring이다:
        ///   - Clear()+Insert 방식의 double notify / stale tree 위험을 피한다
        ///   - 매번 새 index를 만드는 비용은 VCA PoC 결과 후 평가한다
        ///   - 최종 incremental/batching 구조는 아직 아님
        /// </summary>
        public SpatialIndex BuildSnapshot() => BuildSnapshot(DefaultProjectionExtent);

        /// <summary>
        /// 지정된 extent로 SpatialIndex snapshot을 생성한다.
        /// </summary>
        public SpatialIndex BuildSnapshot(VCRect extent)
        {
            SnapshotBuildCount++;
            var snapshot = new SpatialIndex { Extent = extent };
            foreach (NodeViewItem item in _snapshots.Values)
            {
                snapshot.Insert(item);
            }

            return snapshot;
        }
    }
}
