# Integration Execution Plan — DagEdit

> DagEdit repo 전용 실행 문서.
> DagEdit 가 VCA 와 안전하게 통합되기 위해 이 repo 에서 수행할 작업 순서와 판단 기준을 기록한다.
> 이 문서는 living document 다 — 매 작업 완료 시 Step Log 를 갱신한다.

---

## 1. Purpose

`docs/INTEGRATION_CONTRACT.md` 는 DagEdit 와 VCA 가 공유하는 상위 계약을 정의한다.
이 문서는 그 계약 아래에서 **DagEdit 가 구체적으로 무엇을, 어떤 순서로, 어떤 판단 기준으로 작업하는지** 를 기록한다.

- 이 문서를 읽으면 DagEdit 의 다음 single small diff 를 판단할 수 있어야 한다.
- 새 기능을 추가하기 전에 이 문서의 `Decision Checklist` 를 통과하는지 확인한다.
- 계약 계층의 의미 변경이 필요하면 이 문서의 `[Proposed Change]` 섹션에만 남긴다.

---

## 2. Current State Summary

| 항목 | 현재 상태 |
|------|-----------|
| Tests | 120개, 100% 통과 |
| Architecture | `DagEditor` (SelectingItemsControl) → `DagEditorCanvas` (custom Canvas) → `Node`, `Connection` |
| Viewport | `DagEditorViewModel.ViewportLocation/Scale` (SoT), `DagEditorCanvas.RenderTransform` 적용 |
| Coordinate math | `ViewportTransform.ScreenToWorld / WorldToScreen`, VCA 수식과 동일 확인 ✅ |
| Undo/Redo | `UndoRedoStack` + `IUndoableCommand` 완료 |
| Selection | Selection rectangle (world-coord 기반), `SelectingItemsControl.Selection` 사용 |
| Node drag | grid snap, `MoveNodeCommand`, `ConnectionChangedEvent` 경유 앵커 갱신 |
| Connector snap | `GetClosestControlUnderPointer` (bounds center 최근접) |
| Spatial query | `FinalizeSelection()` O(n) 루프 — VCA `ISpatialIndex` 교체 대상 |
| Virtualization | 없음 — 모든 노드 항상 live |
| **Viewer projection seam** | `NodeViewItem : ISpatialItem` ✅ — DagNode → VCA item 투영 경로 검증 완료 |
| VCA 통합 | Phase 1 Viewer adapter spike 완료, IVisualFactory / SpatialIndex 연결은 미검증 |

---

## 3. DagEdit Role in Integration

DagEdit 는 통합에서 **editor** 역할을 유지한다.

- DagEdit 는 사용자 행동을 해석하고(selection, drag, keyboard), 그 결과를 graph state(Dag, DagNode, DagConnection) 로 변환한다.
- DagEdit 는 VCA 에 "무엇을 어디에 그릴지" 를 공급한다. VCA 는 그 공급된 데이터를 "어떻게 효율적으로 표시할지" 를 결정한다.
- DagEdit 는 VCA 의 canvas primitive / virtualization 인프라를 **사용**하지, VCA 에 editor semantics 를 **위임**하지 않는다.

---

## 4. What Must Stay in DagEdit

아래 기능은 어떤 통합 단계에서도 DagEdit 에 남는다.

| 기능 | 이유 |
|------|------|
| `UndoRedoStack` / `IUndoableCommand` / 모든 Command 클래스 | editor command history — VCA 관심사 아님 |
| `DagEditorViewModel` — ViewportLocation/Scale SoT | editor state; VCA.Offset/Scale 과 바인딩 연결점 |
| Selection policy (단일/다중/Rect 선택 규칙) | modifier key 해석 포함, editor semantics |
| Selection rectangle overlay UI | editor UX — VCA 의 `ISpatialIndex.Query` 는 query primitive 만 제공 |
| `DagNode` / `DagConnection` / `DagItems` / `Dag` | graph domain model |
| `EditorGestures` — keyboard shortcut 정책 | editor UX |
| `ConnectorSnap` / grid snap logic | interaction UX quality |
| Context menu / `EditorContextFlyout` | editor UX |

---

## 5. What DagEdit Expects from VCA

DagEdit 가 통합을 위해 VCA 에서 필요로 하는 계약.

| 기대 | 단계 | 현재 상태 |
|------|------|-----------|
| `Offset` / `Scale` 이 DagEdit viewport 수식과 동일 | Viewer | 확인 완료 ✅ |
| `ActualViewbox` — 현재 화면에 보이는 world rect | Hybrid | VCA 구현 있음, 매핑 검증 필요 |
| `ISpatialIndex.Query(worldRect)` — world rect 교차 아이템 조회 | Hybrid | VCA 구현 있음 |
| Pinning primitive — 드래그·선택 중 virtualization 억제 | Hybrid | API 미확정 |
| Visual factory / host adapter — DagEdit 아이템 → VCA 아이템 변환 | Hybrid | API 미확정 |
| Realize / virtualize lifecycle 계약 | Full | 설계 미정 |

---

## 6. Decoupling Plan

