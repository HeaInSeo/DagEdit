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

### [Step 11] DagEditorViewModel 분리 + DynamicData SourceList 전환
- **날짜**: 2026-03-05
- **수행 내용**:
  - `DagEditorViewModel.cs` 신규: `sealed ReactiveObject`, Dag 소유, Items 노출
  - `Dag.cs`: `AvaloniaList<DagItems>` → `DynamicData.SourceList<DagItems>` 전환, `IDisposable` 구현
  - `DagEditor.cs`: `DataContext = Dag` → `DataContext = DagEditorViewModel`
  - `DagEditor.axaml`: `x:DataType=DagEditorViewModel`, `Binding Items`
  - Dag 생성자 seed data 제거 (빈 상태로 시작)
  - `BaseNode`: `Unloaded += Dispose()` 자기 해제 등록
  - `DelDagNodeItem`: `.Hide()/.Dispose(true)` 직접 호출 제거 → SourceList.Remove() → Unloaded 경로
- **검증 지표**: 32/32 테스트 통과
- **결정 참조**: DEC-014 (SourceAnchor/TargetAnchor Point?→Point 계약 변경)

---

### [Step 12] PendingConnection semantics 복원 + Point? 타입 불일치 해소
- **날짜**: 2026-03-05
- **수행 내용**:
  - `DagEditor.SourceAnchor`/`TargetAnchor`: `Point?` → `Point` (nullable 제거)
  - "연결 없음" 신호를 `IsVisiblePendingConnection = false`로 명확히 분리
  - `PendingConnection`: `Skip(1)` 제거 (첫 프레임 anchor 초기화 보장)
  - `_templateDisposables` 도입 (OnApplyTemplate 재호출 시 구독 누적 방지)
- **검증 지표**: 빌드 0 Error, 32/32 테스트 통과

---

### [Step 13] Connection 삭제 구현
- **날짜**: 2026-03-05
- **수행 내용**:
  - `Dag.AddDagConnectionItem`: Connection 추가 시 SourceNode.SourceConnections / TargetNode.TargetConnections 등록 (연결 추적)
  - `Dag.DelDagConnectionItem`: ConnectionId로 Connection 단독 삭제
  - `Dag.DelDagNodeItem`: 노드 삭제 전 연결된 모든 Connection 자동 삭제
  - `DagEditorViewModel.DelDagConnectionItem`: 위임 메서드 추가
  - `Connection`: `ConnectionId` 프로퍼티, `Focusable=true`, `OnPointerPressed → Focus()`
  - `DagEditor.CreateContainerForItemOverride`: ConnectionId 모델→UI 전파
  - `DagEditor.HandleKeyDown`: Connection 클릭 후 Delete 키로 삭제 분기 추가
- **검증 지표**: 빌드 0 Error, 32/32 테스트 통과

---

### [Step 14] PendingConnection static ctor 크래시 수정
- **날짜**: 2026-03-05
- **수행 내용**:
  - 원인: `Register<PendingConnection, T>` 선언 시 PendingConnection metadata가 즉시 등록되므로, static ctor에서 `OverrideDefaultValue<PendingConnection>` 재호출 시 `TypeInitializationException` 발생
  - `EnablePreviewProperty.OverrideDefaultValue<PendingConnection>(false)` 제거 (bool 기본값과 동일)
  - `EnableSnappingProperty.OverrideDefaultValue<PendingConnection>(true)` 제거 → `Register` 호출부에서 `defaultValue: true` 지정
  - `AddOwner<PendingConnection>()` 방식(StrokeThickness, Direction)은 metadata를 별도 등록하지 않으므로 유지
- **검증 지표**: 빌드 0 Error, 32/32 테스트 통과, startup `TypeInitializationException` 제거 확인

---

### [Step 15] 메뉴 텍스트 정상화
- **날짜**: 2026-03-05
- **수행 내용**:
  - `EditorContextFlyout.cs`: 임시 한글 텍스트("바보", "멍충이") → 영어로 교체
  - `"Node(_N)"`, `"Add Node(_A)"`, `"Open(_O)"`
