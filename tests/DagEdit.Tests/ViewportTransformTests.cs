namespace DagEdit.Tests;

using Avalonia;
using Xunit;

/// <summary>
/// <see cref="ViewportTransform"/> 좌표 변환 유틸리티의 단위 테스트.
///
/// 검증 계약:
///   스크린 점 (sx, sy) → 월드: ((sx + vl.X) / scale, (sy + vl.Y) / scale)
///   월드 점  (wx, wy) → 스크린: (wx * scale − vl.X, wy * scale − vl.Y)
///
/// 패닝 델타가 scale-independent임을 수식으로 증명:
///   w = (sx + VL) / s  →  ΔVL = −Δsceen  (s 소거)
/// </summary>
public class ViewportTransformTests
{
    // ─── ScreenToWorld ────────────────────────────────────────────

    [Fact]
    public void ScreenToWorld_Identity_ReturnsSamePoint()
    {
        // scale=1, vl=0 → 항등 변환
        var screen = new Point(100, 200);

        var world = ViewportTransform.ScreenToWorld(screen, new Point(0, 0), 1.0);

        Assert.Equal(100.0, world.X, precision: 6);
        Assert.Equal(200.0, world.Y, precision: 6);
    }

    [Fact]
    public void ScreenToWorld_PanOnly_ShiftsCorrectly()
    {
        // scale=1, vl=(50, 30) → world = screen + vl
        var world = ViewportTransform.ScreenToWorld(new Point(100, 100), new Point(50, 30), 1.0);

        Assert.Equal(150.0, world.X, precision: 6);
        Assert.Equal(130.0, world.Y, precision: 6);
    }

    [Fact]
    public void ScreenToWorld_ZoomOnly_DividesCorrectly()
    {
        // scale=2, vl=0 → world = screen / 2
        var world = ViewportTransform.ScreenToWorld(new Point(200, 100), new Point(0, 0), 2.0);

        Assert.Equal(100.0, world.X, precision: 6);
        Assert.Equal(50.0, world.Y, precision: 6);
    }

    [Fact]
    public void ScreenToWorld_PanAndZoom_CombinesCorrectly()
    {
        // scale=2, vl=(100, 0), screen=(200, 100)
        // world = (200 + 100) / 2, (100 + 0) / 2 = (150, 50)
        var world = ViewportTransform.ScreenToWorld(new Point(200, 100), new Point(100, 0), 2.0);

        Assert.Equal(150.0, world.X, precision: 6);
        Assert.Equal(50.0, world.Y, precision: 6);
    }

    // ─── WorldToScreen ────────────────────────────────────────────

    [Fact]
    public void WorldToScreen_Identity_ReturnsSamePoint()
    {
        // scale=1, vl=0 → 항등 변환
        var screen = ViewportTransform.WorldToScreen(new Point(100, 200), new Point(0, 0), 1.0);

        Assert.Equal(100.0, screen.X, precision: 6);
        Assert.Equal(200.0, screen.Y, precision: 6);
    }

    [Fact]
    public void WorldToScreen_PanOnly_ShiftsCorrectly()
    {
        // scale=1, vl=(50, 30) → screen = world - vl
        var screen = ViewportTransform.WorldToScreen(new Point(150, 130), new Point(50, 30), 1.0);

        Assert.Equal(100.0, screen.X, precision: 6);
        Assert.Equal(100.0, screen.Y, precision: 6);
    }

    [Fact]
    public void WorldToScreen_ZoomOnly_MultipliesCorrectly()
    {
        // scale=2, vl=0 → screen = world * 2
        var screen = ViewportTransform.WorldToScreen(new Point(100, 50), new Point(0, 0), 2.0);

        Assert.Equal(200.0, screen.X, precision: 6);
        Assert.Equal(100.0, screen.Y, precision: 6);
    }

    [Fact]
    public void WorldToScreen_PanAndZoom_CombinesCorrectly()
    {
        // scale=2, vl=(100, 0), world=(150, 50)
        // screen = (150 * 2 - 100, 50 * 2 - 0) = (200, 100)
        var screen = ViewportTransform.WorldToScreen(new Point(150, 50), new Point(100, 0), 2.0);

        Assert.Equal(200.0, screen.X, precision: 6);
        Assert.Equal(100.0, screen.Y, precision: 6);
    }

