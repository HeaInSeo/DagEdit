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

### [Step 20] VCA Pin/Unpin consumer-side wiring (H-3)
- **날짜**: 2026-03-11
- **수행 내용**:
  - `DagEditorViewModel`: `PinRequested`/`UnpinRequested` internal event 추가 — ViewModel은 `ViewerCanvas`를 직접 참조하지 않고 이벤트로 위임
  - `DagEditor`: `_pinnedBySelection: HashSet<Guid>` 도입; selection 확정 시 기존 pin 해제 후 신규 선택 pin; `NodeDragStartedEvent` 수신 시 drag 노드 pin 요청
  - `Node.cs`: `NodeDragStartedEvent` (Bubble) 추가, drag 시작 시 발행
  - `MainWindow.axaml.cs`: `vm.PinRequested → ViewerCanvas.Pin`, `vm.UnpinRequested → ViewerCanvas.Unpin` wiring; `Unloaded`에서 명시적 unsubscribe
- **책임 경계**: DagEdit가 "어느 노드를 pin할지" 결정(selection/drag 의미론 소유), VCA는 Pin/Unpin 수행만 담당. VCA pinning contract 상세는 VCA canonical 문서 참조.
- **검증 지표**: 빌드 0 Error, → 165개 테스트 100% 통과 (`ViewerPinUnpinTests` 8개 신규)

---

### [Step 21] NodeDragEndedEvent — drag pin leak 방지 (H-4)
- **날짜**: 2026-03-13
- **수행 내용**:
  - **버그**: drag 시작 후 같은 셀에 놓이면 `NodeMovedEvent` 미발행 → drag pin 누수
  - `NodeMovedEventArgs.cs`: `NodeDragEndedEventArgs` 추가
  - `Node.cs`: `NodeDragEndedEvent` (Bubble) 추가; `HandlePointerReleased`에서 이동 여부와 무관하게 항상 발행
  - `DagEditor.cs`: `HandleNodeDragEnded` — 선택 집합에 없는 노드에 한해 `RequestUnpinNode` 호출
  - drag pin lifecycle 기준을 "이동 발생 여부"에서 "drag 종료"로 교정
- **검증 지표**: 빌드 0 Error, 165 → 170개 테스트 100% 통과 (`NodeDragEndedTests` 5개 신규)

---

### [Step 22] Overlapping pin lifecycle 검증 (H-5)
- **날짜**: 2026-03-13
- **수행 내용**:
  - **아키텍처 불변조건 확인**: "drag 활성 중 deselection" 시나리오(S-3/S-4)는 현재 DagEditor에서 발생 불가 — `Node.HandlePointerPressed`가 `args.Handled = true`를 설정하므로, DagEditor의 `IsSelecting = true` 진입 조건(`!args.Handled`)이 차단됨
  - **VCA Pin/Unpin 계약 확인**: `HashSet<ISpatialItem>`기반 set-semantics — `Pin(x)` 중복 호출은 idempotent, `Unpin(x)` 1회로 즉시 해제
  - **S-6b 중복 unpin 문서화**: 이동이 있는 drag 종료 시 `NodeMovedEvent` + `NodeDragEndedEvent`가 모두 발행되어 비선택 노드에 `UnpinRequested` 2회 발행 — VCA HashSet이 idempotent이므로 실제 영향 없음, 코드 수정 없이 테스트로 명시
  - `PinLifecycleTests.cs` (9개): S-1/2/2b/5×2/6a/6b + SelectionChange + ArchitecturalInvariant
- **코드 수정 없음** — H-3/H-4 현재 구현이 모든 도달 가능 시나리오를 올바르게 처리함
- **검증 지표**: 빌드 0 Error, 170 → 179개 테스트 100% 통과

---

### [Step 23] Unpin 경로 정규화 (H-6)
- **날짜**: 2026-03-13
- **수행 내용**:
  - `DagEditor.HandleNodeMoved`에서 `RequestUnpinNode` 로직 제거
  - `HandleNodeMoved`는 `PushMoveNode + args.Handled = true`만 유지
  - drag pin 해제의 단일 경로를 `HandleNodeDragEnded`로 확정
  - `PinLifecycleTests.cs`: `PinSim.NodeMoved` unpin 제거 일치, S-6b assertion 2→1회 수정