DagEdit 가 `DagEditorCanvas` 직접 의존을 줄이기 위한 방향.

현재 DagEdit 는 다음에 직접 의존한다.
- `DagEditorCanvas` (custom Canvas, `PART_ItemsHost`)
- `DagEditorCanvas.RenderTransform` (TransformGroup — Scale + Translate)
- `HandleLoaded` 의 좌표계 일치 검사 (`Extension.IsCanvasMatched`)

목표 구조:
```
DagEditor
  └── [host abstraction / VCA seam]
        ├── DagEditorCanvas     (현재: 직접 사용)
        └── VirtualCanvas       (미래 Hybrid: 교체)
```

단계:
1. `DagEditorCanvas` 와 `VirtualCanvas` 가 공유할 수 있는 최소 인터페이스/추상을 식별한다. (Hybrid 진입 전)
2. `ViewportLocation/Scale` 은 이미 ViewModel 기반 추상화 완료 — 교체 준비 ✅
3. `FinalizeSelection()` 의 worldRect 계산은 이미 분리 완료 — `ISpatialIndex.Query` 교체만 남음 ✅
4. `HandleLoaded` 의 `Extension.IsCanvasMatched` 는 VCA 교체 시 업데이트 필요

---

## 7. Phase Plan

### Phase 1: Viewer

**목표**: VCA 를 DagEdit 옆에 read-only 뷰어로 연결한다. 편집 기능과 완전히 분리.

**전제 조건**:
- [x] 좌표계 수식 일치 확인 (완료 ✅ — `SelectionRectTests.TransformFormula_MatchesVcaFormula`)
- [x] DagEdit 데이터 → VCA item 투영 seam 확인 (완료 ✅ — `NodeViewItem : ISpatialItem`)
- [ ] VCA repo 에 `INTEGRATION_CONTRACT.md` canonical 생성 및 sync
- [ ] VCA 쪽에서 `IVisualFactory` 구현으로 NodeViewItem → Control 실현 PoC
- [ ] `SpatialIndex.Insert(NodeViewItem)` 연결 — 실제 렌더링 경로 확인

**이 단계에서 DagEdit 가 한 일** (완료):
- `NodeViewItem : ISpatialItem` — DagNode read-only projection seam 정의
- `VirtualCanvas.Core` 프로젝트 참조 추가 (UI 의존 없음)
- Viewport 매핑 규칙 문서화: `ViewportLocation == VCA.Offset`, `ViewportScale == VCA.Scale`

**이 단계에서 DagEdit 가 더 할 일** (VCA PoC 결과 대기 후):
- `DagEditorViewModel.ViewportLocation` → `VCA.Offset` 단방향 동기화 (VCA PoC 확인 후)

**금지**:
- DagEditorCanvas 제거
- 편집 이벤트 VCA 경유
- `NodeViewItem` public API 승격 (VCA PoC 결과 보기 전)
- `IViewportHost` 같은 큰 공용 인터페이스 지금 확정

---

### Phase 2: Hybrid

**목표**: `DagEditorCanvas` → `VirtualCanvas` 로 items host 교체. DagEdit editor semantics 유지.

**전제 조건**:
- [ ] Phase 1 완료 및 Viewer 안정성 확인
- [ ] VCA pinning API 확정
- [ ] VCA visual factory seam 이 `CreateContainerForItemOverride` 계약 수용 가능 확인

**이 단계에서 DagEdit 가 할 일**:
- `DagEditorCanvas` → `VirtualCanvas` 교체 (items host)
- `ViewportLocation` ↔ `VCA.Offset` 양방향 바인딩
- `FinalizeSelection` 루프 → `ISpatialIndex.Query(worldRect)` 교체
- 드래그·선택 중 VCA pinning 활성화

**금지**:
- UndoRedoStack / SelectionPolicy VCA 이전
- DagEditorViewModel 제거

---

### Phase 3: Full Editor

**목표**: VCA 모든 primitive 위에서 DagEdit editor 전체 동작.

**전제 조건**:
- [ ] Phase 2 완료 및 편집 UX 안정성 확인
- [ ] VCA realize / virtualize lifecycle 이 DagEdit node lifecycle 과 호환 확인

**이 단계에서 DagEdit 가 할 일**:
- VCA virtualization 으로 대규모 그래프 성능 확보
- 필요 시 `DagEditorCanvas` 완전 제거

---

## 8. Risks and Blockers

| ID | 리스크 | 영향 단계 | 심각도 | DagEdit 대응 |
|----|--------|-----------|--------|--------------|
| R-A | 노드 드래그 중 SpatialIndex update 전략 미결 | Hybrid | High | 드래그 중 pinning 으로 억제 or 드래그 완료 시 일괄 갱신 검토 |
| R-B | 선택·드래그 중 virtualization pinning 정책 미결 | Hybrid | High | VCA pinning API 확정 후 `MoveNodeCommand` 에 반영 |
| R-C | `Node` (ContentControl) lifecycle ↔ VCA direct child management 충돌 | Hybrid | High | Phase 1 PoC 에서 조기 검증 필요 |
| R-D | `HandleLoaded` 의 `IsCanvasMatched` — VCA 교체 시 업데이트 필요 | Hybrid | Low | DagEditorCanvas 제거 전 수정 |

