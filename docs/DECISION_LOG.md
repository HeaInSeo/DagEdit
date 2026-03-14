# Decision Log

> 주요 기술 의사결정의 이유와 기대 효과를 기록한다.

---

## 의사결정 목록

### [DEC-001] 테스트 프레임워크: xUnit 선택
- **날짜**: 2026-03-02
- **결정**: xUnit 2.9.0
- **대안**: NUnit, MSTest
- **이유**:
  - .NET 생태계에서 가장 높은 채택률 (ASP.NET Core, Avalonia 공식 테스트에서도 사용)
  - 각 테스트가 독립 인스턴스에서 실행 → 상태 공유 없음 (IClassFixture 패턴)
  - async/await 네이티브 지원
  - xunit.runner.visualstudio로 VS/Rider 통합 용이
- **기대 효과**: 테스트 간 간섭 최소화, CI 병렬 실행 안정성 향상

---

### [DEC-002] UI 테스트 백엔드: Avalonia.Headless
- **날짜**: 2026-03-02
- **결정**: Avalonia.Headless.XUnit 11.0.0
- **대안**: 실제 디스플레이 서버 + Virtual framebuffer(Xvfb)
- **이유**:
  - Rocky 8 서버 환경에서 X11/Wayland 디스플레이 서버 없이 실행 가능
  - GitHub Actions ubuntu-latest에서도 디스플레이 서버 불필요
  - 공식 Avalonia 팀이 유지 관리하는 공식 헤드리스 백엔드
  - `UseHeadlessDrawing = true` 옵션으로 렌더링 오버헤드 제거
- **기대 효과**: CI 환경에서 Avalonia 컨트롤 단위 테스트 가능

---

### [DEC-003] 코드 커버리지 형식: Cobertura
- **날짜**: 2026-03-02
- **결정**: coverlet.collector + Cobertura XML 형식
- **대안**: lcov, OpenCover, JaCoCo
- **이유**:
  - Cobertura는 GitHub Actions, SonarQube, GitLab CI 등 주요 CI/CD 플랫폼에서 모두 지원
  - `coverlet.collector`는 `dotnet test`의 `--collect:"XPlat Code Coverage"` 플래그와 직접 통합
  - 별도 도구 설치 없이 NuGet 패키지만으로 동작
- **기대 효과**: CI에서 커버리지 리포트 자동 생성 및 아티팩트 업로드 가능

---

### [DEC-004] 정적 분석기: StyleCop.Analyzers + Roslyn 내장 분석기
- **날짜**: 2026-03-02
- **결정**: StyleCop.Analyzers 1.2.0-beta.507 + `AnalysisLevel=latest`
- **대안**: Roslynator, SonarAnalyzer.CSharp, FxCop
- **이유**:
  - StyleCop: 코딩 스타일 규칙(네이밍, 공백, 문서화)에 특화
  - Roslyn 내장 분석기 (`AnalysisLevel=latest`): 코드 품질/버그 탐지에 특화
  - 두 도구가 역할이 보완적이라 함께 사용해도 중복 경고 최소화
  - StyleCop은 `.editorconfig`로 규칙별 severity 세밀하게 제어 가능
- **기대 효과**: 빌드 시점에 스타일 + 품질 이중 검증 자동화

---

### [DEC-005] 정적 분석 핵심 규칙 Error 격상 (Go-like 무타협 환경)
- **날짜**: 2026-03-03 (2026-03-02 기존 결정 업데이트)
- **결정**: 핵심 안전 규칙 → `error`, 나머지 SA* → `warning` 유지
- **Error 격상 규칙**:
  - `SA1503` — 중괄호 생략 금지 (Heartbleed 류 버그 방지)
  - `CS8600-CS8625` — nullable 안전성 강제 (Go nil-panic 방지)
  - `SA1400` — 접근 한정자 명시 강제 (Go 가시성 원칙)
  - `SA1106` — 빈 구문 금지
  - `IDE0059`, `IDE0060`, `CS0168`, `CS0219` — 미사용 코드 금지 (Go 컴파일 에러와 동일)
  - private 필드 `_camelCase` 네이밍, `int`/`string` 타입 키워드