- **동기**: S-6b에서 이동 있는 drag 종료 시 `UnpinRequested` 2회 중복 발행이 있었음. 현재는 VCA가 idempotent이므로 무해하나, 미래에 Unpin이 ref-counted로 변경될 경우에 대비
- **검증 지표**: 빌드 0 Error, 179개 테스트 100% 통과 (테스트 수 변동 없음)

---

### [Step 24] Undo-after-delete pin contract 고정 (H-8)
- **날짜**: 2026-03-13
- **수행 내용**:
  - `tests/DagEdit.Tests/UndoAfterDeletePinContractTests.cs` 신규 (10개 테스트)
  - **코드 수정 없음** — 기존 계약 불일치 없음. 테스트로만 계약 명문화.
  - `UndoAfterDeleteSim`: DagEditor pin guard + `DelNodeCommand.Undo()` 경로를 test-local pure 클래스로 미러링
  - 검증 계약:
    - **C2**: `Undo`는 `PinRequested`를 발행하지 않는다
    - **C3**: `Undo` 후 `_pinnedBySelection`에 nodeId 없음
    - **C4**: `Undo` 후 adapter snapshot 복원됨 (새 object ref)
    - **C5**: `Undo` 후 selection → 새 pin cycle이 정상 시작됨
    - **C6**: `Undo` 후 drag start/end → 새 pin cycle이 정상 동작함
  - 전체 시퀀스 테스트: Select → Delete → Undo → Select → Drag → Deselect
  - 비선택 노드 케이스, 아키텍처 불변조건(Undo 경로에 pin/unpin 코드 경로 없음) 3회 반복 검증 포함
- **검증 지표**: 빌드 0 Error, 188 → **198개** 테스트 100% 통과

---

### [Step 25] InspectCode 경고 1차 정리 + 회귀 게이트 고정
- **날짜**: 2026-03-14
- **수행 내용**:
  - **경고 정리 (1차 패스)**:
    - `BaseNode.cs`: 미사용 `using Microsoft.VisualBasic;` 제거
    - `DagItems.cs`: 미사용 `using System.Data.Common;` 제거
  - **회귀 게이트 (`inspectcode.yml`)**:
    - "Check warning regression" 스텝 추가 (Parse SARIF 직후, artifact 업로드 전)
    - 현재 `warning-count.json` vs 이전 `prev-warning-count.json` 비교
    - total 또는 warning severity 증가 시 `exit 1` → workflow 실패
    - baseline 없으면 info 통과, artifact 업로드는 `if: always()`로 항상 실행
    - Job Summary에 ✅ PASS / ❌ FAIL 판단 테이블 추가
  - `docs/DECISION_LOG.md`: DEC-016 추가 (회귀 금지 정책 명문화)
- **정책**: *InspectCode warnings may decrease or stay flat, but must not increase.*
- **검증 지표**: 빌드 0 Error, 198개 테스트 100% 통과, workflow YAML 구문 유효

---

### [Step 26] Lint Sprint 실행 문서화 + S0 기준선 정렬 착수
- **날짜**: 2026-04-09
- **수행 내용**:
  - `docs/LINT_SPRINT_EXECUTION_PLAN.md` 추가
  - 5개 스프린트(S0~S4) 일정, 종료 기준, 운영 정책 문서화
  - `verify.yml` 경고 집계 로직 보정: `CA*` 분리 집계, 총경고를 실제 build warning line 기준으로 계산
  - `inspectcode.yml` baseline fetch 보정: PR에서는 `GITHUB_BASE_REF`, 그 외에는 `GITHUB_REF_NAME` 기준으로 이전 성공 실행 탐색
  - `InspectCode` summary에 비교 기준 branch 표시 추가
- **의도**:
  - GitHub 업로드 후 회귀 검사 실패는 실제 실패로 유지
  - 다만 잘못된 집계나 잘못된 baseline branch 선택으로 인한 거짓 실패는 제거
- **검증 지표**:
  - workflow YAML 구문 유효
  - `Verify` summary가 `CA*`를 포함한 총경고를 표시
  - PR run에서도 baseline branch 기준이 명시됨

---

