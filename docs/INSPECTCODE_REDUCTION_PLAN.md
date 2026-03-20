# 


## 1. Document purpose

이 문서는 DagEdit repo의 InspectCode 경고를 운영하는 기준서다.

- GitHub Actions의 CI summary는 현재 경고 현황을 보여주는 현황판이다.
- 이 문서는 그 현황을 바탕으로 실제 배치를 어떻게 운영할지 정하는 운영 기준서다.

이 문서의 목표는 두 가지다.

1. 새 InspectCode 경고 증가를 막는다.
2. 이미 누적된 기존 경고를 10% 단위로 점진 감축한다.

이 문서는 이후 에이전트와 개발자가 InspectCode 관련 작업을 시작하기 전에 먼저 읽고, 배치 목표와 종료 조건을 맞추기 위한 기준으로 사용한다.

## 2. Current baseline

### Confirmed Phase 1 close

Phase 1이 완료되었다. 아래 수치는 CI 확정값(Batch 1-A) + 로컬 확인값(Batch 1-B)을 합산한 최종 기준선이다.

| Item | Value |
| --- | ---: |
| **Current total warnings** | **1027** |
| warning (severity) | 491 |
| note (severity) | 536 |
| Confirmed at commit | `8e1179c` |
| Confirmed at | 2026-03-20 |

**warning severity count 491은 Phase 1 전체에서 변하지 않았다.** 감축 −114 전부 note-severity findings에서 발생. warning regression 없음.

Phase 1 배치 누적 결과:

| Batch | Commit | Total | Delta | Rule | 비고 |
| --- | --- | ---: | ---: | --- | --- |
| — | `7551168` | 1141 | — | — | Phase 1 기준선 (Phase 0 closed) |
| 1-A | `ee8c9ad` | 1077 | −64 | `SuggestVarOrType_SimpleTypes` | CI confirmed |
| 1-B | `8e1179c` | 1027 | −50 | `SuggestVarOrType_BuiltInTypes` | CI confirmed (user declared closed) |

**Phase 1 CLOSED — target ≤1027 정확히 달성.**

감소 흐름 (전체):

| Commit | Total | Delta | 비고 |
| --- | ---: | ---: | --- |
| `e721c0c` | 1195 | — | 이전 기준선 |
| `90e5f74` | 1141 | −54 | using 제거 −29, target-typed new −21, 기타 −4 |
| `7551168` | 1141 | ±0 | Batch A (`.md`/`.yml` only, no .cs change) |
| `ee8c9ad` | 1077 | −64 | Phase 1-A: `SuggestVarOrType_SimpleTypes` |
| `8e1179c` | 1027 | −50 | Phase 1-B: `SuggestVarOrType_BuiltInTypes` |

### New baseline

| Item | Value |
| --- | ---: |
| **Baseline** | **1027** |
| warning (severity) | 491 (고정, regression 없음) |
| Baseline commit | `8e1179c` |

### Remaining top candidates for Phase 2

Phase 1 완료 이후 DagEdit owned rule 잔존 추정값:

| Rule | Estimated remaining | 비고 |
| --- | ---: | --- |
| `SuggestVarOrType_SimpleTypes` | ~198 | Phase 1-A에서 test/bench 처리. 나머지는 production |
| `RedundantNameQualifier` | 48 | 네임스페이스 한정자 제거 |
| `ArrangeThisQualifier` | 34 | `this.` 제거 (SA1101=none) |
| `SuggestVarOrType_BuiltInTypes` | ~23 | Connection.cs(14) + DagEditor.cs(7) 미처리분 |
| `RedundantUsingDirective` | 1 | |

`ArrangeObjectCreationWhenTypeEvident` 잔존 7건은 **external/VCA inherited** (§6 참조). DagEdit 직접 수정 불가.

## 3. Operating principles

다음 원칙은 반드시 지킨다.

- 새 InspectCode 경고 증가 금지
- 기능 수정 배치와 경고 감축 배치를 섞지 않는다
- 기능 배치에서 경고가 증가하면 먼저 delta를 제거한 뒤 배치를 닫는다
- 감축 작업은 작은 배치로 나눈다
- 한 번에 너무 많은 rule을 섞지 않는다
- public API 또는 동작 의미론 변경은 별도 승인 없이 하지 않는다
- gate를 우회하지 않는다
- InspectCode 결과를 숨기거나 기준을 느슨하게 만드는 방식은 사용하지 않는다