- **이유**: "성능은 기능만큼 중요하다"와 동일하게 "코드 품질은 기능만큼 중요하다". 즉각적인 규칙 위반 탐지로 리뷰 부담 감소
- **영향**: 기존 SA1503 위반 30건 즉시 수정 (전 파일 중괄호 추가). 빌드 0 Error 확인.
- **목표**: 현재 295 SA* 경고를 점진적으로 0으로 감소 → 전체 Error 격상

---

### [DEC-006] 벤치마크 프레임워크: BenchmarkDotNet
- **날짜**: 2026-03-02
- **결정**: BenchmarkDotNet 0.14.0
- **대안**: NBench, 직접 Stopwatch 측정
- **이유**:
  - .NET 생태계 표준 마이크로벤치마크 도구 (Microsoft 공식 권장)
  - JIT 워밍업 자동 처리, 통계적으로 신뢰할 수 있는 결과
  - `[MemoryDiagnoser]`로 GC 할당량 측정 가능
  - `--job Dry` 모드로 CI에서 실제 측정 없이 컴파일+실행 검증 가능
- **기대 효과**: DAG 알고리즘 성능 회귀를 수치로 조기 탐지

---

### [DEC-007] CI 벤치마크 실행 모드: Short Job (Dry Run에서 업그레이드)
- **날짜**: 2026-03-03 (2026-03-02 기존 결정 변경)
- **결정**: CI에서 `--job Short --filter "*"` + `JsonExporter.Full` 사용
- **변경 이유**:
  - Dry Run은 컴파일/실행 검증만 가능, 실측값 없어 회귀 감지 불가
  - Short Job: 실제 통계적 측정값 수집하면서 Full Job보다 빠름 (~2-4분)
  - JsonExporter.Full로 BDN JSON 출력 → 이전 실행과 자동 비교 가능
- **기대 효과**: CI에서 10% 이상 성능 회귀 시 빌드 실패로 즉시 탐지
- **연관 결정**: DEC-009 (성능 회귀 가드)

---

### [DEC-009] 성능 회귀 가드: GitHub Cache 기반 기준선 비교
- **날짜**: 2026-03-03
- **결정**: `benchmark-baseline.json`을 GitHub Cache에 저장, 매 CI 실행 시 비교
- **대안**: 아티팩트 다운로드, 브랜치 커밋, 외부 스토리지
- **이유**:
  - GitHub Cache: 브랜치별 자동 격리, 7일 TTL, 10GB 무료 용량
  - Artifact API 대비 단순한 restore-keys 기반 최신 기준선 탐색
  - master 브랜치 push + 회귀 없음 조건에서만 기준선 갱신 → 회귀가 기준선을 오염시키지 않음
- **임계값**: Mean 또는 Allocated +10% 이상 악화 시 빌드 실패 (소수점 2자리 정밀도)
- **기대 효과**: 성능 저하를 코드 변경과 즉시 연결하여 회귀 원인 추적 용이

---

### [DEC-010] ReactiveUI WhenAnyValue: NodeDragState 분리 패턴
- **날짜**: 2026-03-03
- **결정**: `Node` (ContentControl) 내에 별도 `NodeDragState : ReactiveObject`를 두어 WhenAnyValue 사용
- **대안**: Avalonia `GetObservable(Property)` 직접 사용, Node에 IReactiveObject 구현
- **이유**:
  - `AvaloniaObject`는 `IReactiveObject`를 구현하지 않아 `WhenAnyValue` 직접 사용 불가
  - `ReactiveObject`를 별도로 분리하면 ReactiveUI 핵심 패턴(WhenAnyValue)을 명확히 시연 가능
  - Go 채널 비유: `NodeDragState` = 채널, `HandlePointerMoved` = 생산자, `WhenAnyValue` 구독 = 소비자
- **기대 효과**: 입력 처리(HandlePointerMoved)와 부수 효과(앵커 재계산, 이벤트 발행) 분리 → 단위 테스트 용이

