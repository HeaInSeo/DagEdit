# Progress Log

> 실시간 작업 기록. 각 단계의 진행 상황, 문제, 해결 방안을 기록한다.

---

## 로드맵 (초기)

| # | 단계 | 상태 | 목표 |
|---|------|------|------|
| 0 | 문서 초기화 | ✅ 완료 | `docs/PROGRESS_LOG.md`, `docs/DECISION_LOG.md` 생성 |
| 1 | 정적 분석 설정 | ✅ 완료 | `.editorconfig` + StyleCop Analyzer 추가 |
| 2 | 테스트 프로젝트 | ✅ 완료 | xUnit + Avalonia.Headless + Cobertura 커버리지 |
| 3 | 벤치마크 프로젝트 | ✅ 완료 | BenchmarkDotNet + DAG 샘플 벤치마크 |
| 4 | 솔루션 업데이트 | ✅ 완료 | DagEdit.sln에 신규 프로젝트 등록 |
| 5 | CI/CD 워크플로우 | ✅ 완료 | GitHub Actions `verify.yml` 생성 |

---

## 단계별 기록

### [Step 0] 문서 초기화
- **날짜**: 2026-03-02
- **수행 내용**: `docs/` 디렉토리 생성, `PROGRESS_LOG.md`, `DECISION_LOG.md` 초기화
- **검증 지표**: 파일 2개 생성 완료
- **다음 단계**: Step 1 — 정적 분석 설정

---

### [Step 1] 정적 분석 설정
- **날짜**: 2026-03-02
- **수행 내용**:
  - `.editorconfig` 생성 (C# 스타일 규칙 + StyleCop severity=warning)
  - `DagEdit.csproj`에 `EnforceCodeStyleInBuild`, `AnalysisLevel`, `StyleCop.Analyzers 1.2.0-beta.507` 추가
- **검증 지표**: `dotnet build` 실행 시 SA* 경고 수 측정 가능
- **결정**: 기존 코드 위반 사항 존재로 severity=warning 유지 (error로 즉시 격상 시 빌드 중단). 목표: 경고 수 점진적 감소 → 0건 도달
- **다음 단계**: Step 2 — 테스트 프로젝트

---

### [Step 2] 테스트 프로젝트 (xUnit + Avalonia.Headless)
- **날짜**: 2026-03-02
- **수행 내용**:
  - `tests/DagEdit.Tests/DagEdit.Tests.csproj` 생성
  - `TestApp.cs`: Avalonia.Headless 부트스트랩 (향후 UI 테스트용)
  - `DagTests.cs`: `Dag` 클래스 9개 테스트 케이스
  - `DagItemsTests.cs`: `DagItems`, `DagNode`, `DagConnection` 8개 테스트 케이스
- **검증 지표**:
  - 총 테스트: 17개
  - 예상 통과: 17개 (100%)
  - 커버리지 형식: Cobertura XML
- **기술적 판단**: `DagItems.CreateDagNode()`는 `Node` UI 컨트롤을 생성하지 않음 → 현재 테스트는 `[Fact]` 사용 가능. `[AvaloniaFact]`는 향후 UI 테스트를 위해 인프라 구성만 완료
- **다음 단계**: Step 3 — 벤치마크

---

### [Step 3] 벤치마크 프로젝트 (BenchmarkDotNet)
- **날짜**: 2026-03-02
- **수행 내용**:
  - `benchmarks/DagEdit.Benchmarks/DagEdit.Benchmarks.csproj` 생성
  - `DagBenchmarks.cs`: 3개 벤치마크 (AddNodes, AddConnections, LargeGraphOperations)
  - `[MemoryDiagnoser]`로 메모리 할당량 측정
  - CI 드라이런: `--job Dry --filter "*"` (실제 측정 없이 컴파일/실행 검증)
- **검증 지표**: 드라이런 종료 코드 0
- **다음 단계**: Step 4 — 솔루션 업데이트

---

### [Step 4] 솔루션 업데이트
- **날짜**: 2026-03-02
- **수행 내용**: `DagEdit.sln`에 Tests, Benchmarks 프로젝트 등록
- **검증 지표**: `dotnet sln list` 결과 3개 프로젝트 확인
- **다음 단계**: Step 5 — CI/CD

---

### [Step 5] GitHub Actions CI/CD
- **날짜**: 2026-03-02
- **수행 내용**:
  - `.github/workflows/verify.yml` 생성
  - 트리거: `master` 브랜치 push/PR
  - 리포트 항목: 테스트 통과율, 커버리지 %, 분석기 경고 수, 벤치마크 드라이런 상태
- **검증 지표**: 워크플로우 파일 유효성 확인
- **다음 단계**: 없음 (초기 인프라 구축 완료)

---

## 향후 과제

| 우선순위 | 내용 |
|---------|------|
| High | StyleCop 경고 0건 달성 (severity warning → error 격상) |
| High | `DelDagNodeItem` 해피패스 테스트 (Node 인스턴스 분리 필요) |
| Medium | 커버리지 목표 설정 (목표: 80% 이상) |
| Medium | 벤치마크 기준선(baseline) 저장 및 회귀 감지 |
| Low | Zoom 기능 구현 후 해당 테스트 추가 |
| Low | Connection 삭제 기능 구현 후 테스트 추가 |
