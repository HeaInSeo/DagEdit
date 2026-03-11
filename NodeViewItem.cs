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
    /// NodeViewItem은 viewer가 필요한 최소 데이터만 캡처한 불변 스냅샷이다.
    /// editor 상태(NodeInstance, 연결 목록, UndoRedoStack 등)는 포함하지 않는다.
    ///
    /// ─── Viewport 매핑 (참고용) ──────────────────────────────────────────────────
    /// DagEditorViewModel.ViewportLocation (Point)  ↔  VirtualCanvas.Offset (Point)
    /// DagEditorViewModel.ViewportScale   (double)  ↔  VirtualCanvas.Scale  (double)
    ///
    /// 타입과 수식이 동일하므로 어댑터 변환 없이 직접 바인딩 가능하다.
    /// (SelectionRectTests.TransformFormula_MatchesVcaFormula 로 이미 검증 완료)
    ///
    /// ─── 이 타입의 범위 ──────────────────────────────────────────────────────────
    /// - 노드 위치/크기 → VCA SpatialIndex 등록용 Bounds
    /// - Connection viewer item은 이번 spike 범위 밖 (노드 먼저 검증)
    /// - IVisualFactory, SpatialIndex 연결은 다음 diff에서 수행
    /// </summary>
    internal sealed class NodeViewItem : ISpatialItem
    {
        /// <summary>원본 DagNode의 NodeId. viewer가 DagNode와 상관관계를 추적할 때 사용한다.</summary>
        public Guid NodeId { get; }

        // ─── ISpatialItem ──────────────────────────────────────────────────────

        /// <summary>
        /// DagNode.Location을 TopLeft으로, NodeWidth/NodeHeight를 크기로 하는 world-coordinate 범위.
        /// </summary>
        public VCRect Bounds { get; }

        /// <summary>기본 우선순위. viewer에서 노드 간 렌더링 순서는 현재 미구분.</summary>
        public double Priority => 0.0;

        /// <summary>기본 Z순서. 모든 노드는 동일 레벨.</summary>
        public int ZIndex => 0;

        /// <summary>항상 표시. viewer에서 IsVisible=false 제어는 이번 spike 범위 밖.</summary>
        public bool IsVisible => true;

        // ─── Constructor ──────────────────────────────────────────────────────

        private NodeViewItem(Guid nodeId, VCRect bounds)
        {
            NodeId = nodeId;
            Bounds = bounds;
        }

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
    }
}
