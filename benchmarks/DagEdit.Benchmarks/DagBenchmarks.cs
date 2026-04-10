namespace DagEdit.Benchmarks;

using Avalonia;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

/// <summary>
/// DAG 모델 레이어 성능 벤치마크.
///
/// 측정 대상:
/// - 노드 추가 처리량 (AddDagNodeItem)
/// - 커넥션 추가 처리량 (AddDagConnectionItem)
/// - 대규모 그래프 생성 + 조회
///
/// CI 실행: dotnet run -c Release -- --job Dry --filter "*"
///   → 각 벤치마크를 1회 실행하여 컴파일/런타임 오류만 확인 (측정값 미사용)
///
/// 로컬 전체 실행: dotnet run -c Release -- --filter "*"
///   → 통계적으로 신뢰 가능한 성능 수치 생성
/// </summary>
[MemoryDiagnoser]                          // GC 할당량 및 컬렉션 횟수 측정
[SimpleJob(RuntimeMoniker.Net80)]          // .NET 8.0 런타임 고정
[MinColumn, MaxColumn, MeanColumn, MedianColumn] // 통계 열 추가
public class DagBenchmarks
{
    // ─── 벤치마크 파라미터 ─────────────────────────────────────

    /// <summary>벤치마크에서 반복 추가할 노드 수.</summary>
    [Params(10, 100, 1000)]
    public int NodeCount { get; set; }

    // ─── 노드 추가 벤치마크 ───────────────────────────────────

    /// <summary>
    /// 노드를 순차적으로 추가하는 성능을 측정한다.
    /// 각 반복마다 새로운 Dag 인스턴스를 생성하므로 격리된 측정이 가능하다.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Add N nodes sequentially")]
    public void AddNodes_Sequential()
    {
        var dag = new Dag();

        for (int i = 0; i < NodeCount; i++)
        {
            dag.AddDagNodeItem(new Point(i * 10.0, i * 10.0));
        }
    }

    // ─── 커넥션 추가 벤치마크 ────────────────────────────────

    /// <summary>
    /// 커넥션을 순차적으로 추가하는 성능을 측정한다.
    /// NodeCount 수만큼의 커넥션을 추가한다.
    /// </summary>
    [Benchmark(Description = "Add N connections sequentially")]
    public void AddConnections_Sequential()
    {
        var dag = new Dag();
        var source = new Point(0.0, 0.0);
        var target = new Point(200.0, 62.0); // Node 기본 크기 (200x124) 중심점

        for (int i = 0; i < NodeCount; i++)
        {
            dag.AddDagConnectionItem(
                source, Guid.NewGuid(),
                target, Guid.NewGuid());
        }
    }

    // ─── 대규모 그래프 생성 벤치마크 ─────────────────────────

    /// <summary>
    /// 노드와 커넥션을 혼합하여 추가한 후 DAGItemsSource를 순회하는 성능을 측정한다.
    /// 실제 워크플로우 시나리오에 가장 유사한 벤치마크.
    /// </summary>
    [Benchmark(Description = "Build graph and iterate all items")]
    public int BuildAndIterateGraph()
    {
        var dag = new Dag();

        // 노드 추가
        for (int i = 0; i < NodeCount; i++)
        {
            dag.AddDagNodeItem(new Point(i * 210.0, (i % 5) * 130.0));
        }

        // 커넥션 추가 (노드 수의 절반)
        int connectionCount = NodeCount / 2;
        for (int i = 0; i < connectionCount; i++)
        {
            dag.AddDagConnectionItem(
                new Point(i * 210.0 + 200.0, (i % 5) * 130.0 + 62.0),
                Guid.NewGuid(),
                new Point((i + 1) * 210.0, ((i + 1) % 5) * 130.0 + 62.0),
                Guid.NewGuid());
        }

        // 전체 순회 (순회 자체의 성능도 측정)
        int count = 0;
        foreach (var item in dag.DAGItemsSource)
        {
            if (item.NodeItem != null || item.ConnectionItem != null)
            {
                count++;
            }
        }

        return count; // 인라인 최적화 방지용 반환값
    }

    // ─── LINQ 조회 벤치마크 ───────────────────────────────────

    /// <summary>
    /// LINQ로 특정 NodeId를 검색하는 성능을 측정한다.
    /// DelDagNodeItem 내부의 FirstOrDefault 패턴과 동일한 조회 방식.
    /// </summary>
    [Benchmark(Description = "LINQ search for node by ID")]
    public bool FindNode_ByGuid()
    {
        var dag = new Dag();

        // NodeCount개 노드 추가
        for (int i = 0; i < NodeCount; i++)
        {
            dag.AddDagNodeItem(new Point(i * 10.0, i * 10.0));
        }

        // 마지막으로 추가된 노드의 ID (최악의 경우 검색)
        var lastNodeId = dag.DAGItemsSource[dag.DAGItemsSource.Count - 1].NodeItem?.NodeId;

        return dag.DAGItemsSource
            .FirstOrDefault(i => i.NodeItem != null && i.NodeItem.NodeId == lastNodeId)
            ?.NodeItem != null;
    }
}