## 4. Two work tracks

### A. Regression prevention track

기능 개발 중 새 경고 증가를 막는 트랙이다.

- 기능 작업은 baseline 대비 total 증가가 없어야 종료할 수 있다
- 기능 배치 중 경고가 늘어나면 기능 완료보다 delta 제거가 우선이다
- 기능 배치의 종료 조건은 `baseline 대비 total 증가 없음`이다

운영 규칙:

- 기능 변경 후 InspectCode total을 확인한다
- 증가가 있으면 이번 배치에서 생긴 delta를 먼저 제거한다
- delta 제거 없이 “다음 배치에서 정리”로 넘기지 않는다

### B. Reduction track

기존 누적 경고를 줄이기 위한 별도 품질 개선 트랙이다.

- 기능 작업과 분리해서 수행한다
- 10% 단위 목표를 잡고 작은 배치로 나눠서 줄인다
- low-risk rule부터 먼저 줄이고, 구조 영향이 큰 rule은 뒤로 미룬다

운영 규칙:

- 이번 배치가 reduction batch인지 먼저 명시한다
- 대상 rule은 1~2개만 고른다
- 배치 전/후 수치를 기록한다
- 배치 결과를 다음 baseline으로 남긴다

## 5. 10% reduction targets

### Next phase target

확정 기준선 1141 기준 감축 목표는 아래와 같다.

| Phase | Baseline | Goal | 비고 |
| --- | ---: | --- | --- |
| Phase 0 | 1195 | ≤ 1197 | **CLOSED** — no code changes required |
| Phase 1 | 1141 | ≤ 1027 | **CLOSED** — Batch 1-A (−64) + Batch 1-B (−50) |
| Phase 2 | 1027 | ≤ 924 | 현재 단계 |
| Phase 3 | 924 | ≤ 831 | |
| Phase 4 | 831 | ≤ 748 | |
| Phase 5 | 748 | ≤ 673 | |

운영 해석:

- `Phase 0` 완료. 회복 배치 없이 닫힘.
- `Phase 1` 완료. Batch 1-A (−64) + Batch 1-B (−50) = −114. 1141 → 1027.
- `Phase 2`가 현재 단계. 기준선 1027, 목표 ≤924.
- 각 phase는 이전 phase 완료 수치를 다음 기준선으로 삼는다.

## 6. Rule prioritization strategy

### Ownership split

InspectCode가 분석하는 파일 중 일부는 DagEdit 소유가 아니다.

**DagEdit owned** — 직접 수정 가능:

| Rule | Count | 비고 |
| --- | ---: | --- |
| `SuggestVarOrType_SimpleTypes` | 262 | local variable 선언부 |
| `SuggestVarOrType_BuiltInTypes` | 73 | int/string 키워드 치환 |
| `RedundantNameQualifier` | 48 | 네임스페이스 한정자 |
| `ArrangeThisQualifier` | 34 | this. 제거 |
| `RedundantUsingDirective` | 1 | |

**external/VCA inherited** — DagEdit에서 직접 수정 불가, VCA 저장소에서만 수정 가능:

| Rule | Count | 파일 |
| --- | ---: | --- |
| `ArrangeObjectCreationWhenTypeEvident` | 7 | `external/virtualcanvas-avalonia/src/VirtualCanvas.Core/Geometry/VCRect.cs` (2건), `Spatial/PriorityQuadTree/PriorityQuadTree.cs` (3건), `Spatial/PriorityQuadTree/PriorityQueue.cs` (2건) |

VCA inherited findings는 DagEdit InspectCode total에 포함되지만 DagEdit 배치 대상에서 제외한다. VCA repo에서 별도 처리.

### First group: low-risk, repetitive note-style cleanup

먼저 다룰 후보 (DagEdit owned만):

- `SuggestVarOrType_SimpleTypes` (262)
- `SuggestVarOrType_BuiltInTypes` (73)
- `RedundantNameQualifier` (48)
- `ArrangeThisQualifier` (34)
- `RedundantUsingDirective` (1)

이 순서를 먼저 잡는 이유:

