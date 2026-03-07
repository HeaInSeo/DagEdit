# Integration Contract: DagEdit ↔ VCA

```
Contract-ID:      IC-001
Version:          0.1.0
Status:           Draft
Canonical-Repo:   HeaInSeo/virtualcanvas-avalonia
Canonical-Path:   docs/INTEGRATION_CONTRACT.md
Mirrored-In:      HeaInSeo/DagEdit / docs/INTEGRATION_CONTRACT.md
Last-Updated:     2026-03-07
Last-Synced:      2026-03-07 (initial mirror — canonical not yet committed in VCA repo)
Change-Type:      Minor
Mirror-Status:    Pending Sync (awaiting canonical creation in VCA repo)
```

> **Mirror 문서 규칙**: 이 파일은 VCA repo `docs/INTEGRATION_CONTRACT.md` 의 mirror다.
> 의미 변경 없는 수정(Patch)은 이 파일에 직접 반영 가능하다.
> Minor / Major 변경은 canonical source 에 먼저 반영된 후 이 파일을 동기화한다.
> 변경이 필요해 보이면 이 파일 또는 `INTEGRATION_EXECUTION_DAGEDIT.md` 의 `[Proposed]` 섹션에 제안으로만 남긴다.

---

## 1. Purpose

DagEdit 와 VCA(VirtualCanvas-Avalonia) 가 장기적으로 안전하게 통합되기 위한
공유 계약(shared contract)을 기록한다.

이 계약은 다음을 정의한다.
- 두 시스템이 공유하는 좌표계 / 뷰포트 불변성
- 각 시스템이 소유하는 책임 경계
- 기능의 분류 기준 (VCA 승격 / DagEdit 유지 / 보류)
- 단계적 도입 순서와 통합 전제 조건
- 계약 변경 절차

이 계약을 위반하는 변경은 어느 repo 에서도 허용되지 않는다.

---

## 2. Fixed Contracts

아래 계약은 양측 모두에서 변경 불가(frozen)다.
변경이 필요하면 Major change 절차를 밟아야 한다.

### 2a. Coordinate System

```
world → screen:   screen = world * scale − offset
screen → world:   world  = (screen + offset) / scale
```

- `offset` ≡ DagEdit `ViewportLocation` ≡ VCA `Offset` (Avalonia `Point`)
- `scale`  ≡ DagEdit `ViewportScale`   ≡ VCA `Scale`  (`double`)
- 월드 좌표가 모든 데이터의 저장·전달 형식이다. 스크린 좌표는 렌더링·입력 처리 시의 파생값이다.

검증: `tests/DagEdit.Tests/SelectionRectTests.cs` — `TransformFormula_MatchesVcaFormula`,
`InverseFormula_MatchesVcaActualViewboxOrigin`

### 2b. Viewport State Single Source of Truth

DagEdit 에서 viewport 상태의 source of truth 는 `DagEditorViewModel` 이다.

```
DagEditorViewModel.ViewportLocation  ≡  future VCA.Offset
DagEditorViewModel.ViewportScale     ≡  future VCA.Scale
```

VCA 통합 시: `VCA.Offset` ↔ `DagEditorViewModel.ViewportLocation` 양방향 바인딩.
자세한 sync 아키텍처는 `docs/viewport-contract.md` 참조.

### 2c. Staged Adoption Order

```
1. Viewer   — VCA 를 read-only 뷰어로 삽입, DagEdit 상태와 분리
2. Hybrid   — VCA items host 로 교체, DagEdit editor semantics 유지
3. Full     — VCA primitive 위에서 DagEdit 전체 editor 동작
```

순서를 건너뛰거나 역행할 수 없다.

---

## 3. Responsibility Boundary

### VCA 소유 (rendering / virtualization infra)

- viewport math primitive (Offset, Scale, ActualViewbox 계산)
- spatial query / visible range query (`ISpatialIndex`, `GetItemsIntersecting`)
- realize / virtualize lifecycle (아이템 가시성 기반 컨트롤 생성·해제)
- batching 계약 (bounds 변경 일괄 처리)
- bounds 변경 반영 계약
- pinning primitive (드래그·선택 중 가상화 억제)
- visual factory / host adapter seam (아이템 → 컨트롤 변환 추상)
- semantic zoom primitive

### DagEdit 소유 (editor state / interaction)

