# ReactiveUI 패턴: 노드 드래그 좌표 업데이트

> 파일: `Node.cs`, `NodeDragState.cs`
> 관련 커밋: ReactiveUI WhenAnyValue 리팩토링 (2026-03-03)

---

## [What it does]

**해결하려는 UI 문제:**

노드를 드래그할 때 `PointerMoved` 이벤트는 초당 수십~수백 회 발생한다.
리팩토링 전 코드는 이 핸들러 안에서 다음 세 가지를 순서대로 수행했다:

1. 그리드 스냅 계산 (입력 처리)
2. `TranslateTransform.X/Y` 갱신 (렌더링)
3. `SourceAnchor`, `TargetAnchor` 재계산 (데이터)
4. `ConnectionChangedEvent` 발행 (이벤트)

**관심사가 뒤섞여 있어** 테스트, 유지보수, 확장이 어렵다.

**리팩토링 후:**

```
HandlePointerMoved  ─→  _dragState.Position = newPos
                                    │
                    ┌───────────────┘  (WhenAnyValue 체인)
                    ▼
            UpdateFromDragPosition(newPos)
                    │
                    ├── FindAnchors(newPos)
                    └── RaiseConnectionChangedEvent(...)
```

`HandlePointerMoved`는 **"어디로 이동했는가"** 만 결정한다.
Rx 체인은 **"이동했을 때 무엇을 해야 하는가"** 를 독립적으로 처리한다.

---

## [Go Analogy]

Go의 채널(chan)과 고루틴(goroutine)으로 이해하면:

```go
// NodeDragState.Position ≈ 버퍼 채널
posCh := make(chan Point, 1)

// HandlePointerMoved ≈ 생산자 고루틴
go func() {
    for _, event := range pointerEvents {
        if effectiveDelta != zero {
            posCh <- newPosition  // 그리드 스냅 후 새 위치만 전송
        }
    }
}()

// WhenAnyValue 구독 ≈ 소비자 고루틴
go func() {
    prev := Point{}
    for pos := range posCh {
        if pos == prev { continue }  // DistinctUntilChanged
        prev = pos
        updateAnchors(pos)           // FindAnchors
        emitEvent(pos)               // RaiseConnectionChangedEvent
    }
}()

// DisposeWith ≈ defer cancel()
// Node가 GC될 때 구독 자동 해제 → 고루틴 누수 방지
```

| Go 개념                | Rx 대응                    |
|------------------------|----------------------------|
| `chan Point`           | `IObservable<Point>`       |
| `ch <- value`          | `RaiseAndSetIfChanged`     |
| `for val := range ch`  | `.Subscribe(...)`          |
| `if val == prev { skip }` | `.DistinctUntilChanged()` |
| `defer cancel()`       | `.DisposeWith(_disposables)` |

---

## [Operator Breakdown]

### `WhenAnyValue(x => x.Position)`

```csharp
_dragState.WhenAnyValue(x => x.Position)
```

- **역할**: `NodeDragState.Position` 프로퍼티의 변경을 `IObservable<Point>` 스트림으로 변환한다.
- **작동 방식**: `ReactiveObject`가 `RaiseAndSetIfChanged`를 호출할 때마다 새 값을 방출.
- **주의**: 구독 즉시 현재 값(`Point()`)을 한 번 방출한다 → 다음 연산자(`Skip`)로 처리.

---

### `Skip(1)`

```csharp
.Skip(1)
```

- **역할**: 구독 직후 방출되는 **초기값**(`Point()`)을 무시한다.
- **이유**: 드래그가 시작되기 전, Node 생성 시점의 빈 좌표가 처리되는 것을 방지.
- **Go 비유**: 채널에서 첫 번째 값을 항상 버리는 패턴 (`<-ch // discard init`).

---

### `DistinctUntilChanged()`

```csharp
.DistinctUntilChanged()
```

- **역할**: **이전과 동일한 위치**가 연속으로 들어올 때 다운스트림으로 전달하지 않는다.
- **이유**: 그리드 스냅(15px 단위)으로 인해 마우스가 조금 움직여도 실제 `Position`이
  바뀌지 않을 수 있다. 이때 불필요한 앵커 재계산과 이벤트 발행을 막는다.
- **성능**: `FindAnchors` + `RaiseEvent`의 호출 횟수를 최소화하여 렌더링 부하를 줄인다.

---

### `Subscribe(UpdateFromDragPosition)`

```csharp
.Subscribe(UpdateFromDragPosition)
```

- **역할**: 새 위치가 확정될 때마다 `UpdateFromDragPosition(Point)` 메서드를 호출한다.
- **처리 내용**:
  1. `FindAnchors(newPosition)` → `SourceAnchor`, `TargetAnchor` 갱신
  2. `RaiseConnectionChangedEvent(...)` → 연결된 Connection 선 위치 업데이트 요청

---

### `DisposeWith(_disposables)`

```csharp
.DisposeWith(_disposables)
```

- **역할**: 반환된 `IDisposable` 구독 객체를 `CompositeDisposable`에 등록한다.
- **효과**: `Node.Dispose(true)` 시 `_disposables.Dispose()` 호출 → 구독 자동 해제.
- **이전 코드와의 차이**: 기존엔 단일 `_disposable: IDisposable` 필드였다.
  이제 `CompositeDisposable`로 여러 구독을 묶어 관리하므로 새 구독 추가가 안전하다.

---

## 아키텍처 다이어그램

```
PointerPressed
    │
    ▼
HandlePointerPressed
    │ _initialPointerPosition = ...
    │ _initialNodePosition = Location
    │ IsDragging = true

PointerMoved (매우 빈번)
    │
    ▼
HandlePointerMoved
    │ delta = currentPos - initialPos
    │ _dragAccumulator += delta
    │ effectiveDelta = snap(accumulator, 15px)
    │
    ├─ [effectiveDelta == 0] → 아무것도 하지 않음
    │
    └─ [effectiveDelta != 0]
            │ _translateTransform.X/Y 갱신 (렌더링)
            │ _temporaryNewPosition 계산
            └─ _dragState.Position = _temporaryNewPosition
                            │
                    ┌───────┘ (WhenAnyValue 체인)
                    ▼
            UpdateFromDragPosition(newPosition)
                    │ FindAnchors(newPosition)
                    │ SourceAnchor = ...
                    │ TargetAnchor = ...
                    └─ RaiseConnectionChangedEvent(...)
                                    │
                            ┌───────┘ (Bubble)
                            ▼
                    DagEditor.HandleConnectionChanged
                            │
                            └─ Connection.UpdateAnchors(...)
```

---

## 다음 ReactiveUI 작업 예정

1. `PendingConnection` 드래그를 `Observable.FromEventPattern`으로 전환
2. `DagEditor`의 노드 추가/삭제를 `ReactiveList`로 관리
3. `ViewModel` 레이어 분리: `DagEditorViewModel : ReactiveObject`