- **검증 지표**: 빌드 0 Error

---

### [Step 16] 좌표계 기준선 고정 + zoom 상태 연결 드래그 정상화
- **날짜**: 2026-03-06
- **수행 내용**:
  - `ViewportTransform.cs` 신규 추가: `ScreenToWorld` / `WorldToScreen` 헬퍼로 변환 공식 집중화
  - `DagEditor.HandlePointerPressed`: inline `(pos + VL) / scale` → `ViewportTransform.ScreenToWorld` 사용
  - `DagEditor.HandlePointerWheelChanged`: zoom 피벗 공식을 `worldUnderCursor * newScale − cursor` 형태로 명시화
  - `DagEditor.HandlePointerMoved`: `/ 1` no-op 제거, 패닝 델타가 scale-independent임을 수식으로 주석
  - `DagEditor.HandleConnectionStarted`: 죽은 Offset 분기 제거, 드래그 시작 시 TargetAnchor=SourceAnchor 의도 명시
  - `DagEditor.HandleConnectionDrag`: "TODO 버그 있음" 제거 — `GetPosition(PART_ItemsHost)` 가 ScaleTransform 역변환으로 월드 좌표를 반환함을 근거 주석으로 확인
  - `PendingConnection.cs`: `ViewportScale` 프로퍼티 추가, `RenderTransform`을 `TransformGroup(Scale, Translate)` 으로 교체 → zoom시 미리보기 선이 노드 앵커와 일치
  - `DagEditor.axaml`: `ViewportScale` 바인딩 추가
  - `ViewportTransformTests.cs`: 12개 [Fact] 추가 (identity / pan-only / zoom-only / pan+zoom / roundtrip / pan-scale-independence / zoom-pivot)
- **검증 지표**: 빌드 0 Error, 38 → 50개 테스트 100% 통과

---

### [Step 17] Dag 노드 인덱스 도입 + HandleConnectionChanged O(n) → O(1) 최적화
- **날짜**: 2026-03-06
- **수행 내용**:
  - `Dag._nodeIndex: Dictionary<Guid, DagNode>` 추가 — `AddDagNodeItem`/`DelDagNodeItem` 성공 시 자동 갱신
  - `Dag.FindNode(Guid): DagNode?` 공개: O(n) `FirstOrDefault` → O(1) Dictionary 조회
  - `AddDagConnectionItem`: `_dagItemsSource.Items.FirstOrDefault` 2회 → `FindNode` 2회 (source/target 노드 조회)
  - `DagEditorViewModel.FindNode(Guid)`: `Dag.FindNode` 위임 노출
  - `DagEditor.HandleConnectionChanged` 재작성:
    - 기존: `_viewModel.Items` 전체 O(n) 순회 + `OldSourceAnchor == connectionItem.SourceAnchor` 앵커 휴리스틱
    - 개선: `FindNode(args.NodeId)` O(1) → `dagNode.SourceConnections` / `TargetConnections` O(k) 직접 순회
    - 노드 소유 연결만 순회하므로 앵커 비교 휴리스틱이 불필요해져 제거
  - **불변조건 (invariants)**:
    - DagItems 추가/삭제는 반드시 `Dag.AddDagNodeItem` / `DelDagNodeItem` 경로를 통해서만 수행 → `_nodeIndex` 동기화 보장
    - `DagNode.NodeId(Guid)`는 생성 후 불변 (`CreateDagNode`에서 `Guid.NewGuid()` 1회 할당)
    - `DelDagNodeItem` 성공 경로(NodeInstance != null)에서만 `_nodeIndex.Remove` 호출 → 실패 시 인덱스 오염 없음
- **검증 지표**: 빌드 0 Error, 60 → 65개 테스트 100% 통과 (`FindNode` 5개 신규)

---