---

### [DEC-011] CodeQL 보안 분석: 별도 Job 격리
- **날짜**: 2026-03-03
- **결정**: `codeql` job을 `verify` job과 분리하여 병렬 실행
- **이유**:
  - CodeQL은 빌드 추적이 필요하여 `verify` job의 NuGet 캐시와 독립적인 환경 필요
  - 별도 job으로 격리 시 한 job의 실패가 다른 job에 영향 없음
  - `security-extended` 쿼리 팩: CWE 분류 기반 보안 취약점 탐지 (SQL Injection, Path Traversal 등)
- **기대 효과**: 보안 취약점을 PR 단계에서 자동 탐지, GitHub Security 탭에 결과 자동 게시

---

### [DEC-008] 프로젝트 구조: 서브디렉토리 분리
- **날짜**: 2026-03-02
- **결정**: `tests/DagEdit.Tests/`, `benchmarks/DagEdit.Benchmarks/`
- **대안**: 루트 디렉토리에 모두 배치
- **이유**:
  - .NET 커뮤니티 컨벤션 (dotnet/runtime, dotnet/aspnetcore 동일 구조 사용)
  - 파일 탐색 및 `.gitignore` 관리 용이
  - 미래 확장 시 (`tests/DagEdit.IntegrationTests/` 등) 구조 명확
- **기대 효과**: 장기적 유지보수성 향상

---

### [DEC-012] PendingConnection AXAML 완전 제거 + ReactiveUI 전환
- **날짜**: 2026-03-04
- **결정**:
  1. `PendingConnection.axaml` 삭제 → 스타일·템플릿을 C# `FuncControlTemplate`으로 완전 이관
  2. `PendingConnectionState : ReactiveObject` 신규 생성 (`NodeDragState` 패턴 적용)
  3. `IDisposable _disposable` → `CompositeDisposable _disposables` 전환
  4. `AvaloniaProperty.GetObservable()` + `WhenAnyValue` + `DisposeWith` 체인으로 모든 상태 반응형 처리
- **수정된 버그**:
  - B-1: `_disposable` 두 번 덮어쓰기 → 첫 번째 구독 영구 누수 (Critical)
  - B-2: `ViewportLocationProperty.Register<DagEditorCanvas, Point>` 오너 타입 오류
  - B-3: `SetFillAndStrokePropertyChanged`의 `Sender is Connection` 체크 → dead code (sender는 항상 `PendingConnection`)
- **대안**:
  - AXAML 유지 + C# 버그 수정만: 템플릿 유지비용, XAML-C# 이중 관리 부담 잔존
  - Avalonia `GetObservable(Property)` 직접 사용 (상태 클래스 없이): 테스트 용이성 낮음, NodeDragState 패턴 불일치
- **이유**:
  - "XAML to C# Migration" 원칙: 컨트롤 임베딩 용이성 + 단일 파일 관리
  - `PendingConnectionState` 분리: 향후 스냅-투-커넥터, 연결 가능 여부 검사 등 확장점 확보
  - `CompositeDisposable`은 구독 수 증가에 무관하게 안전한 수명 관리를 보장
  - `TemplateProperty.OverrideDefaultValue<PendingConnection>(BuildTemplate())`: 런타임 스타일 오버라이드 없이 C# 기본값으로 고정
- **데이터 흐름**:
  ```
  DagEditor (AXAML 바인딩) → PendingConnection.SourceAnchor (AvaloniaProperty)
    → GetObservable() → _state.SourceAnchor (ReactiveObject)
      → WhenAnyValue.Skip(1).DistinctUntilChanged()
        → _partConnection.Source  (OnApplyTemplate 이후)
  ```
- **기대 효과**:
  - 메모리 누수 3건 즉시 해소
  - 반응형 파이프라인 일관성 (Node.cs, PendingConnection.cs 동일 패턴)
  - `PendingConnectionState` 단독 단위 테스트 가능
- **검증 결과**: 빌드 0 Error / 295 Warning (기존 동일), 32/32 테스트 통과, BDN dry-run 24 benchmarks 성공