### [Step 27] Verify 경고 기준선 아티팩트 + 회귀 게이트 추가
- **날짜**: 2026-04-09
- **수행 내용**:
  - `verify.yml`에 이전 성공 실행의 `build-warning-count` artifact 다운로드 추가
  - 현재 실행의 `build-warning-count.json` 생성 추가
  - total 및 `SA/CA/CS/IDE` prefix별 증가 시 실패하는 build warning regression gate 추가
  - baseline 없음 또는 build 자체 실패 시 비교는 정보성 스킵으로 처리
- **의도**:
  - `InspectCode`와 별개로 실제 build warning도 독립 baseline으로 관리
  - PR에서도 target branch 기준으로 빌드 경고 회귀를 잡되, baseline 부재나 build 실패 때문에 거짓 실패가 나지 않게 함
- **검증 지표**:
  - `verify.yml`이 `build-warning-count.json` artifact를 업로드
  - 이전 성공 실행 artifact가 있으면 warning regression을 비교
  - 증가가 있으면 workflow 실패

---

### [Step 28] S1 저위험 StyleCop 정리 1차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - 포맷/레이아웃 중심 파일 1차 정리: `App.axaml.cs`, `TemplateLayoutCanvas.cs`, `TargetConnector.cs`, `MultiGesture.cs`, `DagItems.cs`, `Connection.cs`, `Extension.cs`
  - 정리 대상: 후행 쉼표, 주석 공백, trailing whitespace, 일부 multi-line parameter 줄바꿈, BOM 제거
  - 스프린트 종료 보고 규칙 추가: 종료 시 다음 추천 스프린트와 이유를 반드시 기록
- **의도**:
  - S1 범위를 동작 무관한 기계적 정리부터 시작
  - 이후 스프린트 종료 보고 형식을 고정해 문서 업데이트를 일관화
- **검증 지표**:
  - 저위험 StyleCop 규칙 감소 예상
  - 기능 의미 변경 없음

---

### [Step 29] S1 저위험 StyleCop 정리 2차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `BaseNode.cs`, `SourceConnector.cs`, `Program.cs`, `MainWindow.axaml.cs` 정리
  - `using` 정렬, comment spacing, trailing whitespace 제거, 단일행 블록 확장, private 필드 네이밍 정리(`elementUnderPointer` → `_elementUnderPointer`)
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `320 Warning(s)` → `307 Warning(s)`

---

### [Step 30] S1 저위험 StyleCop 정리 3차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `BrushResources.cs`, `BaseNode.cs`, `DagEditor.cs`, `Connector.cs`, `Node.cs` 정리
  - BOM 제거, `using` 정렬, comment spacing, multi-line parameter/argument 정리, multi-line child statement 중괄호 추가
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `307 Warning(s)` → `292 Warning(s)`

---

### [Step 31] S1 저위험 StyleCop 정리 4차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `Connection.cs`, `DagEditor.cs` 추가 정리
  - 대상: comment spacing, single-line block 확장, trailing comma, multi-line parameter 줄바꿈
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `292 Warning(s)` → `282 Warning(s)`

---

### [Step 32] S1 저위험 StyleCop 정리 5차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `EditorContextFlyout.cs`, `SourceConnector.cs`, `Connection.cs`, `DagEditor.cs` 추가 정리
  - 대상: trailing comma, comment spacing, method signature 정렬, static member/return-type 개선, 불필요한 괄호 제거
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `282 Warning(s)` → `273 Warning(s)`

---

### [Step 33] S1 저위험 StyleCop 정리 6차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `EditorContextFlyout.cs`, `DagItems.cs`, `Extension.cs`, `MultiGesture.cs` 추가 정리
  - 대상: constructor initializer 줄바꿈, `this.` 제거, generic constraint 줄바꿈 일부, comment/branch 스타일 정리
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `273 Warning(s)` → `265 Warning(s)`

---

### [Step 34] S1 저위험 StyleCop 정리 7차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `ILocatable.cs`, `EditorContextFlyout.cs`, `NodeViewItem.cs`, `Node.cs`, `Extension.cs`, `EditorMenuItem.cs`, `TargetConnector.cs`, `SourceConnector.cs`, `DagItems.cs`, `MultiGesture.cs`, `UndoRedoStack.cs` 추가 정리
  - 대상: 파일 종료 개행, 생성자 initializer 줄바꿈, comment spacing, 멤버 순서 일부, constructor/property 위치 정리
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `265 Warning(s)` → `241 Warning(s)`

