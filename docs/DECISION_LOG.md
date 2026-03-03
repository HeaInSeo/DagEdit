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

### [DEC-005] 정적 분석 초기 severity: warning (error 아님)
- **날짜**: 2026-03-02
- **결정**: StyleCop 규칙 severity=warning으로 초기 설정
- **이유**:
  - 기존 코드베이스에 SA* 위반 사항 다수 존재
  - 즉시 error로 격상 시 모든 빌드 실패 → 개발 흐름 차단
  - 단계적 적용: warning 확인 → 코드 수정 → error 격상 순으로 진행
- **목표**: SA 경고 수를 점진적으로 0으로 줄인 후 error로 격상
- **CI 연동**: verify.yml에서 경고 수를 수치로 보고하여 회귀 추적

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

### [DEC-007] CI 벤치마크 실행 모드: Dry Run
- **날짜**: 2026-03-02
- **결정**: CI에서 `--job Dry --filter "*"` 사용
- **대안**: 실제 벤치마크 실행, 완전 스킵
- **이유**:
  - 실제 벤치마크는 수 분~수십 분 소요 → CI 속도 저하
  - 완전 스킵 시 컴파일 오류 미탐지 위험
  - Dry Run: 각 벤치마크를 1회만 실행하여 컴파일/런타임 오류만 탐지
- **기대 효과**: CI는 빠르게 통과, 실제 성능 측정은 로컬/주간 전용 파이프라인에서 수행

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
