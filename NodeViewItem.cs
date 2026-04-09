using System;
using Avalonia;
using VirtualCanvas.Core.Geometry;
using VirtualCanvas.Core.Spatial;

namespace DagEdit
{
    /// <summary>
    /// Phase 1 Viewer spike — read-only projection of DagNode into VCA's ISpatialItem.
    ///
    /// ─── Projection 이유 ─────────────────────────────────────────────────────────
    /// DagNode는 editor domain model이다: NodeInstance(UI Control), SourceConnections,
    /// TargetConnections 등 editor 전용 상태를 포함한다.
    /// DagNode를 ISpatialItem으로 직접 노출하면 VCA가 editor domain에 직접 의존하게 된다.
    ///
    /// NodeViewItem은 viewer가 필요한 최소 데이터만 캡처한다.
    /// editor 상태(NodeInstance, 연결 목록, UndoRedoStack 등)는 포함하지 않는다.
    ///
    /// ─── Stable Reference Contract (F-0-prep) ───────────────────────────────────
    /// VCA는 reference identity 기반으로 Control을 캐싱한다.
    /// 같은 논리적 노드를 매번 새 NodeViewItem으로 교체하면 VCA가 기존 Control을 버리고 재생성한다.
    /// 따라서 move/update 시 기존 object를 재사용하고 Bounds만 in-place 변경한다.
    /// → Bounds는 이 이유로 mutable(private set)이다.
    /// → NodeId는 여전히 불변 — object의 논리 identity.
    ///
    /// ─── Viewport 매핑 (참고용) ──────────────────────────────────────────────────
    /// DagEditorViewModel.ViewportLocation (Point)  ↔  VirtualCanvas.Offset (Point)
    /// DagEditorViewModel.ViewportScale   (double)  ↔  VirtualCanvas.Scale  (double)
    ///
    /// 타입과 수식이 동일하므로 어댑터 변환 없이 직접 바인딩 가능하다.
    /// (SelectionRectTests.TransformFormula_MatchesVcaFormula 로 이미 검증 완료)
    ///
    /// ─── VCA Handoff: Minimum Item Contract ────────────────────────────────────
    /// VCA viewer가 NodeViewItem으로부터 기대할 수 있는 최소 계약:
    ///
    ///   ISpatialItem 필드    DagEdit 소스
    ///   ─────────────────────────────────────────────────────────────────────
    ///   Bounds.X / Y        DagNode.Location (world coordinates, 그리드 스냅 적용 후)
    ///   Bounds.Width        Constants.NodeWidth  = 200
    ///   Bounds.Height       Constants.NodeHeight = 124
    ///   Priority            0.0  (단일 레벨, 향후 노드 타입별 구분 가능)
    ///   ZIndex              0    (단일 레벨)
    ///   IsVisible           true (항상 표시, 선택적 숨김 미지원)
    ///   NodeId (Guid)       DagNode.NodeId — 검색/상관관계 추적용 (ISpatialItem 외 추가 필드)
    ///
    /// 현재 미포함 (DagNode에 해당 필드 없음):
    ///   표시 레이블/제목    DagNode에 label 필드 없음 — 필요 시 별도 결정 필요
    ///
    /// 미검증 경로 (다음 spike에서 확인 필요):
    ///   IVisualFactory.Realize(NodeViewItem) → Avalonia Control 생성 여부
    ///   SpatialIndex.Insert(NodeViewItem)    → VCA 공간 인덱스 등록 연결
    ///   Node lifecycle 충돌 (R-C)           → DagEditorCanvas 자식 Node와 VCA realized Control 공존
    ///   Pinning                              → VCA API 미확정
    ///   Connection viewer item               → 이번 spike 범위 밖 (노드 먼저 검증)
    ///
    /// ─── 이 타입을 더 확장하지 않는 이유 ─────────────────────────────────────────
    /// 위 미검증 항목들이 해소되기 전에 NodeViewItem을 확장하면 premature abstraction이 된다.
    /// IVisualFactory / SpatialIndex 연결은 VCA 쪽 PoC가 먼저 시작한 후에 결정한다.
    /// </summary>
    internal sealed class NodeViewItem : ISpatialItem
    {
        // ─── Constructor ──────────────────────────────────────────────────────
        private NodeViewItem(Guid nodeId, VCRect bounds)
        {
            NodeId = nodeId;
            Bounds = bounds;
        }

        /// <summary>원본 DagNode의 NodeId. viewer가 DagNode와 상관관계를 추적할 때 사용한다.</summary>
        public Guid NodeId { get; }

        // ─── ISpatialItem ──────────────────────────────────────────────────────

        /// <summary>
        /// DagNode.Location을 TopLeft으로, NodeWidth/NodeHeight를 크기로 하는 world-coordinate 범위.
        /// VCA stable reference contract를 위해 private set 허용 — UpdateLocation()으로만 변경한다.
        /// </summary>
        public VCRect Bounds { get; private set; }

        /// <summary>기본 우선순위. viewer에서 노드 간 렌더링 순서는 현재 미구분.</summary>
        public double Priority => 0.0;

        /// <summary>기본 Z순서. 모든 노드는 동일 레벨.</summary>
        public int ZIndex => 0;

        /// <summary>항상 표시. viewer에서 IsVisible=false 제어는 이번 spike 범위 밖.</summary>
        public bool IsVisible => true;

        // ─── Factory ──────────────────────────────────────────────────────────
        /// <summary>
        /// DagNode에서 viewer projection을 생성한다.
        ///
        /// NodeId 또는 Location이 null이면 null을 반환한다.
        /// DagNode의 editor 상태(NodeInstance, 연결 목록 등)는 캡처하지 않는다.
        /// </summary>
        public static NodeViewItem? From(DagNode node)
        {
            if (node.NodeId == null || node.Location == null)
            {
                return null;
            }

            Point loc = node.Location.Value;
            return new NodeViewItem(
                node.NodeId.Value,
                new VCRect(loc.X, loc.Y, Constants.NodeWidth, Constants.NodeHeight));
        }

        // ─── In-place update ──────────────────────────────────────────────────
        /// <summary>
        /// 노드 위치가 변경되었을 때 Bounds를 in-place로 갱신한다.
        /// 이 메서드는 object reference를 유지하면서 위치만 바꾼다 (VCA stable reference contract).
        /// DagViewerProjectionAdapter.OnNodeMoved()만 호출한다.
        /// </summary>
        internal void UpdateLocation(Point location)
        {
            Bounds = new(location.X, location.Y, Constants.NodeWidth, Constants.NodeHeight);
        }
    }
}
