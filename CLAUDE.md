# DagEdit — Claude Code Guidelines

## 1. Responsibility boundary (immutable)

**DagEdit owns**: editor state (`Dag`, `DagEditorViewModel`), selection policy, undo/redo (`UndoRedoStack`), keyboard UX (`EditorGestures`), interaction quality (grid snap, connector snap, rubber-band, context menu), and all UX semantics.

**VCA owns**: rendering infrastructure, viewport math primitives (`Offset`/`Scale`/`ActualViewbox`), spatial index (`ISpatialIndex`), realize/virtualize lifecycle, batching contract (`BeginUpdate`/`EndUpdate`), visual factory seam (`IVisualFactory`), and pinning primitive (`Pin`/`Unpin`).

Do not move editor semantics into VCA. Do not re-implement quad-tree or viewport culling in DagEdit once Stage 2 is active.

## 2. VirtualCanvas (WPF) is reference-only

`/VirtualCanvas_ref/` and the `VirtualCanvas` repo are read-only references for porting analysis.
**Never modify them.** Build exclusion is already in place (`DagEdit.csproj`).

## 3. Staged adoption order — no skipping

```
Stage 1  Viewer       [COMPLETE — 2026-03-14]
Stage 2  Hybrid       DagEditorCanvas → VirtualCanvas items host replacement
Stage 3  Full Editor  VCA stable API consumer only
```

Prerequisites for Stage 2 must be verified before starting. See `docs/INTEGRATION_EXECUTION_DAGEDIT.md §7`.

## 4. Viewport state source of truth

`DagEditorViewModel.ViewportLocation` and `ViewportScale` are the SSoT.
`DagEditor` StyledProperties are passthrough for template-binding only.
VCA mapping: `ViewportLocation ≡ VCA.Offset`, `ViewportScale ≡ VCA.Scale` (same formula).
Do not redesign this mapping without an explicit decision and contract update.

## 5. Integration contract precedence

Canonical source: `virtualcanvas-avalonia/docs/INTEGRATION_CONTRACT.md`
Mirror: `DagEdit/docs/INTEGRATION_CONTRACT.md`

- **Patch** (wording, typo): update mirror directly.
- **Minor/Major**: propose in `docs/INTEGRATION_EXECUTION_DAGEDIT.md §12 [Proposed Change]` only. Wait for canonical update before mirroring.
- Section 2 (Fixed Contracts — coordinate formula) requires explicit user approval for any change.

## 6. Decision checklist before every change

Run the checklist in `docs/INTEGRATION_EXECUTION_DAGEDIT.md §9` before writing code:
- Does it add direct `DagEditorCanvas` coupling that blocks VCA replacement?
- Does it touch the coordinate formula (must keep all viewport tests green)?
- Does it unilaterally resolve a deferred/hold item?
- Does it skip a phase prerequisite?

## 7. Small diffs; no unrelated refactors

Each commit must have a single, stated purpose. Do not clean up surrounding code, add comments to unchanged lines, or refactor while fixing a bug. Unrelated SA* warning fixes belong in a dedicated cleanup commit.

## 8. InspectCode regression gate

InspectCode warnings must not increase (CI gate: `inspectcode.yml`).
Baseline: `docs/INSPECTCODE_REDUCTION_PLAN.md`. Before writing code, verify the current baseline.
Error-level rules: SA1503, CS8600–CS8625, SA1400, SA1106, IDE0059, IDE0060, CS0168, CS0219, `_camelCase` private fields.

## 9. Coordinate formula (frozen)

```
world  = (screen + Offset) / Scale
screen =  world  × Scale   − Offset
```

Validated by `tests/DagEdit.Tests/SelectionRectTests.cs` and `ViewportTransformTests.cs`.
Any change here is a **Major** contract update requiring user approval and sync to both repos.
