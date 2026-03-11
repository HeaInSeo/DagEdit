using System;
using System.Collections.Generic;

namespace DagEdit
{
    /// <summary>
    /// Phase 1 Viewer spike — DagEdit 모델 변화를 viewer projection으로 변환하는 최소 adapter.
    ///
    /// ─── 역할 ──────────────────────────────────────────────────────────────────
    /// DagNode 변경(add/remove/move)을 명시적 메서드로 받아 NodeViewItem 스냅샷 집합을 갱신하고,
    /// Flush() 호출 시 ProjectionChanged를 단 1회 발생시킨다.
    ///
    /// ─── 설계 결정: SpatialIndex를 소유하지 않는 이유 ──────────────────────────
    /// SpatialIndex(VCA QuadTree)는 Extent 설정, Clear() 내부 RaiseChanged() 등
    /// VCA 구현 세부사항을 포함한다. 이를 adapter가 직접 관리하면 VCA 내부에 결합된다.
    /// adapter는 "무엇이 바뀌었는가"만 알고, "SpatialIndex를 어떻게 갱신할지"는 알지 않는다.
    ///
    /// 수신자(wiring 단계)가 ProjectionChanged를 받아 SpatialIndex를 갱신한다:
    ///   adapter.ProjectionChanged += (_, _) => { RebuildSpatialIndex(adapter.Snapshots); index.RaiseChanged(); };
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
    /// - SpatialIndex.Insert / RaiseChanged 실제 연결 (wiring 단계)
    /// - reactive/DynamicData 통합 (필요성 확인 후)
    /// - connection viewer item (노드 먼저 검증 완료 후)
    /// - hide/show: DagNode에 visibility 필드 없음.
    ///   숨김이 필요하면 OnNodeRemoved / OnNodeAdded 경로 사용 (동일 경로로 처리 가능)
    /// </summary>
    internal sealed class DagViewerProjectionAdapter
    {
        // NodeId → 현재 viewer projection 스냅샷
        private readonly Dictionary<Guid, NodeViewItem> _snapshots = new();

        // OnNode* 호출 후 Flush() 전에 미반영 변경이 있으면 true
        private bool _pendingFlush;

        // ─── 읽기 전용 노출 ───────────────────────────────────────────────────

        /// <summary>
        /// 현재 projection 스냅샷 집합. 읽기 전용.
        /// ProjectionChanged 수신자가 SpatialIndex 갱신 시 이 집합을 순회한다.
        /// </summary>
        public IReadOnlyDictionary<Guid, NodeViewItem> Snapshots => _snapshots;

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
        /// </summary>
        public void OnNodeRemoved(Guid nodeId)
        {
            if (_snapshots.Remove(nodeId))
            {
                _pendingFlush = true;
            }
        }

        /// <summary>
        /// 노드 위치가 변경되었을 때 호출한다.
        /// NodeViewItem은 불변이므로 기존 스냅샷을 제거하고 새 위치로 재생성한다.
        /// </summary>
        public void OnNodeMoved(DagNode node)
        {
            if (node.NodeId == null)
            {
                return;
            }

            _snapshots.Remove(node.NodeId.Value);

            var item = NodeViewItem.From(node);
            if (item != null)
            {
                _snapshots[item.NodeId] = item;
            }

            _pendingFlush = true;
        }

        // ─── Flush ────────────────────────────────────────────────────────────

        /// <summary>
        /// 보류 중인 변경이 있을 경우 ProjectionChanged를 1회 발생시킨다.
        /// 변경이 없으면 발생하지 않는다.
        ///
        /// 호출 시점 (정책은 VCA PoC 결과 후 결정):
        ///   - per-operation: OnNode* 직후 즉시 호출 (단순, 비용 미측정)
        ///   - per-command: UndoableCommand.Execute/Undo 완료 후 호출 (batch)
        ///   - per-frame: Dispatcher.Post 등 frame 단위 (throttle)
        /// </summary>
        public void Flush()
        {
            if (!_pendingFlush)
            {
                return;
            }

            _pendingFlush = false;
            ProjectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
