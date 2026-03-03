# DagEdit

> Avalonia 기반의 시각적 DAG(방향 비순환 그래프) 편집기

---

## 개요

DagEdit는 데이터 처리 파이프라인을 직관적인 노드-엣지 방식으로 설계하고 시각화하는 크로스플랫폼 GUI 도구입니다.
드래그 앤 드롭으로 노드를 배치하고 연결하여 복잡한 데이터 흐름을 표현할 수 있습니다.

---

## 기술 스택

| 구분 | 기술 | 버전 |
|------|------|------|
| 런타임 | .NET | 8.0 |
| UI 프레임워크 | Avalonia | 11.0.0 |
| 반응형 프로그래밍 | ReactiveUI / System.Reactive | 19.6.1 / 6.0.0 |
| 테스트 | xUnit + Avalonia.Headless | 2.9.0 / 11.0.0 |
| 벤치마크 | BenchmarkDotNet | 0.14.0 |
| 정적 분석 | StyleCop.Analyzers + Roslyn | 1.2.0-beta.507 / latest |

---

## 프로젝트 구조

```
DagEdit/
├── *.cs / *.axaml          # 메인 에디터 소스
├── DagEdit.csproj           # 메인 프로젝트 (WinExe, net8.0)
├── DagEdit.sln              # 솔루션 파일
├── .editorconfig            # 코딩 스타일 규칙
├── .github/
│   └── workflows/
│       └── verify.yml       # CI/CD: 빌드·테스트·벤치마크·린트
├── tests/
│   └── DagEdit.Tests/       # xUnit 단위 테스트
├── benchmarks/
│   └── DagEdit.Benchmarks/  # BenchmarkDotNet 성능 벤치마크
└── docs/
    ├── DEV_HISTORY.md       # 개발 히스토리 (원본 TODO 및 진행 기록)
    ├── PROGRESS_LOG.md      # 작업 진행 로그
    └── DECISION_LOG.md      # 기술 의사결정 기록
```

---

## 개발 환경 설정

### 사전 요구 사항

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 이상
- Linux: X11 또는 Wayland 디스플레이 서버 (실행 시), 테스트는 헤드리스 모드로 불필요

### Rocky 8 / RHEL 8 환경 설정

```bash
# .NET 8 SDK 설치 확인
dotnet --version   # 8.0.x 출력 확인

# 필요 패키지 (Avalonia 실행용)
sudo dnf install -y libX11 libXext mesa-libGL fontconfig

# 의존성 복원
dotnet restore DagEdit.sln
```

### 빌드

```bash
# 전체 솔루션 빌드 (정적 분석 포함)
dotnet build DagEdit.sln --configuration Release

# 메인 앱 실행
dotnet run --project DagEdit.csproj
```

---

## 테스트

```bash
# 전체 테스트 실행
dotnet test tests/DagEdit.Tests/ --configuration Release

# Cobertura 커버리지 리포트 생성
dotnet test tests/DagEdit.Tests/ \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./test-results \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

# 커버리지 XML 위치
# ./test-results/{guid}/coverage.cobertura.xml
```

### 테스트 구조

| 파일 | 테스트 대상 | 테스트 수 |
|------|------------|---------|
| `DagTests.cs` | `Dag` 클래스 (AddDagNodeItem, AddDagConnectionItem, DelDagNodeItem) | 14개 |
| `DagItemsTests.cs` | `DagItems`, `DagNode`, `DagConnection` 모델 | 14개 |

> Avalonia.Headless.XUnit 인프라가 구성되어 있어, `[AvaloniaFact]` 어트리뷰트로 UI 컨트롤 직접 테스트도 가능합니다.

---

## 성능 벤치마크

```bash
# CI용 드라이런 (컴파일 및 실행 확인, 측정값 없음)
cd benchmarks/DagEdit.Benchmarks
dotnet run --configuration Release -- --job Dry --filter "*"

# 로컬 전체 벤치마크 (통계적 측정)
dotnet run --configuration Release -- --filter "*"

# 특정 벤치마크만 실행
dotnet run --configuration Release -- --filter "*AddNodes*"

# 사용 가능한 벤치마크 목록 확인
dotnet run --configuration Release -- --list flat
```

### 벤치마크 항목

| 벤치마크 | 측정 대상 |
|---------|---------|
| `AddNodes_Sequential` | N개 노드 순차 추가 처리량 (Baseline) |
| `AddConnections_Sequential` | N개 커넥션 순차 추가 처리량 |
| `BuildAndIterateGraph` | 그래프 생성 + 전체 순회 |
| `FindNode_ByGuid` | LINQ ID 검색 성능 (최악 케이스) |

