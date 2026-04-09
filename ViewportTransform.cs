using Avalonia;

namespace DagEdit
{
    /// <summary>
    /// DagEditor 뷰포트 좌표 변환 유틸리티.
    ///
    /// 좌표계 계약 — 월드 좌표가 Source of Truth:
    ///   - 노드 위치(Node.Location), 앵커(SourceAnchor/TargetAnchor),
    ///     커넥션 앵커(DagConnection.SourceAnchor/TargetAnchor) 는 모두 월드 좌표로 저장·전달한다.
    ///   - 스크린 좌표는 렌더링·포인터 입력 처리 시의 파생값이다.
    ///
    /// DagEditorCanvas 및 PendingConnection 의 RenderTransform:
    ///   TransformGroup(Scale(s), Translate(-vl.X, -vl.Y))
    ///
    ///   월드 점 (wx, wy) → 스크린 점: (wx * s − vl.X,  wy * s − vl.Y)
    ///   스크린 점 (sx, sy) → 월드 점: ((sx + vl.X) / s, (sy + vl.Y) / s)
    ///
    /// 패닝 델타(ΔViewportLocation)는 scale과 무관하다:
    ///   WorldUnderCursor = (sx + VL) / s  →  ΔVL = −ΔscreenPointer  (s가 약분됨)
    ///
    /// ─── VCA (VirtualCanvas-Avalonia) 매핑 ───────────────────────────────────────
    ///   DagEdit 개념              VCA 개념
    ///   ─────────────────────     ──────────────────────────────────────────────────
    ///   ViewportLocation (vl)  ≡  VirtualCanvas.Offset (Point)
    ///   ViewportScale    (s)   ≡  VirtualCanvas.Scale  (double)
    ///   ScreenToWorld(pt,vl,s) ≡  (pt + Offset) / Scale
    ///   WorldToScreen(pt,vl,s) ≡  pt * Scale − Offset
    ///   ─ 수식이 완전히 일치한다 ─
    ///
    ///   VCA.ActualViewbox = VCRect(Offset.X/Scale, Offset.Y/Scale, W/Scale, H/Scale)
    ///   DagEdit 등가: ScreenToWorld(Point(0,0), vl, s) = (vl.X/s, vl.Y/s) — 뷰포트 월드 원점
    ///</summary>
    public static class ViewportTransform
    {
        /// <summary>
        /// DagEditor 기준 스크린 좌표를 캔버스 월드 좌표로 변환한다.
        /// </summary>
        /// <param name="screen">DagEditor 로컬 스크린 좌표.</param>
        /// <param name="viewportLocation">현재 ViewportLocation (월드 기준).</param>
        /// <param name="scale">현재 ViewportScale.</param>
        /// <returns>월드 좌표.</returns>
        public static Point ScreenToWorld(Point screen, Point viewportLocation, double scale)
            => new(
                (screen.X + viewportLocation.X) / scale,
                (screen.Y + viewportLocation.Y) / scale);

        /// <summary>
        /// 캔버스 월드 좌표를 DagEditor 기준 스크린 좌표로 변환한다.
        /// </summary>
        /// <param name="world">월드 좌표.</param>
        /// <param name="viewportLocation">현재 ViewportLocation (월드 기준).</param>
        /// <param name="scale">현재 ViewportScale.</param>
        /// <returns>DagEditor 로컬 스크린 좌표.</returns>
        public static Point WorldToScreen(Point world, Point viewportLocation, double scale)
            => new(
                (world.X * scale) - viewportLocation.X,
                (world.Y * scale) - viewportLocation.Y);
    }
}
