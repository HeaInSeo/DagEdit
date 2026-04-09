# Lint Sprint Execution Plan

> DagEdit 린트/정적분석 경고 감축 스프린트의 실행 기준서. 이 문서는 스프린트 종료 시마다 실제 결과로 갱신한다.

---

## 1. Purpose

이 문서는 다음 두 가지를 동시에 관리하기 위한 실행 문서다.

1. GitHub Actions 기준선과 회귀 게이트를 안정적으로 유지한다.
2. 누적된 린트/정적분석 경고를 스프린트 단위로 계획하고 감축한다.

운영 원칙:

- 회귀 게이트는 우회하지 않는다.
- GitHub에 올라간 배치에서 회귀 검사 실패는 실제 실패로 취급한다.
- 실패를 피하기 위해 기준을 느슨하게 하지 않는다.
- 대신 기준선 수집 방식과 집계 로직을 정확하게 유지해 거짓 실패를 없앤다.
- 이 문서는 각 스프린트 종료 시 실제 수치와 결과로 갱신한다.

## 2. Current baseline

확인 기준:

- InspectCode baseline artifact: [warning-count.json](/opt/dotnet/src/github.com/HeaInSeo/DagEdit/warning-count.json)
- CI workflow: [verify.yml](/opt/dotnet/src/github.com/HeaInSeo/DagEdit/.github/workflows/verify.yml)
- CI workflow: [inspectcode.yml](/opt/dotnet/src/github.com/HeaInSeo/DagEdit/.github/workflows/inspectcode.yml)

현재 확인된 수치:

| Item | Value |
| --- | ---: |
| InspectCode total | 981 |
| InspectCode warning severity | 445 |
| InspectCode note severity | 536 |
| Baseline commit | `9e4476f` |
| Generated at | 2026-03-20T11:40:12Z |

현 시점 관찰:

- `InspectCode`는 `warning-count.json`을 기준으로 회귀를 검사한다.
- `Verify`는 빌드 출력에서 `SA`, `CS`, `IDE`만 합산하고 있어 `CA` 계열이 총경고에서 누락될 수 있다.
- PR 실행에서는 회귀 비교 기준 브랜치 선택이 불안정할 수 있어 baseline fetch를 명시적으로 정리할 필요가 있다.

## 3. Sprint roadmap

기본 cadence는 1주 스프린트다.

| Sprint | 기간 | 상태 | 핵심 목표 | 종료 기준 |
| --- | --- | --- | --- | --- |
| S0 | 2026-04-13 ~ 2026-04-17 | In Progress | 측정 체계/기준선 정렬 | GitHub Actions 집계 정합성 확보, PR baseline 안정화 |
| S1 | 2026-04-20 ~ 2026-04-24 | Planned | 기계적 포맷/레이아웃 경고 1차 정리 | 저위험 StyleCop 60~90건 감소 |
| S2 | 2026-04-27 ~ 2026-05-01 | Planned | 선언 순서/파일 구조 정리 | `SA1201/1202/1402/1649` 중심 정리 |
| S3 | 2026-05-04 ~ 2026-05-08 | Planned | API 표면/네이밍/미사용 멤버 정리 | `CA1515`, `MemberCanBePrivate`, `UnusedMember` 축소 |
| S4 | 2026-05-11 ~ 2026-05-15 | Planned | 수명주기/null/예외 경고 정리 | `CA1062/1063/2213/1031/1305` 우선 해소 |

## 4. Sprint detail

### S0. Measurement and baseline hardening

목표:

- `Verify`가 실제 빌드 경고 총량을 정확히 집계하도록 수정
- `Verify`가 이전 성공 실행 대비 빌드 경고 회귀를 검사하도록 수정
- `InspectCode`가 PR에서도 안정적인 baseline을 찾도록 수정
- baseline/게이트 정책을 문서화

작업 범위:

- `.github/workflows/verify.yml`
- `.github/workflows/inspectcode.yml`
- `docs/LINT_SPRINT_EXECUTION_PLAN.md`
- `docs/PROGRESS_LOG.md`

완료 기준:

- `Verify` summary의 총경고가 `CA*` 누락 없이 계산된다.
- `Verify`가 `build-warning-count.json` artifact를 남기고 이전 성공 실행 대비 회귀를 검사한다.
- `InspectCode`의 이전 baseline fetch가 PR에서 target branch 기준으로 동작한다.
- baseline 부재 시 스킵, 증가 시 실패 정책이 문서와 workflow에서 일치한다.

### S1. Low-risk mechanical cleanup

대상 규칙:

- `SA1515`
- `SA1516`
- `SA1518`
- `SA1413`
- `SA1116`
- `SA1117`
- `SA1005`
- `SA1028`

전략:

- 기능 변경 없이 포맷/개행/후행 쉼표/파라미터 줄바꿈 위주로 처리
- 파일 churn이 작고 리뷰가 쉬운 배치로 나눔

### S2. Structural StyleCop cleanup

대상 규칙:

- `SA1201`
- `SA1202`
- `SA1203`
- `SA1402`
- `SA1649`

전략:

- 멤버 정렬과 파일 분리 중심
- 한 번에 1~2개 파일군만 묶어 충돌 범위를 제한

### S3. API surface and ownership cleanup

대상 규칙:

- `CA1515`
- `MemberCanBePrivate.Global`
- `UnusedMember.Global`
- `InconsistentNaming`

전략:

- app 내부 전용 타입은 `internal` 축소 검토
- 테스트 접근성은 `InternalsVisibleTo` 유지 조건으로 확인