- 대체로 기계적이고 반복적인 수정이 가능하다
- 동작 의미론에 영향을 줄 가능성이 낮다
- 작은 배치로 잘게 쪼개기 쉽다
- Phase 1 목표(≤1027) 달성에 가장 적합하다

### Second group: moderate-risk cleanup with wider surface

그다음 후보:

- `MemberCanBePrivate.Global`
- `UnusedMember.Global`
- `InconsistentNaming`

이 그룹을 두 번째로 두는 이유:

- 파일 간 참조 범위를 확인해야 하는 경우가 많다
- 접근 제한자 축소는 외부 사용/테스트 영향 확인이 필요하다
- 이름 변경은 호출부와 문서/테스트까지 확인해야 한다

### Last group: structural or layout-heavy rules

마지막/주의 후보:

- `SA1201` 등 파일 구조/배치 영향이 큰 rule

이 그룹을 뒤로 미루는 이유:

- diff가 커지기 쉽다
- 리뷰 비용이 높다
- 충돌 가능성이 높고 기능 작업과 쉽게 섞인다
- 동작 변화는 없어도 파일 구조 churn이 커서 운영 효율이 낮다

## 7. Batch operating model

모든 InspectCode 배치는 아래 절차로 운영한다.

### Before the batch

- 현재 baseline total 기록
- 이번 배치 목표 수치 기록
- 이번 배치에서 줄일 rule 1~2개 선택
- 기능 배치인지 reduction batch인지 명시

### During the batch

- rule 범위를 넓히지 않는다
- 불필요한 구조 정리는 섞지 않는다
- public API/동작 의미론 변경은 별도 승인 없이는 하지 않는다

### After the batch

- 전/후 total 수치 기록
- 줄인 rule과 남은 수치 기록
- 만약 total이 증가했으면 원인을 분석하고 delta를 먼저 제거한다

### Required batch checklist

- [ ] baseline before 기록
- [ ] target for this batch 기록
- [ ] targeted rules 1~2개만 선택
- [ ] 전/후 수치 기록
- [ ] 증가 여부 확인
- [ ] 증가 시 delta 우선 제거
- [ ] 결과 보고 형식 통일

## 8. Agent operating rules

이 문서를 읽는 에이전트는 아래 규칙을 따른다.

- 작업 시작 전 이 문서를 먼저 읽는다
- 기능 배치에서는 새 InspectCode 경고를 늘리지 않는다
- 증가가 생기면 기능 배치를 닫기 전에 delta를 먼저 제거한다
- reduction batch에서는 이번 배치 목표와 대상 rule을 먼저 명시한다
- 한 번에 1~2개 rule만 대상으로 잡는다
- 결과 보고에는 현재 총 경고 수, 이전 대비 증감, 줄인 rule, 남은 리스크를 반드시 포함한다
- gate 우회, 결과 숨기기, 기준 완화 제안은 하지 않는다

## 9. Recommended reporting format

InspectCode 관련 배치 보고는 아래 형식을 기본 템플릿으로 사용한다.

1. Confirmed facts
2. Baseline before
3. Target for this batch
4. Rules targeted
5. Files changed
6. Delta after
7. Validation results
8. Deferred risks / next step

## 10. Immediate next actions

Phase 1은 닫혔다. 현재 단계는 Phase 2다.

- **Baseline**: 1027 (commit `8e1179c`)
- **Target**: ≤ 924 (−103 필요)
- **warning severity**: 491 고정, no regression

Phase 2 배치 권장 순서:

1. `SuggestVarOrType_SimpleTypes` 잔존 ~198건 — production 파일 (Connection.cs, DagEditor.cs 등). 단, UI 핵심 파일은 소규모 배치로 나눌 것.
2. `RedundantNameQualifier` (48건) — System.Guid.NewGuid() 등 불필요한 네임스페이스 한정자 제거. 기계적이고 안전.
3. `ArrangeThisQualifier` (34건) — `this.` 제거. SA1101=none 설정 하에 안전.
4. `SuggestVarOrType_BuiltInTypes` 잔존 ~23건 — Connection.cs(14) + DagEditor.cs(7).
5. `RedundantUsingDirective` (1건) — 마지막 잔존 using 정리.

> Phase 2 배치는 별도 승인 후 시작한다. 이 문서 업데이트만으로 Phase 2 코드 수정을 시작하지 않는다.