### [Step 18] connector snap UX — tree-order → closest-candidate 정책
- **날짜**: 2026-03-06
- **수행 내용**:
  - `Extension.GetClosestControlUnderPointer<T>`: 후보 전체 열거 후 pointer와 bounds center 간
    거리(DistanceSq = `(localPtr - boundsCenter)^2`)가 최소인 컨트롤 반환. 동점은 tree 순서 우선.
  - `Extension.PickClosestCandidate<T>(IReadOnlyList<(T Item, double DistanceSq)>)`: 선택 정책
    pure function — Avalonia 의존 없이 단위 테스트 가능.
  - `SourceConnector.HandlePointerMoved`: `GetControlUnderPointer` → `GetClosestControlUnderPointer` 1줄 교체.
  - 기존 `GetControlUnderPointer`는 유지 (API 호환).
  - 단일 후보 시 기존 동작과 동일.
- **검증 지표**: 빌드 0 Error, 65 → 71개 테스트 100% 통과 (`ConnectorSnapTests` 6개 신규)

---

### [Step 19] Viewport Contract Hardening
- **날짜**: 2026-03-07
- **수행 내용**:
  - **ViewportTransform.cs** — VCA 매핑 주석 추가 (ViewportLocation ≡ Offset, ViewportScale ≡ Scale, 수식 동일 명시)
  - **DagEditorViewModel.cs** — 책임 방향(ViewModel = Source of Truth, StyledProperty = 패스스루) 및 VCA 매핑 주석 강화
  - **DagEditor.cs** — 단방향 ViewModel→DagEditor 동기화를 **양방향 동기화**로 교체
    - `_syncingViewport` 플래그로 재진입 차단
    - 외부 코드가 `DagEditor.ViewportLocation/Scale` 에 직접 쓸 때도 ViewModel 이 일치 보장
    - 미래 VCA `Offset`/`Scale` 양방향 바인딩 지원 준비
  - **tests/DagEdit.Tests/SelectionRectTests.cs** (신규) — 11개 테스트
    - identity / pan-only / zoom-only / pan+zoom 각 상태에서 selection rect 포함·제외 검증
    - viewport 불변성: 동일 월드 선택 rect가 서로 다른 viewport 상태에서도 동일한 worldRect를 생성함을 검증
    - VCA 공식 일치 검증: `WorldToScreen` = `world * Scale - Offset`, `ActualViewbox.TopLeft` = `ScreenToWorld(0,0)` 수식 일치
  - **docs/viewport-contract.md** (신규) — viewport 계약 문서
    - Single Source of Truth, 책임 방향, transform formula, VCA 매핑 표, selection rectangle 원칙, VCA 통합 경로
- **VCA 통합 관점 리스크 분석**:
  - **해소됨**: viewport 공식이 VCA와 완전히 일치함을 테스트로 확인 (`SelectionRectTests.TransformFormula_MatchesVcaFormula`, `InverseFormula_MatchesVcaActualViewboxOrigin`)
  - **해소됨**: 양방향 동기화 gap (외부 쓰기 시 ViewModel 불일치) → `_syncingViewport` 가드로 처리
  - **잔여 리스크**: VCA를 items host로 교체 시 `PART_ItemsHost` 기반 좌표계 일치 검사(`HandleLoaded`) 업데이트 필요
- **검증 지표**: 빌드 0 Error, 103 → **114개** 테스트 100% 통과 (`SelectionRectTests` 11개 신규)

---

## 향후 과제

| 우선순위 | 내용 |
|---------|------|
| High | StyleCop 경고 0건 달성 (295 → 0, 전체 Error 격상 가능 시점) |
| High | `DelDagNodeItem` 해피패스 테스트 (Node 인스턴스 분리 필요) |
| High | Connection 삭제 테스트 추가 (Step 13 구현에 대한 단위 테스트) |
| Medium | 커버리지 목표 설정 (목표: 80% 이상, 현재 4%) |
| Medium | Zoom 기능 구현 |
| Low | `docs/performance_trend.png` 생성 (히스토리 3회 이상 누적 후) |