    // ─── 역변환 일관성 (RoundTrip) ────────────────────────────────

    [Fact]
    public void RoundTrip_ScreenToWorldToScreen_IsIdentity()
    {
        // ScreenToWorld 후 WorldToScreen = 원래 스크린 좌표
        var vl = new Point(75, 30);
        const double scale = 1.5;
        var original = new Point(300, 200);

        var world = ViewportTransform.ScreenToWorld(original, vl, scale);
        var back = ViewportTransform.WorldToScreen(world, vl, scale);

        Assert.Equal(original.X, back.X, precision: 5);
        Assert.Equal(original.Y, back.Y, precision: 5);
    }

    [Fact]
    public void RoundTrip_WorldToScreenToWorld_IsIdentity()
    {
        // WorldToScreen 후 ScreenToWorld = 원래 월드 좌표
        var vl = new Point(100, 50);
        const double scale = 3.0;
        var original = new Point(200, 100);

        var screen = ViewportTransform.WorldToScreen(original, vl, scale);
        var back = ViewportTransform.ScreenToWorld(screen, vl, scale);

        Assert.Equal(original.X, back.X, precision: 5);
        Assert.Equal(original.Y, back.Y, precision: 5);
    }

    // ─── 패닝 델타의 scale 독립성 ─────────────────────────────────

    [Fact]
    public void PanDelta_IsScaleIndependent()
    {
        // 마우스가 (px1) → (px2)로 이동했을 때
        // 커서 아래 월드 점을 고정하려면 ΔVL = −Δscreen (scale 무관)
        // 검증: scale=1과 scale=2에서 동일한 ΔVL이 나와야 한다.
        var vl = new Point(200, 100);
        var px1 = new Point(400, 300);
        var px2 = new Point(450, 350);
        var dScreen = px2 - px1; // (50, 50)

        // scale=1 에서 커서 아래 월드 점
        var w1 = ViewportTransform.ScreenToWorld(px1, vl, 1.0);
        // 패닝 후 새 VL (ΔVL = -dScreen)
        var newVl1 = new Point(vl.X - dScreen.X, vl.Y - dScreen.Y);
        // 새 VL에서 px2의 월드 좌표
        var w1After = ViewportTransform.ScreenToWorld(px2, newVl1, 1.0);

        // scale=2 에서 동일한 패닝 적용
        var w2 = ViewportTransform.ScreenToWorld(px1, vl, 2.0);
        var newVl2 = new Point(vl.X - dScreen.X, vl.Y - dScreen.Y);
        var w2After = ViewportTransform.ScreenToWorld(px2, newVl2, 2.0);

        // 커서 아래 월드 점이 이동 전후로 보존되어야 함 (scale=1)
        Assert.Equal(w1.X, w1After.X, precision: 5);
        Assert.Equal(w1.Y, w1After.Y, precision: 5);
        // scale=2에서도 동일
        Assert.Equal(w2.X, w2After.X, precision: 5);
        Assert.Equal(w2.Y, w2After.Y, precision: 5);
    }

    // ─── 줌 피벗 공식 검증 ───────────────────────────────────────

    [Fact]
    public void ZoomPivot_WorldUnderCursorIsPreserved()
    {
        // DagEditor.HandlePointerWheelChanged 의 공식 검증:
        // w = ScreenToWorld(cursor, vl1, s1)
        // newVL = (w.X * s2 − cursor.X, ...)
        // → ScreenToWorld(cursor, newVL, s2) = w  (줌 피벗 보존)
        var vl1 = new Point(100, 0);
        const double s1 = 1.0;
        const double s2 = 2.0;
        var cursor = new Point(200, 100);

        var w = ViewportTransform.ScreenToWorld(cursor, vl1, s1);
        var newVl = new Point(w.X * s2 - cursor.X, w.Y * s2 - cursor.Y);
        var wAfter = ViewportTransform.ScreenToWorld(cursor, newVl, s2);

        Assert.Equal(w.X, wAfter.X, precision: 5);
        Assert.Equal(w.Y, wAfter.Y, precision: 5);
    }
}
