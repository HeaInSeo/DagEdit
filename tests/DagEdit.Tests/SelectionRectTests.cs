namespace DagEdit.Tests;

using Avalonia;
using Xunit;

/// <summary>
/// Selection rectangle은 항상 월드 좌표로 계산된다.
/// 뷰포트 상태(pan / zoom / pan+zoom) 변화와 무관하게 동일한 노드 집합을 선택해야 한다는
/// 불변성을 검증한다. (<see cref="ViewportTransform"/> + <see cref="Rect.Intersects"/> 조합)
///
/// 핵심 원칙: <see cref="DagEditor.FinalizeSelection"/> 의 worldRect 계산은
/// 시각적 렌더링 상태와 완전히 독립적이다 — VCA ISpatialIndex.Query(worldRect) 로
/// 드롭인 교체 시 동일한 worldRect를 입력으로 사용한다.
/// </summary>
public class SelectionRectTests
{
    private static readonly Size NodeSize = new(Constants.NodeWidth, Constants.NodeHeight);

    /// <summary>화면 직사각형 두 모서리를 월드 좌표 Rect로 변환한다 (FinalizeSelection 동일 로직).</summary>
    private static Rect ScreenRectToWorld(Rect screen, Point vl, double scale)
    {
        var topLeft = ViewportTransform.ScreenToWorld(screen.TopLeft, vl, scale);
        var bottomRight = ViewportTransform.ScreenToWorld(screen.BottomRight, vl, scale);
        return new Rect(topLeft, bottomRight);
    }

    // ─── Identity viewport ────────────────────────────────────────────────

    [Fact]
    public void SelectionRect_Identity_IncludesNodeInsideRect()
    {
        var nodeRect = new Rect(new Point(100, 100), NodeSize);
        var screenRect = new Rect(50, 50, 300, 250);

        var worldRect = ScreenRectToWorld(screenRect, new Point(0, 0), 1.0);

        Assert.True(worldRect.Intersects(nodeRect));
    }

    [Fact]
    public void SelectionRect_Identity_ExcludesNodeOutsideRect()
    {
        var nodeRect = new Rect(new Point(500, 500), NodeSize);
        var screenRect = new Rect(0, 0, 200, 200);

        var worldRect = ScreenRectToWorld(screenRect, new Point(0, 0), 1.0);

        Assert.False(worldRect.Intersects(nodeRect));
    }

    // ─── Pan-only viewport ────────────────────────────────────────────────

    [Fact]
    public void SelectionRect_PanOnly_SelectsCorrectNode()
    {
        // 노드 월드 (100, 100). pan: vl=(50, 30), scale=1.
        // 노드 스크린 위치: WorldToScreen(100, 100) = (100-50, 100-30) = (50, 70).
        var nodeRect = new Rect(new Point(100, 100), NodeSize);
        var vl = new Point(50, 30);
        var screenRect = new Rect(20, 40, 400, 300);

        var worldRect = ScreenRectToWorld(screenRect, vl, 1.0);

        Assert.True(worldRect.Intersects(nodeRect));
    }

    [Fact]
    public void SelectionRect_PanOnly_ExcludesNodeScrolledAway()
    {
        // 노드 월드 (10, 10). 뷰포트가 (500, 500)으로 패닝 → 노드가 화면 밖.
        var nodeRect = new Rect(new Point(10, 10), NodeSize);
        var vl = new Point(500, 500);
        var screenRect = new Rect(0, 0, 300, 200);

        var worldRect = ScreenRectToWorld(screenRect, vl, 1.0);

        Assert.False(worldRect.Intersects(nodeRect));
    }

    // ─── Zoom-only viewport ───────────────────────────────────────────────

    [Fact]
    public void SelectionRect_ZoomOnly_SelectsCorrectNode()
    {
        // 노드 월드 (100, 100). zoom: vl=(0,0), scale=2.
        // 노드 스크린 위치: (200, 200).
        var nodeRect = new Rect(new Point(100, 100), NodeSize);
        var screenRect = new Rect(150, 150, 300, 250);

        var worldRect = ScreenRectToWorld(screenRect, new Point(0, 0), 2.0);

        Assert.True(worldRect.Intersects(nodeRect));
    }

    [Fact]
    public void SelectionRect_ZoomOut_ExpandsWorldCoverage()
    {
        // scale=0.5 (축소): 스크린 (0,0)~(200,200) → 월드 (0,0)~(400,400)
        var nodeRect = new Rect(new Point(350, 350), NodeSize);
        var screenRect = new Rect(0, 0, 200, 200);

        var worldRect = ScreenRectToWorld(screenRect, new Point(0, 0), 0.5);

        Assert.True(worldRect.Intersects(nodeRect));
    }