- selection policy (단일·다중·Rect 선택 규칙)
- multi-selection modifier key 해석 (Shift, Ctrl 등)
- selection rectangle overlay UI (`_selectionRect`, `FinalizeSelection`)
- undo / redo (`UndoRedoStack`, `IUndoableCommand`)
- node / port / connection 의미 (`DagNode`, `DagConnection`, `DagItems`)
- keyboard shortcut 정책 (`EditorGestures`)
- interaction UX 품질 (snap-to-grid, connector snap, context menu)
- 상용 UI 정책 및 DagEditorViewModel 상태 관리

### 경계 원칙

- VCA 는 "어떻게 보여줄 것인가"를 결정한다.
- DagEdit 는 "무엇을 보여줄 것인가"와 "사용자 행동을 어떻게 해석할 것인가"를 결정한다.
- VCA 에 selection semantics / undo history / editor state 를 넘기지 않는다.
- DagEdit 가 concrete canvas 구현(DagEditorCanvas)에 영구적으로 묶이지 않는다.

---

## 4. Classification

### VCA 승격 대상

아래 기능은 장기적으로 VCA 가 primitive 를 제공하고 DagEdit 가 사용하는 형태가 된다.

| 기능 | 현재 상태 | 통합 단계 |
|------|-----------|-----------|
| Viewport math (ScreenToWorld / WorldToScreen) | DagEdit `ViewportTransform` | Hybrid — VCA primitive 호출로 교체 |
| ActualViewbox / visible world rect | DagEdit 미구현 | Hybrid |
| Spatial query (node hit-test / selection query) | DagEdit `FinalizeSelection` O(n) 루프 | Hybrid — `ISpatialIndex.Query(worldRect)` |
| Realize / virtualize lifecycle | DagEdit 없음 (모든 노드 항상 live) | Full |
| Bounds update batching | DagEdit 없음 | Full |
| Pinning primitive | DagEdit 없음 | Hybrid (drag / selection 중) |
| Visual factory seam | DagEdit `CreateContainerForItemOverride` | Hybrid |
| Semantic zoom primitive | 미구현 | Full |

### DagEdit 유지

| 기능 | 이유 |
|------|------|
| SelectionPolicy / Rect selection | editor semantics, modifier key 해석 포함 |
| UndoRedoStack / IUndoableCommand | editor command history |
| DagNode / DagConnection / DagItems | graph domain model |
| Connector snap logic | interaction UX |
| Grid snap | interaction UX |
| Context menu / EditorGestures | keyboard / UX policy |
| DagEditorViewModel viewport state | editor state SoT |

### 보류 (미결정)

| 항목 | 이유 |
|------|------|
| VCA `SelectedItem` ↔ DagEdit `Selection` 연동 방식 | Selection 모델 설계 불확실 |
| Pinning API 구체 형식 | VCA API 미확정 |
| Semantic zoom parity 범위 | DagEdit 요구사항 미정 |
| Template visual factory parity 범위 | VCA visual factory API 미확정 |
| Hybrid 단계에서 무엇을 먼저 VCA 로 올릴지 | 구체 순서 미결 |

---

## 5. Staged Adoption Path

### Phase 1: Viewer (read-only)

**전제 조건**:
- DagEdit 그래프 데이터를 VCA 에 read-only 로 공급할 수 있는 어댑터 존재
- VCA 의 Offset / Scale 이 DagEdit 수식과 동일함을 테스트로 확인 (완료 ✅)

**이 단계에서 하는 것**:
- VCA 를 DagEditor 옆에 별도 뷰어로 연결 (DagEdit 편집 기능과 분리)
- ViewportLocation ↔ VCA.Offset 단방향 동기화 검증

**이 단계에서 하지 않는 것**:
- DagEditorCanvas 제거
- 편집 이벤트(drag, connection) VCA 경유

### Phase 2: Hybrid (items host 교체)

**전제 조건**:
- Phase 1 완료 + Viewer 안정성 확인
- VCA `pinning primitive` API 확정
- VCA visual factory seam 이 DagEdit 의 `CreateContainerForItemOverride` 계약을 수용 가능