---

### [Step 35] S1 저위험 StyleCop 정리 8차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `TargetConnector.cs`, `SourceConnector.cs`, `UndoRedoStack.cs`, `MultiGesture.cs`, `ConnectionChangedEventArgs.cs`, `NodeViewItem.cs` 추가 정리
  - 대상: 생성자/중첩 타입/속성 순서 재배치, 다중 파라미터 줄바꿈, 공개 멤버 순서 보정
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `241 Warning(s)` → `235 Warning(s)`

---

### [Step 36] S1-S2 경계 구조 정리 1차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `NodeMovedEventArgs.cs`, `TargetConnector.cs`, `SourceConnector.cs`, `BaseNode.cs`, `Dag.cs`, `TemplateLayoutCanvas.cs`, `ViewportTransform.cs`, `NodeViewItemVisualFactory.cs`, `DagEditorCanvas.cs` 정리
  - 대상: 생성자/속성/필드 순서 재배치, 매개변수 이름 정합화(`constraint` → `availableSize`), 산술식 괄호 명시, `Dispose(bool)` 접근 수준 보정
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `235 Warning(s)` → `230 Warning(s)`

---

### [Step 37] S1-S2 경계 구조 정리 2차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `BaseNode.cs`, `DagEditorCanvas.cs`, `NodeViewItemVisualFactory.cs`, `DagEditor.cs` 추가 정리
  - 대상: 백킹 필드 위치 재배치, `DependencyProperty` 정적 필드와 인스턴스 속성 분리, `BaseNode` 상태를 필드에서 프로퍼티로 전환, 공개/비공개 멤버 순서 보정
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `230 Warning(s)` → `212 Warning(s)`

---

### [Step 38] S1-S2 경계 구조 정리 3차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `SourceConnector.cs`, `TargetConnector.cs`, `PendingConnection.cs` 추가 정리
  - 대상: 임시 상태 필드 제거, pointer release 시점 직접 조회로 단순화, 필드 섹션 상향 배치
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `212 Warning(s)` → `211 Warning(s)`

---

### [Step 39] S1-S2 경계 구조 정리 4차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `BaseNode.cs`, `DagEditorCanvas.cs`, `Connection.cs`, `DagEditor.cs` 추가 정리
  - 대상: 생성자/종료자/메서드 순서 재배치, 산술식 precedence 명시, 증분 빌드 수치와 클린 빌드 기준선 재검증
- **검증 지표**:
  - `dotnet clean DagEdit.sln -c Release` 성공
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `211 Warning(s)` → `204 Warning(s)`

---

### [Step 40] S1-S2 경계 구조 정리 5차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `SourceConnector.cs`, `TargetConnector.cs`, `PendingConnection.cs`, `DagViewerProjectionAdapter.cs` 추가 정리
  - 대상: static constructor 위치 조정, 속성/생성자 섹션 재배치, adapter 기본 extent 필드 상향
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `204 Warning(s)` → `186 Warning(s)`

---

### [Step 41] S1 저위험 스타일 정리 9차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `EditorContextFlyout.cs`, `Extension.cs`, `Connection.cs`, `DagViewerProjectionAdapter.cs` 추가 정리
  - 대상: comment spacing, static readonly 필드 네이밍, 산술식 precedence 보강, 이벤트/속성 순서 보정
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `186 Warning(s)` → `175 Warning(s)`

---

### [Step 42] S1-S2 경계 구조 정리 6차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `Connector.cs`, `SourceConnector.cs`, `TargetConnector.cs` 추가 정리
  - 대상: protected field를 protected property로 전환, 생성자/이벤트/메서드 순서 재배치, 파생 클래스 참조 정렬
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `175 Warning(s)` → `166 Warning(s)`

---

### [Step 43] S1-S2 경계 구조 정리 7차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `DagEditorViewModel.cs` 추가 정리
  - 대상: 필드/이벤트/속성/생성자/메서드 순서를 StyleCop 규칙에 맞게 재배치
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `166 Warning(s)` → `158 Warning(s)`

---

### [Step 44] S1-S2 경계 구조 정리 8차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `Node.cs` 추가 정리
  - 대상: 필드/생성자/이벤트/속성/메서드 순서 재배치, 상수/readonly/static 배치 보정, tuple 이름 및 기본값 생성자 정리
