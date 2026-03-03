using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;
using DagEdit.Benchmarks;

// Release 모드 빌드 확인
#if !RELEASE
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("[경고] BenchmarkDotNet은 Release 모드에서 실행해야 의미 있는 결과를 얻을 수 있습니다.");
Console.WriteLine("       실행 명령: dotnet run -c Release -- [옵션]");
Console.ResetColor();
Console.WriteLine();
#endif

// ─── ManualConfig: JsonExporter.Full 추가 ──────────────────────────────────
//
// JsonExporter.Full: 개별 측정값(Measurements)과 전체 통계를 JSON으로 기록한다.
// - 출력 경로: --artifacts <path>/results/*.json (CLI 인자로 제어)
// - 기본 경로: BenchmarkDotNet.Artifacts/results/ (로컬 실행 시)
//
// 사용 가능한 CLI 옵션:
//   --job Dry        : CI 드라이런 (컴파일 및 실행 검증만)
//   --job Short      : 빠른 실측 (CI 회귀 비교용)
//   --filter "*"     : 모든 벤치마크 실행
//   --filter "*Add*" : 패턴 매칭으로 특정 벤치마크만 실행
//   --artifacts <path>: 결과 저장 경로 지정
//   --list flat      : 사용 가능한 벤치마크 목록 출력
var config = ManualConfig
    .Create(DefaultConfig.Instance)
    .AddExporter(JsonExporter.Full);

BenchmarkSwitcher.FromAssembly(typeof(DagBenchmarks).Assembly).Run(args, config);