---

### [DEC-014] SourceAnchor/TargetAnchor 타입 계약 변경: Point? → Point
- **날짜**: 2026-03-05
- **결정**: `DagEditor.SourceAnchorProperty`, `TargetAnchorProperty`를 `Point?` → `Point`로 변경
- **이유**:
  - null의 의미("연결 없음")를 `IsVisiblePendingConnection = false`로 분리 표현하여 단일 책임 명확화
  - `Point?` → `Point` 전환으로 바인딩 경로에서 nullable 역참조 가드 제거 가능
  - PendingConnection 내부에서 `Skip(1)` 제거 후 첫 프레임 anchor가 `default(Point)`로 초기화되어도 안전 (IsVisible=false이면 렌더링 안 됨)
- **상태 계약**: `IsVisiblePendingConnection = true` → SourceAnchor/TargetAnchor 유효 / `false` → 값 무시

---

### [DEC-015] 월드 좌표를 Source of Truth로 사용
- **날짜**: 2026-03-06
- **결정**: 노드 위치, 앵커, 커넥션 앵커, 드래그 목표 좌표를 모두 월드 좌표로 저장·전달한다. 스크린 좌표는 렌더링·포인터 입력 처리 시의 파생값으로만 사용한다.
- **근거**:
  - `DagEditorCanvas.RenderTransform = TransformGroup(Scale(s), Translate(-vl.X, -vl.Y))` 적용 후 `GetPosition(PART_ItemsHost)`가 Avalonia 내부 역변환으로 이미 월드 좌표를 반환함
  - `SourceConnector.Anchor` = `Node.SourceAnchor` (FindAnchors(worldLocation) 에서 생성) → 월드 좌표 ✓
  - `HandleConnectionDrag`의 `TargetAnchor = Offset` 은 월드 포인터 위치로 올바른 코드였음. 이전 "TODO 버그 있음" 주석은 zoom 도입 전 분석으로 오해 소지가 있었음 → 제거
  - `PendingConnection`에 동일한 `TransformGroup(Scale, Translate)` 적용으로 줌 상태에서도 미리보기 선이 노드 앵커와 정렬됨
  - 패닝 델타 `ΔVL = −Δscreen` 은 scale과 무관함 (WorldUnderCursor 공식에서 s 약분 확인)
- **유틸**: `ViewportTransform.ScreenToWorld` / `WorldToScreen` 으로 공식 단일화

---

### [DEC-016] InspectCode 경고 회귀 금지 기준선 채택
- **날짜**: 2026-03-14
- **결정**: `inspectcode.yml`에 "Check warning regression" 게이트 추가
- **정책**: *InspectCode warnings may decrease or stay flat, but must not increase.*
  - 감소·동일: 통과 ✅
  - 증가 (total 또는 warning severity): 빌드 실패 ❌
  - note/info: Summary 표시만, 게이트 조건 제외
  - baseline 없음 (첫 실행): info 통과, 이번 실행이 새 baseline
- **이유**: 경고 총량이 많더라도 "지금부터 늘지 않게"를 CI로 강제하면, 매 PR에서 경고 증가를 즉시 차단할 수 있음
- **연관 결정**: DEC-004 (StyleCop), DEC-005 (Error 격상 목표)

---

### [DEC-013] VirtualCanvas_ref 빌드 제외
- **날짜**: 2026-03-04
- **결정**: `DagEdit.csproj`에 `<Compile Remove="VirtualCanvas_ref/**/*.cs" />` 추가
- **이유**:
  - `VirtualCanvas_ref/src/` 는 WPF 의존 참조 코드 (`System.Windows.*`)
  - Avalonia 프로젝트에서 컴파일 시 다수의 CS0234/CS0246 에러 발생
  - 심볼릭 링크 특성상 글로브 패턴이 자동으로 파일을 포함하게 됨
- **영향**: 참조 분석(VirtualCanvas 이식 로드맵)은 파일 직접 열람으로 수행 — 빌드에 포함 불필요
