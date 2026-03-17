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

## 8. Warning debt policy

InspectCode warnings must not increase (CI gate: `inspectcode.yml`).
Baseline: `docs/INSPECTCODE_REDUCTION_PLAN.md`. Before writing code, verify the current baseline.
Error-level rules: SA1503, CS8600–CS8625, SA1400, SA1106, IDE0059, IDE0060, CS0168, CS0219, `_camelCase` private fields.
No new warnings may be introduced. Prefer reducing the baseline when touching existing code.

## 9. Coordinate formula (frozen)

```
world  = (screen + Offset) / Scale
screen =  world  × Scale   − Offset
```

Validated by `tests/DagEdit.Tests/SelectionRectTests.cs` and `ViewportTransformTests.cs`.
Any change here is a **Major** contract update requiring user approval and sync to both repos.

## 10. Validation responsibility

Every change carries a validation obligation appropriate to its type:

| Change type | Expected validation |
|---|---|
| New feature | New or updated tests covering the added behavior |
| Bug fix | A regression test that would have caught the bug |
| Refactor | Existing tests must remain green; add tests if coverage was absent |
| Purely mechanical cleanup (SA* warnings, wording) | No new tests required; relevant existing tests must still pass |

Do not demand tests blindly. Do demand that validation matches the risk of the change.

## 11. Completion reporting

A task is not complete until the following are stated explicitly:

- **What changed**: files and logic affected
- **Validation run**: which tests, lint checks, or manual verifications were performed
- **Results**: pass/fail counts, warning counts, any regressions
- **Remaining risks**: known unknowns, deferred items, or assumptions that were not verified

## 12. Hidden failure mode review

Before marking a change complete, explicitly check for failure modes beyond the happy path:

- null / empty boundary conditions
- drag-time state inconsistency (mid-drag model mutations not committed on cancel)
- selection breakage during virtualization transitions
- SpatialIndex update omissions after model changes
- undo/redo inconsistency (command does not fully restore state)
- coordinate drift across pan/zoom cycles

These are examples, not a closed list. The obligation is to actively look, not merely to avoid accidentally introducing.