- **검증 지표**:
  - `dotnet clean DagEdit.sln -c Release` 성공
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `158 Warning(s)` → `149 Warning(s)`

---

### [Step 45] S1-S2 경계 구조 정리 9차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `Node.cs`, `BaseNode.cs` 추가 정리
  - 대상: public/internal/protected/private 순서 보정, helper/event handler 배치 정리, region 정합성 복구
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `149 Warning(s)` → `143 Warning(s)`

---

### [Step 46] S1-S2 경계 구조 정리 10차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `Connector.cs`, `DagEditorViewModel.cs` 추가 정리
  - 대상: `fields/ctors/finalizer/events/properties/methods` 순서 보정, public/internal 멤버 재배치
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore` 성공
  - 경고 수: `143 Warning(s)` → `138 Warning(s)`

---

### [Step 47] S1-S2 경계 구조 정리 11차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `Connection.cs` 추가 정리
  - 대상: 필드/생성자/속성/공개 메서드/override/helper/static helper 순서 재배치, `StreamGeometryContext` 구체 타입 반영
- **검증 지표**:
  - `dotnet clean DagEdit.sln -c Release` 성공
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `138 Warning(s)` → `131 Warning(s)`

---

### [Step 48] S1-S2 경계 구조 정리 12차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `PendingConnection.cs`, `DagEditorViewModel.cs` 추가 정리
  - 대상: public/protected/private 멤버 순서 보정, `FuncControlTemplate<PendingConnection>` 구체 타입 반영, `ViewerAdapter`/생성자/속성 배치 재정렬
- **검증 지표**:
  - `dotnet clean DagEdit.sln -c Release` 성공
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `131 Warning(s)` → `127 Warning(s)`

---

### [Step 49] S1-S2 경계 구조 정리 13차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `PendingConnection.cs`, `DagEditorViewModel.cs` 추가 정리
  - 대상: `OnApplyTemplate`/template helper 순서 보정, Pin/Unpin 이벤트 위치 조정, `SA1512` comment spacing 보정
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `127 Warning(s)` → `122 Warning(s)`

---

### [Step 50] S1-S2 경계 구조 정리 14차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `Connector.cs`, `Connection.cs`, `Node.cs` 추가 정리
  - 대상: 생성자/속성 순서 보정, dependency property와 상수 배치 재정렬, protected override와 private helper 순서 정리
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `122 Warning(s)` → `120 Warning(s)`

---

### [Step 51] S1-S2 경계 구조 정리 15차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `DagEditor.cs` 대규모 순서 정리
  - 대상: dependency property 등록과 CLR property 분리, readonly/non-readonly 필드 정렬, public/protected/private 메서드 블록 재배치
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `120 Warning(s)` → `103 Warning(s)`

---

### [Step 52] S1-S2 경계 구조 정리 16차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `Extension.cs`, `DagEditor.cs` 추가 정리
  - 대상: `Extension` private static field를 상단으로 이동해 `SA1201` 제거, `DagEditor`의 comment spacing 및 closing brace blank line 보정
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `103 Warning(s)` → `99 Warning(s)`

---

### [Step 53] S1-S2 경계 구조 정리 17차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `DagEditor.cs`, `Connection.cs`, `Node.cs` 추가 정리
  - 대상: `DagEditor` field 네이밍 보정, static helper 위치 재조정, `Connection`/`Node` helper 순서 재배치
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `99 Warning(s)` → `97 Warning(s)`

---

### [Step 54] S1-S2 경계 구조 정리 18차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `Dag.cs`, `EditorContextFlyout.cs`, `DagEditorCanvas.cs` 추가 정리
  - 대상: section header comment 뒤 blank line 제거, single-line comment spacing 보정, 주석 블록 앞뒤 공백 정리
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `97 Warning(s)` → `92 Warning(s)`

---

### [Step 55] S1-S2 경계 구조 정리 19차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `UndoableCommands.cs`, `NodeMovedEventArgs.cs`, `DagItems.cs` 구조 분리
  - 대상: 다중 타입 파일을 단일 타입 파일로 분리해 `AddNodeCommand`, `DelNodeCommand`, `AddConnectionCommand`, `DelConnectionCommand`, `MoveNodeCommand`, `NodeDragStartedEventArgs`, `NodeDragEndedEventArgs`, `DagNode`, `DagConnection`를 각각 별도 파일로 이동
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `92 Warning(s)` → `84 Warning(s)`

---

### [Step 56] S2 구조 분리 20차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - `UndoRedoStack.cs`, `MainWindow.axaml.cs`, `DagEditorViewModel.cs` 구조 분리
  - 대상: `IUndoableCommand`, `ProjectionChangedSubscription`, `AlreadyExecutedCommand`를 별도 파일로 이동해 다중 타입 파일 경고 제거
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `84 Warning(s)` → `81 Warning(s)`

---

### [Step 57] S3 API surface 정리 21차 배치
- **날짜**: 2026-04-09
- **수행 내용**:
  - 내부 전용 명령/도우미 타입의 접근성을 `internal`로 축소
  - 대상: `AddNodeCommand`, `DelNodeCommand`, `AddConnectionCommand`, `DelConnectionCommand`, `MoveNodeCommand`, `AlreadyExecutedCommand`, `IUndoableCommand`, `UndoRedoStack`, `EditorContextFlyout`
  - 참고: `NodeDragStartedEventArgs`, `NodeDragEndedEventArgs`, `NodeMovedEventArgs`는 `Node`의 공개 이벤트 시그니처에 노출되어 있어 `public` 유지
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `81 Warning(s)` → `69 Warning(s)`

---

### [Step 58] S4 수명주기 정리 22차 배치
- **날짜**: 2026-04-10
- **수행 내용**:
  - `DagEditorViewModel.cs`, `DagEditor.cs`, `MainWindow.axaml.cs` dispose 경로 보강
  - 대상: `ObservableAsPropertyHelper`와 `UndoRedoStack` 명시 해제, `DagEditor.Dispose(bool)`와 `GC.SuppressFinalize` 추가, `MainWindow`에 `IDisposable` 구현 추가
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `69 Warning(s)` → `65 Warning(s)`

---

### [Step 59] S4 null/dispose 정리 23차 배치
- **날짜**: 2026-04-10
- **수행 내용**:
  - `MainWindow.axaml.cs`, `Dag.cs`, `DagEditor.cs`, `PendingConnection.cs`, `Node.cs` null/dispose 정리
  - 대상: `MainWindow.Dispose(bool)` 패턴 정리, `RemoveDagItem`/`Restore*`/`OnApplyTemplate`/pointer handler 진입부에 `ArgumentNullException.ThrowIfNull` 추가
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `65 Warning(s)` → `55 Warning(s)`

---

### [Step 60] S3-S4 유틸리티 정리 24차 배치
- **날짜**: 2026-04-10
- **수행 내용**:
  - `Extension.cs`, `PointerGesture.cs`, `MultiGesture.cs`, `EditorMenuItem.cs`, `BrushResources.cs`, `Constants.cs`, `EditorGestures.cs`, `ViewportTransform.cs`, `TemplateLayoutCanvas.cs` 정리
  - 대상: 내부 전용 유틸리티/헬퍼 타입을 `internal`로 축소, `Extension`의 `InvariantCulture` 적용과 catch 구체화, `Subscribe`/`Matches` null guard 추가
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `55 Warning(s)` → `38 Warning(s)`

---

### [Step 61] S2-S3 잔여 스타일 정리 25차 배치
- **날짜**: 2026-04-10
- **수행 내용**:
  - `DagEditor.cs`, `MainWindow.axaml.cs`, `Node.cs`, `DagItems.cs` 정리 및 `UndoableCommands.cs` 제거
  - 대상: 빈 파일 제거, 불필요한 trailing blank line 제거, 일부 public/protected/private 멤버 순서 재배치
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `38 Warning(s)` → `35 Warning(s)`

---

### [Step 62] S3 API 표면 정리 26차 배치
- **날짜**: 2026-04-10
- **수행 내용**:
  - `DagEditorViewModel.cs`, `DagEditorCanvas.cs`, `MultiGesture.cs`, `Dag.cs` 접근성 재조정
  - 대상:
    - 앱 내부 전용 타입인 `DagEditorViewModel`, `DagEditorCanvas`, `MultiGesture.Match`를 `internal`로 축소
    - 벤치마크 프로젝트가 직접 참조하는 `Dag`는 `public`으로 복구해 외부 계약 유지
    - `MainWindow.axaml.cs` dispose 관련 멤버 순서 정리 유지
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `35 Warning(s)` → `29 Warning(s)`

---

### [Step 63] S3 API 표면 정리 27차 배치
- **날짜**: 2026-04-10
- **수행 내용**:
  - `DagEdit.csproj`, `App.axaml.cs`, `MainWindow.axaml.cs`, `Dag.cs`, `DagItems.cs`, `ILocatable.cs`, `BaseNode.cs`, `Node.cs`, `Connection.cs`, `Connector.cs`, `SourceConnector.cs`, `TargetConnector.cs`, `PendingConnection.cs`, `DagEditor.cs`, 각종 `*EventArgs.cs` 접근성 재조정
  - 대상:
    - `DagEdit.Benchmarks`를 `InternalsVisibleTo`로 열어 `Dag`와 `DagItems`를 `internal`로 축소
    - 앱 내부 UI 컨트롤(`DagEditor`, `Node`, `Connection`, `Connector`, `SourceConnector`, `TargetConnector`, `PendingConnection`, `BaseNode`)과 관련 이벤트 args를 `internal`로 축소
    - `App`, `MainWindow`를 `internal`로 축소
    - 공개 모델 `DagNode`, `DagConnection`에는 내부 UI 참조 프로퍼티를 `internal`로 이동
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `29 Warning(s)` → `11 Warning(s)`

---

### [Step 64] S3 API 표면 정리 28차 배치
- **날짜**: 2026-04-10
- **수행 내용**:
  - `DagItems.cs`, `DagNode.cs`, `DagConnection.cs`, `Connection.cs` 내부 enum/멤버 노출 축소
  - 대상:
    - `DagItemsType`, `ConnectionOffsetMode`, `LineShape`, `ConnectionDirection`, `ArrowHeadEnds`를 `internal`로 축소
    - `DagNode.DAGItemType`, `DagConnection.DAGItemType`, `NodeInstance`, `ConnectionInstance`, `SourceNodeInstance`, `TargetNodeInstance`를 내부 구현 멤버로 재배치
    - 공개/내부 멤버 순서를 재정렬해 `SA1202` 잔여 제거
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `11 Warning(s)` → `2 Warning(s)`

---

### [Step 65] S3 API 표면 정리 29차 배치
- **날짜**: 2026-04-10
- **수행 내용**:
  - `DagNode.cs`, `DagConnection.cs`, `benchmarks/DagEdit.Benchmarks/DagBenchmarks.cs` 정리
  - 대상:
    - `DagNode`, `DagConnection`을 `internal`로 축소
    - 벤치마크 `FindNode_ByGuid` 반환형을 내부 모델 타입 의존이 없는 `bool`로 조정
    - friend assembly 경계를 유지하면서 공개 API 노출 2건 제거
- **검증 지표**:
  - `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수: `2 Warning(s)` → `0 Warning(s)`