### S4. Behavioral-risk quality cleanup

대상 규칙:

- `CA1062`
- `CA1063`
- `CA2213`
- `CA1031`
- `CA1305`
- `CA1859`

전략:

- `Dispose` 패턴, null guard, culture-aware formatting, broad catch 정리
- 반드시 테스트 동반

## 5. GitHub Actions operating policy

### Verify

역할:

- 빌드 성공 여부 확인
- 빌드 경고 현황판 제공
- 테스트/커버리지/벤치마크/성능 회귀 확인

정책:

- summary 숫자는 실제 빌드 로그와 일치해야 한다.
- 총경고는 특정 prefix만 더한 값이 아니라 실제 코드 경고를 반영해야 한다.
- 이전 성공 실행 대비 `SA`, `CA`, `CS`, `IDE`, total 증가가 있으면 실패한다.
- 성능 회귀 가드는 실패 시 실패로 본다.

### InspectCode

역할:

- 전체 정적 진단 baseline 관리
- 이전 성공 실행 대비 경고 증가 금지

정책:

- baseline 없음: pass
- total 증가: fail
- warning severity 증가: fail
- note/info는 추적하되 fail 조건은 아님

## 6. Sprint update log

## 7. Sprint reporting rule

스프린트 종료 보고에는 아래 항목을 반드시 포함한다.

1. 스프린트명과 기간
2. 시작 기준선과 종료 기준선
3. 실제 수행 내용
4. 남은 리스크 또는 미완료 항목
5. 추천 다음 스프린트
6. 추천 이유

추천 다음 스프린트는 반드시 하나를 명시한다. 이유는 아래 원칙 중 최소 하나와 연결해 기록한다.

- 경고 감축 효율이 가장 높음
- 회귀 리스크가 가장 낮음
- 현재 CI 게이트 안정화에 직접 기여함
- 다음 구조 변경 전에 선행되어야 함
- 테스트 보강 없이는 뒤 스프린트 진행이 위험함

### S0

- 상태: Completed
- 계획 값: workflow 집계 정합성 수정, `Verify`/`InspectCode` baseline branch 정리, 문서화
- 실제 결과:
  - `Verify` total warning 집계를 실제 build warning line 기준으로 보정
  - `Verify`에 `build-warning-count.json` artifact 및 회귀 게이트 추가
  - `InspectCode`가 PR에서 target branch 기준으로 baseline을 탐색하도록 보정
  - 스프린트 실행 문서 및 종료 보고 규칙 추가
- 종료 보고:
  - 스프린트명: S0. Measurement and baseline hardening
  - 기간: 2026-04-09 ~ 2026-04-09
  - 시작 기준선: InspectCode baseline 981, Verify build summary는 `CA*` 누락 가능 상태
  - 종료 기준선: InspectCode baseline fetch 정렬 완료, Verify build warning baseline artifact/gate 추가 완료
  - 남은 리스크:
    - 실제 GitHub Actions 실행 결과에서 baseline artifact가 정상적으로 이어지는지 1회 확인 필요
    - `Verify`와 `InspectCode`는 각각 다른 기준선을 가지므로 수치 해석 문맥을 계속 분리해야 함
  - 추천 다음 스프린트: S1
  - 추천 이유:
    - 현재 CI 게이트 안정화가 끝나 저위험 감축을 바로 시작할 수 있음
    - 포맷/레이아웃 규칙은 경고 감축 효율이 높고 회귀 리스크가 가장 낮음

### S1

- 상태: In Progress
- 실제 결과:
  - 저위험 정리 8개 배치 수행
  - 대상 파일: `App.axaml.cs`, `TemplateLayoutCanvas.cs`, `TargetConnector.cs`, `MultiGesture.cs`, `DagItems.cs`, `Connection.cs`, `Extension.cs`, `BaseNode.cs`, `SourceConnector.cs`, `Program.cs`, `MainWindow.axaml.cs`, `BrushResources.cs`, `DagEditor.cs`, `Connector.cs`, `Node.cs`, `EditorContextFlyout.cs`, `PendingConnectionEventArgs.cs`, `PointerGesture.cs`, `ILocatable.cs`, `EditorMenuItem.cs`, `UndoRedoStack.cs`, `ConnectionChangedEventArgs.cs`, `NodeViewItem.cs`
  - 수행 내용:
    - BOM 제거, trailing whitespace 제거, comment spacing 보정
    - single-line block 확장, constructor initializer 줄바꿈, method signature/argument 정렬
    - 일부 멤버 순서 재배치와 공개 멤버 순서 보정
    - `SA1201/1202/1203`, `CA1725`, `SA1407`의 소규모 구조 정리 시작
    - `DagEditor`/`BaseNode` 백킹 필드 및 상태 배치 정리
    - `SourceConnector` release 경로 단순화 및 `PendingConnection` 필드 섹션 재배치
    - 클린 빌드 기준선 재검증 및 `BaseNode`/`DagEditorCanvas`/`Connection` 순서 경고 추가 정리
    - `SourceConnector`/`TargetConnector`/`PendingConnection` constructor-order 정리
  - 현재 로컬 검증: `dotnet clean DagEdit.sln -c Release` 성공 후 `dotnet build DagEdit.sln -c Release --no-restore` 성공, `186 Warning(s)`, `0 Error(s)`

### S2

- 상태: Planned
- 실제 결과: 미착수

### S3

- 상태: Planned
- 실제 결과: 미착수

### S4

- 상태: Planned
- 실제 결과: 미착수