---

## 9. Decision Checklist for Future Changes

DagEdit 에 변경을 가하기 전 아래 질문을 확인한다.

```
[ ] 이 변경이 DagEdit 의 책임(editor semantics / interaction UX)에 속하는가?
    → No → VCA 쪽에서 할 일인지 검토한다.

[ ] 이 변경이 viewport 수식(Fixed Contract 2a)에 영향을 주는가?
    → Yes → SelectionRectTests, ViewportTransformTests 가 모두 통과해야 한다.

[ ] 이 변경이 DagEditorCanvas 에 대한 직접 의존을 추가하는가?
    → Yes → host abstraction 을 통해 해결할 방법을 먼저 검토한다.

[ ] 이 변경이 보류(Hold) 항목을 단독 확정하는가?
    → Yes → Proposed Change 로만 남긴다. 직접 구현하지 않는다.

[ ] 이 변경이 Phase 순서를 건너뛰는가?
    → Yes → 허용되지 않는다.

[ ] 이 변경이 Selection policy / UndoRedo / graph domain model 을 VCA 로 이전하는가?
    → Yes → DagEdit 소유 영역이다. 이전하지 않는다.
```

---

## 10. Step Log

| Step | Date | Description | Tests |
|------|------|-------------|-------|
| 1–10 | 2026-03-02/03 | 정적 분석, 테스트/벤치마크 인프라, CI/CD | 32 |
| 11–15 | 2026-03-04/05 | ReactiveUI 전환, DynamicData, DagEditorViewModel, ViewModel Migration | 32 |
| 16 | 2026-03-06 | 좌표계 기준선 고정, ViewportTransform, zoom 연결 드래그 | 50 |
| 17 | 2026-03-06 | Dag O(1) 노드 인덱스, HandleConnectionChanged 최적화 | 65 |
| 18 | 2026-03-06 | Connector snap closest-candidate | 71 |
| 18 (*)  | 2026-03-06 | Undo/Redo, Selection Rectangle, Viewport ViewModel Migration | 103 |
| 19 | 2026-03-07 | Viewport Contract Hardening, 양방향 sync, VCA 매핑 문서 | 114 |
| 20 | 2026-03-07 | INTEGRATION_CONTRACT.md, INTEGRATION_EXECUTION_DAGEDIT.md 작성 | 114 |
| 21 | 2026-03-11 | Phase 1 Viewer adapter spike — NodeViewItem projection seam, VirtualCanvas.Core 참조 추가 | 120 |
| 22 | 2026-03-11 | Phase 1 Viewer projection trigger spike — DagViewerProjectionAdapter (add/remove/move/flush) | 130 |
| F-0-prep | 2026-03-11 | Stable projection cache spike — NodeViewItem mutable Bounds + in-place OnNodeMoved | 134 |
| F-0 | 2026-03-11 | Actual wiring spike — BuildSnapshot() (same refs + new index snapshot), F-0 loop 검증 | 143 |

---

## 11. Next Single Small Diff

**다음 공은 VCA 쪽이다.**

DagEdit 가 할 수 있는 것은 Step 21 에서 완료되었다:
- projection seam (`NodeViewItem`) 정의
- viewport 매핑 규칙 확인
- `VirtualCanvas.Core` 참조 추가

**VCA 쪽에서 필요한 다음 한 가지**:

`DagNodeVisualFactory` skeleton — `IVisualFactory.Realize(ISpatialItem)` 로 `NodeViewItem` 을 받아 Avalonia `Control` 을 반환하는 최소 구현.

```csharp
// VCA DevApp 또는 별도 통합 포인트에 작성 (DagEdit 에 넣지 않는다)
// NodeViewItem item = (NodeViewItem)spatialItem;
// → new Border { Width = item.Bounds.Width, Height = item.Bounds.Height } 수준이면 충분
// → R-C(lifecycle 충돌) 조기 확인 목적
```

이 결과를 보기 전에 DagEdit 에서 `IViewportHost` 추출이나 추가 추상화를 시작하면 premature abstraction 이 된다.

---

## 12. Contract Sync Dependency

```
Canonical: virtualcanvas-avalonia / docs/INTEGRATION_CONTRACT.md  (VCA-DAGEDIT-001 v0.1.0)
Mirror:    HeaInSeo/DagEdit / docs/INTEGRATION_CONTRACT.md         (VCA-DAGEDIT-001 v0.1.0)
Mirror-Status: Pending Sync (canonical v0.1.0 confirmed; body not yet fully reviewed for semantic equivalence)
Last-Synced: 2026-03-07 (header-only sync)
```

**이 문서에서 계약 변경이 필요해 보이면:**
1. 이 섹션 아래 `[Proposed Change]` 항목을 추가한다.
2. 사용자에게 보고하여 canonical 반영 여부를 결정받는다.
3. canonical 반영 확인 후 mirror 를 동기화한다.
4. 개별 repo 에서 계약을 단독으로 확정하지 않는다.

**현재 Proposed Changes**: 없음.