---

### [Step 66] CI follow-up test hardening
- **날짜**: 2026-04-10
- **수행 내용**:
  - `Extension.cs`의 `TryWriteErrorsToFile` 예외 처리 보강
  - 대상: 잘못된 경로 문자열(`null char` 포함)에서 발생하는 `ArgumentException`도 fallback 경로로 흡수
- **검증 지표**:
  - 로컬 빌드 `dotnet build DagEdit.sln -c Release --no-restore -clp:Summary -v:minimal` 성공
  - 경고 수 유지: `0 Warning(s)`, `0 Error(s)`

---

## 향후 과제

| 우선순위 | 내용 |
|---------|------|
| High | StyleCop 경고 0건 달성 (295 → 0, 전체 Error 격상 가능 시점) |
| High | `DelDagNodeItem` 해피패스 테스트 (Node 인스턴스 분리 필요) |
| High | Connection 삭제 테스트 추가 (Step 13 구현에 대한 단위 테스트) |
| Medium | S-6b double-unpin 제거 (NodeMovedEvent 핸들러에서 unpin 제거, DragEnded에 일원화) |
| Medium | 커버리지 목표 설정 (목표: 80% 이상, 현재 4%) |
| Medium | Zoom 기능 구현 |
| Low | `docs/performance_trend.png` 생성 (히스토리 3회 이상 누적 후) |
