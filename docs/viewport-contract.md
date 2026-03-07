# Viewport Contract

> Step 19 (2026-03-07). VCA 통합을 위한 viewport 상태 책임 방향 명세.

---

## Single Source of Truth

`DagEditorViewModel.ViewportLocation` (Point) 과 `DagEditorViewModel.ViewportScale` (double) 이
뷰포트 상태의 **Single Source of Truth** 다.

- 모든 코드는 뷰포트 상태를 `DagEditorViewModel` 을 통해 읽고 써야 한다.
- `DagEditor.ViewportLocation` / `DagEditor.ViewportScale` (StyledProperty) 는 **패스스루** 값이다.
  `PendingConnectionTemplate` 내부 `DataTemplate` 의 `$parent[DagEditor].ViewportLocation` 바인딩을
  지원하기 위해 존재한다 (DataTemplate DataContext 는 ViewModel 이 아니기 때문).

---

## 책임 방향 (Sync Architecture)

```
DagEditorViewModel.ViewportLocation
       │
       │  WhenAnyValue (ViewModel → DagEditor)
       ▼
DagEditor.ViewportLocation (StyledProperty)
       │
       │  GetObservable (DagEditor → ViewModel, 역방향)
       ▼
DagEditorViewModel.ViewportLocation
```

- **내부 코드** (DagEditor 이벤트 핸들러) 는 항상 `_viewModel.ViewportLocation/Scale` 에 직접 쓴다.
- **외부 코드** (테스트 하네스, 미래 VCA 바인딩) 는 `DagEditor` 속성에 써도 ViewModel 이 동기화된다.
- `_syncingViewport` 플래그가 두 방향 사이의 순환을 방지한다.
- Avalonia StyledProperty 와 ReactiveUI `RaiseAndSetIfChanged` 는 값이 동일하면 알림을 억제하므로
  가드 없이도 루프가 발생하지 않지만, 플래그는 의도를 명시적으로 표현한다.

---

## Transform Formula

DagEditorCanvas 의 RenderTransform:
```
TransformGroup(Scale(s, s), Translate(-vl.X, -vl.Y))
```

좌표 변환:
```
world → screen:
    screen.X = world.X * s − vl.X
    screen.Y = world.Y * s − vl.Y

screen → world:
    world.X = (screen.X + vl.X) / s
    world.Y = (screen.Y + vl.Y) / s
```

구현: `ViewportTransform.WorldToScreen` / `ViewportTransform.ScreenToWorld`

---

## VCA (VirtualCanvas-Avalonia) 매핑

| DagEdit 개념 | VCA 개념 | 비고 |
|---|---|---|
| `ViewportLocation` (Point) | `VirtualCanvas.Offset` (Point) | 수식 동일 |
| `ViewportScale` (double) | `VirtualCanvas.Scale` (double) | 수식 동일 |
| `ViewportTransform.WorldToScreen` | `world * Scale - Offset` | 완전 일치 |
| `ViewportTransform.ScreenToWorld` | `(screen + Offset) / Scale` | 완전 일치 |
| ─ | `ActualViewbox.TopLeft` | `ScreenToWorld(Point(0,0))` = `(vl.X/s, vl.Y/s)` |
| `FinalizeSelection()` 루프 | `ISpatialIndex.Query(worldRect)` | 드롭인 교체 가능 |

---

## Selection Rectangle 원칙

Selection rectangle 은 **항상 월드 좌표로 계산** 하며, 시각적 구현(Rectangle shape)과 무관하다.

```
FinalizeSelection() 흐름:

SelectedArea (screen rect, PART_TopLayer 로컬 좌표)
  └─ ScreenToWorld(TopLeft,  vl, scale) ─┐
  └─ ScreenToWorld(BottomRight, vl, scale) ─┘ → worldRect
       └─ foreach DagNode: Rect(loc, NodeSize).Intersects(worldRect) → Selection.Select(i)
```

불변성:
- 동일한 gesture 는 viewport pan/zoom 과 무관하게 동일한 노드 집합을 선택한다.
- `_selectionRect` (화면에 그려지는 파란 점선 사각형) 을 제거하거나 대체해도 선택 로직에 영향을 주지 않는다.
- VCA 통합 시 `foreach` 루프를 `VirtualCanvas.Items.GetItemsIntersecting(worldRect)` 로 교체하면
  동일한 `worldRect` 를 입력으로 사용하므로 결과가 일치한다.

---

## VCA 통합 경로 (미래)

1. `DagEditorCanvas` → `VirtualCanvas` 교체 (items host 역할).
2. `VirtualCanvas.Offset` ↔ `DagEditorViewModel.ViewportLocation` 양방향 바인딩.
3. `VirtualCanvas.Scale`  ↔ `DagEditorViewModel.ViewportScale` 양방향 바인딩.
4. `FinalizeSelection()` 내부 루프 → `VirtualCanvas.Items.GetItemsIntersecting(worldRect)`.
5. 노드 위치는 이미 월드 좌표이므로 VCA ISpatialIndex 에 바로 등록 가능.
6. `DagEditor.ViewportLocation/Scale` StyledProperty 는 바인딩 패스스루로 그대로 유지.

---

## 테스트 파일

| 파일 | 검증 내용 |
|---|---|
| `tests/DagEdit.Tests/ViewportTransformTests.cs` | `ScreenToWorld` / `WorldToScreen` 수식, round-trip, 줌 피벗 |
| `tests/DagEdit.Tests/ViewportViewModelTests.cs` | ViewModel 반응형 프로퍼티, 패닝 시뮬레이션 |
| `tests/DagEdit.Tests/SelectionRectTests.cs` | pan/zoom 불변성, VCA 공식 일치 |
