namespace DagEdit.Tests;

using Xunit;

/// <summary>
/// <see cref="Extension.PickClosestCandidate{T}"/> 단위 테스트.
///
/// 테스트 설계 원칙:
/// - PickClosestCandidate 는 (Item, DistanceSq) 쌍의 리스트에서 최솟값을 선택하는 pure function.
/// - Avalonia 컨트롤/비주얼 트리에 의존하지 않으므로 [Fact]로 실행 가능.
/// - string 을 Item 타입으로 사용하여 Avalonia 초기화 없이 검증.
/// </summary>
public class ConnectorSnapTests
{
    // ─── 빈 목록 ─────────────────────────────────────────────────

    [Fact]
    public void PickClosestCandidate_EmptyList_ReturnsNull()
    {
        var candidates = new List<(string Item, double DistanceSq)>();

        var result = Extension.PickClosestCandidate<string>(candidates);

        Assert.Null(result);
    }

    // ─── 단일 후보 ────────────────────────────────────────────────

    [Fact]
    public void PickClosestCandidate_SingleCandidate_ReturnsThat()
    {
        var candidates = new List<(string Item, double DistanceSq)>
        {
            ("only", 999.0)
        };

        var result = Extension.PickClosestCandidate<string>(candidates);

        Assert.Equal("only", result);
    }

    // ─── 복수 후보 — 최단 거리 선택 ──────────────────────────────

    [Fact]
    public void PickClosestCandidate_ReturnsItemWithSmallestDistanceSq()
    {
        var candidates = new List<(string Item, double DistanceSq)>
        {
            ("far",    100.0),
            ("close",    4.0),
            ("medium",  25.0)
        };

        var result = Extension.PickClosestCandidate<string>(candidates);

        Assert.Equal("close", result);
    }

    [Fact]
    public void PickClosestCandidate_LastItemIsClosest_ReturnsLast()
    {
        var candidates = new List<(string Item, double DistanceSq)>
        {
            ("far1", 200.0),
            ("far2", 150.0),
            ("closest",  1.0)
        };

        var result = Extension.PickClosestCandidate<string>(candidates);

        Assert.Equal("closest", result);
    }

    [Fact]
    public void PickClosestCandidate_ZeroDistanceSq_ReturnsThat()
    {
        var candidates = new List<(string Item, double DistanceSq)>
        {
            ("onCenter",  0.0),
            ("offCenter", 50.0)
        };

        var result = Extension.PickClosestCandidate<string>(candidates);

        Assert.Equal("onCenter", result);
    }

    // ─── 동점(tie-break): tree 순서(first) 우선 ──────────────────

    [Fact]
    public void PickClosestCandidate_TiedDistances_FirstInListWins()
    {
        var candidates = new List<(string Item, double DistanceSq)>
        {
            ("first",  10.0),
            ("second", 10.0),
            ("third",  10.0)
        };

        var result = Extension.PickClosestCandidate<string>(candidates);

        Assert.Equal("first", result);
    }
}