> `[Params(10, 100, 1000)]`: 각 벤치마크를 3가지 규모로 자동 측정합니다.

### 성능 추이 시각화

CI 실행마다 `benchmarks/history/` 폴더에 날짜별 JSON이 자동 누적됩니다.
아래 명령으로 로컬에서 추이 그래프를 생성할 수 있습니다:

```bash
pip install matplotlib numpy
python3 scripts/visualize_performance.py
# → docs/performance_trend.png 생성
```

![성능 추이](docs/performance_trend.png)

> 그래프가 표시되지 않으면 CI가 아직 실행되지 않은 것입니다.
> 첫 번째 CI 실행 후 `benchmark-results` 아티팩트에서 JSON을 다운로드하거나,
> 로컬에서 `dotnet run -c Release -- --filter "*"` 실행 후 히스토리를 직접 생성하세요.

### 성능 회귀 가드

CI는 이전 빌드 결과를 GitHub Cache에 저장하고, 현재 결과와 자동 비교합니다:

- **허용 한도**: Mean(평균 실행 시간) 또는 Allocated(메모리 할당) 이전 대비 **+10% 이내**
- **비교 도구**: `scripts/compare_benchmarks.py` (소수점 2자리 정밀도)
- **기준선 갱신**: master 브랜치 push + 회귀 없음 조건에서만 갱신

```bash
# 로컬 수동 비교
python3 scripts/compare_benchmarks.py benchmark-baseline.json <current>.json
```

---

## 정적 분석

빌드 시 자동으로 StyleCop + Roslyn 분석기가 실행됩니다.

```bash
# 경고 수 확인
dotnet build --configuration Release 2>&1 | grep ": warning"

# 경고 수 카운트
dotnet build --configuration Release 2>&1 | grep -c ": warning SA"
```

규칙 커스터마이징은 `.editorconfig` 파일을 수정하세요.
현재 목표: SA* 경고 수 점진적 감소 → 0건 달성 ([DEC-005](docs/DECISION_LOG.md) 참조).

---

## CI/CD (GitHub Actions)

PR 또는 `master` 브랜치 push 시 `.github/workflows/verify.yml`이 자동 실행됩니다.

| 단계 | 내용 |
|------|------|
| 빌드 & 린트 | `dotnet build` + 분석기 경고 수 집계 (Error 규칙 위반 시 빌드 실패) |
| 테스트 | `dotnet test` + Cobertura 커버리지 생성 |
| 벤치마크 | `--job Short` 실측 + JsonExporter.Full JSON 출력 |
| 회귀 가드 | 이전 기준선과 비교, 10% 이상 악화 시 빌드 실패 |
| 히스토리 | `benchmarks/history/` 에 날짜별 JSON 자동 누적 (GitHub Cache) |
| 보안 분석 | CodeQL `security-extended` 쿼리로 CWE 취약점 탐지 |
| PR 요약 | 테스트 통과율 / 커버리지 % / 경고 수 / 회귀 여부를 PR에 자동 게시 |

---

## 주요 기능 (현재 구현됨)

- **노드 추가**: 우클릭 컨텍스트 메뉴 → "Add Node"
- **노드 이동**: 드래그 (15px 그리드 스냅)
- **노드 삭제**: 노드 선택 후 `Delete` 키
- **커넥션 생성**: SourceConnector(오른쪽) 드래그 → TargetConnector(왼쪽)에 릴리즈
- **캔버스 패닝**: 우클릭 드래그

---

## 알려진 이슈 및 향후 계획

| 우선순위 | 항목 |
|---------|------|
| High | 노드 이동 시 연결선 잔상 버그 |
| High | 커넥션 삭제 기능 미구현 |
| Medium | 줌 기능 미구현 |
| Medium | 커버리지 목표 80% 달성 |
| Low | 노드 포커스 시 UI 변경 (강조 표시) |
| Low | StyleCop 경고 0건 달성 |

자세한 개발 히스토리는 [docs/DEV_HISTORY.md](docs/DEV_HISTORY.md)를 참조하세요.

---

## 문서

| 문서 | 설명 |
|------|------|
| [docs/PROGRESS_LOG.md](docs/PROGRESS_LOG.md) | 작업 진행 로그 및 로드맵 |
| [docs/DECISION_LOG.md](docs/DECISION_LOG.md) | 기술 의사결정 기록 |
| [docs/DEV_HISTORY.md](docs/DEV_HISTORY.md) | 초기 개발 히스토리 (원본 TODO) |

---

## 라이선스

[LICENSE.txt](LICENSE.txt) 참조