    // ─── Pan + Zoom viewport ──────────────────────────────────────────────

    [Fact]
    public void SelectionRect_PanAndZoom_SelectsCorrectNode()
    {
        // 노드 월드 (200, 150). vl=(100, 50), scale=2.
        // 노드 스크린: (200*2-100, 150*2-50) = (300, 250).
        var nodeRect = new Rect(new Point(200, 150), NodeSize);
        var vl = new Point(100, 50);
        var screenRect = new Rect(250, 200, 400, 400);

        var worldRect = ScreenRectToWorld(screenRect, vl, 2.0);

        Assert.True(worldRect.Intersects(nodeRect));
    }

    [Fact]
    public void SelectionRect_PanAndZoom_ExcludesDistantNode()
    {
        var nodeRect = new Rect(new Point(1000, 1000), NodeSize);
        var vl = new Point(100, 50);
        var screenRect = new Rect(0, 0, 200, 200);

        var worldRect = ScreenRectToWorld(screenRect, vl, 2.0);

        Assert.False(worldRect.Intersects(nodeRect));
    }

    // ─── Viewport 불변성: 동일 월드 선택 rect, 다른 viewport 상태 ─────────

    [Fact]
    public void SelectionRect_SameWorldRect_EquivalentAcrossViewportStates()
    {
        // 동일한 월드 선택 영역을 서로 다른 viewport 상태에서 스크린으로 변환하면
        // 역변환 시 동일한 worldRect가 나와야 한다.
        var nodeRect = new Rect(new Point(100, 100), NodeSize);
        var worldSelection = new Rect(80, 80, 250, 200); // 노드 포함

        // viewport A: identity
        var vlA = new Point(0, 0);
        const double sA = 1.0;
        var screenA = new Rect(
            ViewportTransform.WorldToScreen(worldSelection.TopLeft, vlA, sA),
            ViewportTransform.WorldToScreen(worldSelection.BottomRight, vlA, sA));
        var worldA = ScreenRectToWorld(screenA, vlA, sA);

        // viewport B: panned + zoomed
        var vlB = new Point(150, 100);
        const double sB = 3.0;
        var screenB = new Rect(
            ViewportTransform.WorldToScreen(worldSelection.TopLeft, vlB, sB),
            ViewportTransform.WorldToScreen(worldSelection.BottomRight, vlB, sB));
        var worldB = ScreenRectToWorld(screenB, vlB, sB);

        Assert.True(worldA.Intersects(nodeRect));
        Assert.True(worldB.Intersects(nodeRect));
        Assert.Equal(worldA.X, worldB.X, precision: 5);
        Assert.Equal(worldA.Y, worldB.Y, precision: 5);
        Assert.Equal(worldA.Width, worldB.Width, precision: 5);
        Assert.Equal(worldA.Height, worldB.Height, precision: 5);
    }

    // ─── VCA 공식 일치 검증 ───────────────────────────────────────────────

    [Fact]
    public void TransformFormula_MatchesVcaFormula()
    {
        // VCA: screen = world * Scale - Offset  (Offset ≡ ViewportLocation)
        // DagEdit: WorldToScreen(w, vl, s) = (w.X * s - vl.X, w.Y * s - vl.Y)
        // 수식이 동일함을 검증한다.
        var vl = new Point(120, 80);
        const double scale = 2.5;
        var world = new Point(300, 200);

        var dagScreen = ViewportTransform.WorldToScreen(world, vl, scale);

        // VCA 수식 직접 계산
        var vcaX = world.X * scale - vl.X;
        var vcaY = world.Y * scale - vl.Y;

        Assert.Equal(vcaX, dagScreen.X, precision: 6);
        Assert.Equal(vcaY, dagScreen.Y, precision: 6);
    }

    [Fact]
    public void InverseFormula_MatchesVcaActualViewboxOrigin()
    {
        // VCA.ActualViewbox.TopLeft = (Offset.X / Scale, Offset.Y / Scale)
        // DagEdit: ScreenToWorld(Point(0,0), vl, s) = (vl.X / s, vl.Y / s) — 동일
        var vl = new Point(120, 80);
        const double scale = 2.5;

        var dagWorld = ViewportTransform.ScreenToWorld(new Point(0, 0), vl, scale);

        var vcaViewboxX = vl.X / scale;
        var vcaViewboxY = vl.Y / scale;

        Assert.Equal(vcaViewboxX, dagWorld.X, precision: 6);
        Assert.Equal(vcaViewboxY, dagWorld.Y, precision: 6);
    }
}