**이 단계에서 하는 것**:
- `DagEditorCanvas` → `VirtualCanvas` 교체 (items host 역할)
- `ViewportLocation` ↔ `VCA.Offset` 양방향 바인딩
- `FinalizeSelection` 루프 → `ISpatialIndex.Query(worldRect)` 교체
- 드래그·선택 중 VCA pinning 활성화

**이 단계에서 하지 않는 것**:
- UndoRedoStack / SelectionPolicy VCA 이전
- DagEditorViewModel 제거

### Phase 3: Full Editor

**전제 조건**:
- Phase 2 완료 + 편집 UX 안정성 확인
- VCA realize / virtualize lifecycle 이 DagEdit node lifecycle 과 호환

**이 단계에서 하는 것**:
- VCA 모든 primitive 위에서 DagEdit editor 전체 동작
- 필요 시 DagEditorCanvas 완전 제거

---

## 6. Current Blockers and Risks

| ID | 리스크 | 영향 단계 | 심각도 |
|----|--------|-----------|--------|
| R-A | 노드 드래그 중 SpatialIndex update 전략 미결 | Hybrid | High |
| R-B | 선택·드래그 중 virtualization pinning 정책 미결 | Hybrid | High |
| R-C | DagEdit 의 `Node` control model (ContentControl lifecycle) 과 VCA direct child management 충돌 가능성 | Hybrid | High |
| R-D | VCA visual factory / host adapter seam API 미확정 | Hybrid | Medium |
| R-E | `SelectedItem` 연동 방식 — DagEdit `ISelectionModel` vs VCA 자체 선택 상태 | Hybrid | Medium |

---

## 7. Decision Rules

이 계약과 상충하는 변경을 제안할 때 아래 기준을 따른다.

1. **좌표계 수식 변경**: 양측 repo 의 테스트가 모두 통과해야 한다. 수식 변경은 Major.
2. **책임 경계 이전**: "VCA 로 넘기는 이유가 rendering/virtualization infra 때문인가?" 아니면 "editor semantics 때문인가?" 를 먼저 판단한다.
3. **단계 건너뜀**: Phase 순서를 건너뛸 수 없다. 전제 조건이 충족되지 않으면 다음 단계 시작 금지.
4. **보류 항목 확정**: 사용자 승인 + canonical source 반영이 먼저다. 개별 repo 에서 단독 확정 금지.

---

## 8. Contract Update Protocol

| Change-Type | 설명 | 절차 |
|-------------|------|------|
| Patch | 오타, 예시 추가, 가독성 개선 — 의미 변화 없음 | 어느 repo 에서도 직접 반영 가능 |
| Minor | 책임 분류 보강, 단계 조정, primitive 후보 추가 | canonical repo 에 먼저 반영 → mirror sync |
| Major | 좌표계 계약, 책임 경계, staged adoption path, 분류 체계 의미 변경 | 사용자 승인 → canonical 반영 → mirror sync |

---

## 9. Sync Rules for Agents

- mirror 문서에서 의미 변경이 필요해 보이면, `INTEGRATION_EXECUTION_DAGEDIT.md` 의 `[Proposed Change]` 섹션에 제안으로만 남긴다. 직접 확정하지 않는다.
- mirror 문서를 수정할 때 `Last-Updated` 와 `Mirror-Status` 를 갱신한다.
- canonical source 에 변경이 반영되면 mirror 를 sync 하고 `Last-Synced` 를 갱신한다.
- 이 문서의 Section 2 (Fixed Contracts) 는 사용자 승인 없이 수정하지 않는다.

---

## 10. Contract Change Log

| Date | Version | Change-Type | Description |
|------|---------|-------------|-------------|
| 2026-03-07 | 0.1.0 | Minor | Initial mirror creation. Coordinate system verified identical (DagEdit ↔ VCA). Responsibility boundary and classification defined. Staged adoption path established. |

---

## 11. Non-Goals / Forbidden Directions

- DagEdit 가 concrete canvas 구현체(`DagEditorCanvas`)에 영구적으로 묶이는 설계를 고착화하지 않는다.
- editor semantics(selection policy, undo/redo, keyboard UX)를 VCA 에 이전하지 않는다.
- Phase 순서(viewer → hybrid → full)를 역행하거나 건너뛰지 않는다.
- 이 계약의 Section 2 (Fixed Contracts) 를 사용자 승인 없이 변경하지 않는다.
- 보류 항목을 단독으로 확정하지 않는다.
