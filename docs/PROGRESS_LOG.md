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

---

### [Step 6] 성능 지속적 감시 체계 구축
- **날짜**: 2026-03-03
- **수행 내용**:
  - `scripts/compare_benchmarks.py`: BDN JSON 비교 스크립트 (소수점 2자리, 10% 임계값)
  - `scripts/visualize_performance.py`: matplotlib 성능 추이 PNG 생성
  - `benchmarks/history/`: GitHub Cache 기반 날짜별 JSON 히스토리 누적
  - `benchmarks/DagEdit.Benchmarks/Program.cs`: `JsonExporter.Full` 추가
  - `verify.yml` 벤치마크 섹션 재설계: Dry Run → Short Job + 회귀 가드 + 히스토리
- **검증 지표**:
  - `compare_benchmarks.py` 단위 기능 검증 완료
  - `verify.yml` 구문 유효성 확인
- **문제/해결**: `grep -c ... || echo 0` 패턴이 GITHUB_OUTPUT에 개행 포함 "0\n0" 출력 → `grep | wc -l` 로 수정

---

### [Step 7] Go-like 무타협 린트 환경 구축
- **날짜**: 2026-03-03
- **수행 내용**:
  - `.editorconfig`에 핵심 규칙 Error 격상 (SA1503, CS8600-8625, SA1400, IDE0059/0060 등)
  - 기존 SA1503 위반 코드 전 파일 수정 (BaseNode, Connection, Dag, DagEditor, Extension, Node, PointerGesture, SourceConnector)
- **검증 지표**: 빌드 0 Error, 295 Warning, 32/32 테스트 통과
- **결정 참조**: DEC-005 (Error 격상 규칙 목록)

---

### [Step 8] CodeQL 보안 분석 CI 연동
- **날짜**: 2026-03-03
- **수행 내용**:
  - `verify.yml`에 `codeql` job 추가 (별도 job으로 격리)
  - `security-extended` 쿼리 팩 설정
  - GitHub Security 탭에 SARIF 자동 업로드
- **검증 지표**: CodeQL job CI 실행 성공 (2m15s)
- **결정 참조**: DEC-011

---

### [Step 9] ReactiveUI WhenAnyValue 도입 (첫 번째 Rx 작업)
- **날짜**: 2026-03-03
- **수행 내용**:
  - `NodeDragState.cs` 신규 생성 (`ReactiveObject`, `Position` 프로퍼티)
  - `Node.cs` 리팩토링:
    - `_disposable: IDisposable` → `_disposables: CompositeDisposable`
    - `HandlePointerMoved`: 그리드 스냅 + `_dragState.Position` 설정으로 단순화
    - `WhenAnyValue(x => x.Position).Skip(1).DistinctUntilChanged()` 체인으로 앵커/이벤트 처리
  - `docs/rx-patterns/node-drag-reactive.md`: [What it does], [Go Analogy], [Operator Breakdown] 형식 설명서
- **검증 지표**: 32/32 테스트 통과 (기존 회귀 없음)
- **결정 참조**: DEC-010
- **다음 Rx 작업**: PendingConnection 드래그, DagEditorViewModel, ReactiveList

---

### [Step 10] PendingConnection.cs 리팩토링 (ReactiveUI 전환 + AXAML 완전 제거)
- **날짜**: 2026-03-04
- **수행 내용**:
  - `PendingConnectionState.cs` 신규 생성: `ReactiveObject` 기반, `SourceAnchor`/`TargetAnchor` 프로퍼티 (`NodeDragState` 패턴 적용)
  - `PendingConnection.cs` 완전 재작성:
    - B-1 수정: `IDisposable _disposable` 두 번 덮어쓰기 → `CompositeDisposable _disposables`
    - B-2 수정: `ViewportLocationProperty.Register<DagEditorCanvas, Point>` → `Register<PendingConnection, Point>`
    - B-3 수정: dead code `SetFillAndStrokePropertyChanged` 제거 (sender 타입 오판)
    - static constructor: 모든 default 값 이관 (IsHitTestVisible, EnablePreview, EnableSnapping, StrokeThickness, Direction)
    - `BuildTemplate()`: `FuncControlTemplate<PendingConnection>` — TemplateLayoutCanvas + Connection
    - 생성자: `GetObservable(SourceAnchorProperty)` → `_state.SourceAnchor`, ViewportLocation → TranslateTransform, SetFillAndStroke → `_partConnection`
    - `OnApplyTemplate`: PART_Connection 참조 획득 + `_state.WhenAnyValue.Skip(1).DistinctUntilChanged()` 체인
  - `PendingConnection.axaml` 삭제 (C#으로 완전 이관)
  - `Styles.axaml`: `<StyleInclude Source="/PendingConnection.axaml" />` 제거
  - `DagEdit.csproj`: `<Compile Remove="VirtualCanvas_ref/**/*.cs" />` 추가 (WPF 의존 코드 빌드 제외)
- **검증 지표**:
  - 빌드: **0 Error, 295 Warning** (기존 동일, 신규 에러/경고 없음)
  - 테스트: **32/32 통과** (기존 회귀 없음)
  - 벤치마크: **BDN dry-run 24 benchmarks 성공** (exit code 0, 4m39s)
- **결정 참조**: DEC-012 (AXAML 제거 + ReactiveUI), DEC-013 (VirtualCanvas_ref 빌드 제외)
- **다음 Rx 작업**: DagEditorViewModel 분리, ReactiveList 도입

---

## 향후 과제

| 우선순위 | 내용 |
|---------|------|
| High | StyleCop 경고 0건 달성 (295 → 0, 전체 Error 격상 가능 시점) |
| High | `DelDagNodeItem` 해피패스 테스트 (Node 인스턴스 분리 필요) |
| Medium | 커버리지 목표 설정 (목표: 80% 이상, 현재 4%) |
| Medium | ~~ReactiveUI: PendingConnection 드래그 Observable.FromEventPattern 전환~~ ✅ Step 10 완료 |
| Medium | ReactiveUI: DagEditorViewModel 분리 |
| Low | Zoom 기능 구현 후 해당 테스트 추가 |
| Low | Connection 삭제 기능 구현 후 테스트 추가 |
| Low | `docs/performance_trend.png` 생성 (히스토리 3회 이상 누적 후) |
