# Clean State Machine — Technical Review

Generated: 2026-05-26

---

## Table of Contents

1. [Performance Issues & Bad Practices](#1-performance-issues--bad-practices)
   - [Runtime Performance](#a-runtime-performance)
   - [Editor Performance](#b-editor-performance)
   - [Code Quality Issues](#c-code-quality-issues)
2. [Missing Features for a Commercial State Machine](#2-missing-features-for-a-commercial-state-machine-graph)
   - [Core Graph Features](#a-core-graph-features)
   - [Editor / Usability](#b-editor--usability)
   - [Runtime / Debugging](#c-runtime--debugging)
   - [Architecture / Integration](#d-architecture--integration)
   - [Minor UX Gaps](#e-minor-ux-gaps)

---

## 1. Performance Issues & Bad Practices

### A. Runtime Performance

#### 1.1 O(n) Blackboard Variable Access — No Index

**Files:** `StateMachineComponent.cs:512-654`

Every call to `SetBoolParameter()`, `GetBoolParameter()`, `SetIntParameter()`, etc. iterates `_runtimeVariables` linearly using a `for` loop over the entire list. During `Update()` with many conditions each calling `GetFloatParameter()` or `GetBoolParameter()`, this creates O(n*m) complexity per frame where n = variable count and m = condition count.

**Fix:** Replace the `List<BlackboardVariable>` with a `Dictionary<string, BlackboardVariable>` for O(1) lookup by name.

---

#### 1.2 Reflection-Based Type Resolution with No Cache for Conditions

**Files:**
- `StateMachineComponent.cs:279-354` (`CheckTransitions`, `EvaluateConditions`)
- `StateMachineComponent.cs:469-483` (`ResolveType`)

For behaviours, instances are cached in `_behaviourInstances` after first creation. For conditions they are **NOT cached** — every frame, `EvaluateConditions()` calls `ResolveType()` which invokes `Type.GetType()`, then iterates all loaded assemblies, then calls `ScriptableObject.CreateInstance(type)`. The resulting instances are added to `_runtimeConditionInstances` but this list **never shrinks** — instances accumulate until `OnDestroy()`.

**Issues:**
- High per-frame cost for active connections with condition instances that were nulled
- Reflection + `CreateInstance` on every evaluation is a heavy allocation path
- `_runtimeConditionInstances` list grows unboundedly, leaking ScriptableObjects until the component is destroyed

**Fix:** Cache condition instances the same way behaviours are cached, and cap/clean the `_runtimeConditionInstances` list.

---

#### 1.3 `GetOrCreateBehaviours()` Defensive Checks Before Cache Hit

**File:** `StateMachineComponent.cs:226-277`

The method iterates all `state.Behaviours[i].Instance != null` checks (verifying every entry has a valid instance) **before** checking the `_behaviourInstances` dictionary. This means every time a state is entered, all its behaviour entries are iterated even after the first cache hit. The cache should be checked first, and the list-level null check should only run when populating the cache.

---

#### 1.4 Per-Frame Allocations in `ConnectionArrowsLayer`

**File:** `ConnectionArrowsLayer.cs:53-101`

`OnGenerateVisualContent()` calls `mgc.Allocate()` for every connection line, arrowhead triangle, and active-wave circle each frame. While UI Toolkit mesh generation is fast, allocating many small vertex buffers every frame creates GC pressure. With 100+ connections visible, this can become significant.

---

#### 1.5 Perpetual Glow Animation Schedule in `StateView`

**File:** `StateView.cs:478-524`

`InitializeGlowAnimation()` schedules a callback every 30ms via `schedule.Execute()` that **never stops**. Every `StateView` instance pays this cost whenever the editor window is open, regardless of whether the state is active. The callback checks `_isActive` and `_wasBriefActive` and sets opacity, but the schedule itself runs forever.

**Fix:** Cancel the schedule when the view is removed or when both `_isActive` and `_wasBriefActive` are false.

---

#### 1.6 No Pooling for Runtime ScriptableObject Instances

**File:** `StateMachineComponent.cs:258-276, 337-348`

`GetOrCreateBehaviours()` and `EvaluateConditions()` call `ScriptableObject.CreateInstance(type)` each time a cache miss occurs. For conditions evaluated per-frame, this creates unnecessary GC pressure. These should be created once at initialization (during `Awake()` or `Initialize()`), not lazily during `Update()`.

---

#### 1.7 `TransitionToState()` Allocates per Transition

**File:** `StateMachineComponent.cs:356-429`

Every transition creates a new `List<int>` via `new List<int>(_activeStatePath)`. In a game with frequent transitions, this is unnecessary GC churn. Use a pooled `List<int>` or pass the path by ref with a flag indicating it was mutated.

The same allocation pattern exists in `TransitionToStateDirect()` (`StateMachineComponent.cs:669-732`).

---

#### 1.8 Breakpoint Detection Walks a Full List

**File:** `PlayModeTracker.cs:219-228`

`OnGlobalStateEntered()` iterates `data.Breakpoints` linearly on every state entry. With 100+ breakpoints, this is O(n) per state entry. A `HashSet<int>` would give O(1) lookup.

---

#### 1.9 Blackboard Variable Name Change Bypasses Undo

**File:** `BlackboardPanel.cs:342-348`

`CommitNameEdit()` sets `_variables[index].Name = newName` directly without creating an undoable command. Only the **value** (string content) goes through `ModifyBlackboardVariableCommand`. Renaming a variable cannot be undone/redone.

---

#### 1.10 `TransitionRecord` List Never Capped

**File:** `StateMachineComponent.cs:16`

`_recentTransitions` grows unboundedly during play mode. No cleanup or max-length cap. This will consume increasing memory over long play sessions.

---

### B. Editor Performance

#### 1.11 `DetailsPanel.FindFilteredScripts()` Scans Every MonoScript in the Project

**File:** `DetailsPanel.cs:1276-1289`

`AssetDatabase.FindAssets("t:MonoScript")` loads every MonoScript in the project → calls `script.GetClass()` on each → filters by base type → sorts by name. On a large project with thousands of scripts, opening a behaviour or condition dropdown can take hundreds of milliseconds.

**Fix:** Cache MonoScripts by base type (`StateBehaviour`, `ConditionScript`) and refresh the cache when the asset database changes (via `AssetDatabasePostProcessor`).

---

#### 1.12 `SyncStatesWithSubMachines()` is O(n²) and Called Often

**File:** `GraphOperations.cs:565-609`

Nested loop over all states for every state, checking bounding-box containment (`cLeft / cRight / cTop / cBottom` comparison). Called on: state create, paste, duplicate, delete, drag end, resize end — every mutation path. With 500+ states, this becomes distinctly noticeable.

---

#### 1.13 `GraphSerializer.SaveCurrentData()` Has O(n²) Patterns

**File:** `GraphSerializer.cs:38-51, 131-141, 148-157`

The nested loops converting between `DataIndex` and list index (for child indices, expanded stack, breakpoints) rebuild the state-to-index dictionary once, but then iterate the states list for **each** entry that needs conversion (child indices, expanded stack, breakpoints). These should use the pre-built `stateToIndex` dictionary instead of inner loops.

---

#### 1.14 `PlayModeTracker.OnEditorUpdate()` Runs Every Editor Frame

**File:** `PlayModeTracker.cs:18-161`

Subscribed via `EditorApplication.update` in `OnEnable()` and unsubscribed in `OnDisable()`. While the window is open, this delegate fires every editor frame. Early returns for `!Application.isPlaying` reduce the cost, but the delegate dispatch overhead is still paid every frame regardless of whether a graph is loaded or play mode is active.

---

#### 1.15 `MenuDropdown` Holds a Static Cached StyleSheet Forever

**File:** `MenuDropdown.cs:10-16`

The cached `_styleSheet` field is a static reference that is never released. Since USS files are small, this is a minor leak, but non-idiomatic for a package that should clean up after itself.

---

### C. Code Quality Issues

#### 1.16 Error Suppression in External State Machine References

**File:** `StateMachineComponent.cs:734-756`

`ExecuteExternalAction()` silently ignores: null references, missing GameObjects, missing `StateMachineComponent` components, and empty state names. No `Debug.LogWarning()` or user feedback. Failures are invisible.

#### 1.17 `ResolveType()` Does Not Handle Assembly-Qualified Names Efficiently

**File:** `StateMachineComponent.cs:469-483`

Uses `Type.GetType(typeName)` which only resolves type names in the calling assembly or mscorlib. Falls back to iterating all assemblies. Should try `Type.GetType(typeName + ", AssemblyName")` for exact matches first.

#### 1.18 Inconsistent `hideFlags` Between Editor and Runtime

**File:** `StateMachineController.cs:111` vs `StateMachineComponent.cs:271`

Editor creates instances with `HideFlags.HideInHierarchy`. Runtime creates instances with `HideFlags.HideAndDontSave`. The `DestroyRuntimeInstances()` method only destroys objects with `HideFlags.HideAndDontSave`, meaning if a behaviour somehow gets the wrong flag, it leaks.

#### 1.19 Ghost Frame in `GraphPanController` Pan Start

**File:** `GraphPanController.cs:34-41`

The first `MouseDrag` event with button 1 checks `!IsPanning`, starts panning, and uses the event. On the next frame, `IsPanning` is true and normal panning proceeds. The `_hasDragged` flag correctly prevents context menus after panning, but the state machine transition is one frame behind the user's input.

#### 1.20 `BehaviourEntry` / `ConditionEntry` Duplicated Data Patterns

**Files:**
- `Runtime/StateMachineData.cs:16-20` and `Editor/StateView.cs:9-13`
- `Runtime/StateMachineData.cs:68-72` and `Editor/ConnectionView.cs:7-11`

`BehaviourEntry` / `BehaviourEntryView` and `ConditionEntry` / `ConditionEntryView` are near-duplicate data classes in Runtime and Editor assemblies. The Runtime classes carry `Instance` references and `TypeName` strings. The Editor classes carry `MonoScript` references and `Instance` references. This splitting creates conversion overhead in every save/load cycle and is a maintenance burden — adding a field requires changes in four places.

#### 1.21 Optional Parameters vs Method Overloading for Blackboard Access

**File:** `StateMachineComponent.cs:512-654`

The six Set/Get methods are near-identical copies. A single generic method with an enum parameter would eliminate 500+ lines of boilerplate.

---

## 2. Missing Features for a Commercial State Machine Graph

### A. Core Graph Features

#### 2.1 Any State / Global Transitions
No concept of a transition from "any state" (like Unity Animator's "Any State" node). Users must manually draw transitions from every state. With N states, this requires N connections instead of 1.

#### 2.2 Parallel / Concurrent States
Only one active leaf state at a time. No support for running multiple states in parallel, split/join logic, or synchronization barriers.

#### 2.3 History States
No shallow or deep history state for sub-machines. Entering a sub-machine always goes to its entry/sub-entry node, never to the last active child state.

#### 2.4 Sub-Machine Entry/Exit Actions
No way to define lifecycle actions when entering/exiting a sub-machine container (independent of its children). The sub-machine state type skips the behaviour list entirely.

#### 2.5 Transition Priority
Transitions are checked leaf-upward, but when multiple transitions from the same state are valid, the first in the list wins. No explicit priority or reordering UI.

#### 2.6 Built-In Transition Delays / Cooldowns
No integrated "minimum state time" or "exit delay" on transitions. The `Timer_Condition` sample shows this is possible via user-authored scripts, but a commercial tool should include it as a built-in option.

#### 2.7 OR / Composite / Grouped Conditions
Conditions use AND-only logic. No OR gates, nested condition groups, or boolean expressions. Cannot express "A OR (B AND C)" without writing a custom composite condition script.

#### 2.8 Self-Transitions
Can be created but there's no special handling — the state exits and re-enters itself. No distinction between "re-enter and fire all lifecycle methods" vs "stay but fire actions once."

#### 2.9 Event-Driven Transitions
All conditions are polled every frame in `Update()`. No support for event-driven activation (e.g., "this transition fires only when OnTriggerEnter fires on this GameObject").

---

### B. Editor / Usability

#### 2.10 Graph Validation Panel
No built-in validation for: orphaned/unreachable states, duplicate state names, cycles in non-hierarchical graphs, missing entry point, null MonoScript references on behaviour/condition entries, broken external reference targets.

#### 2.11 Search / Filter
No way to find states by name. In a graph with 200+ states, navigating is tedious.

#### 2.12 Condition Reordering
Conditions within a connection cannot be drag-reordered. Their evaluation order depends on list position but there's no UI to rearrange them.

#### 2.13 Behaviour Reordering
Multiple behaviours on a state cannot be reordered. Users must remove and re-add to change execution order.

#### 2.14 Hierarchical Tree View in Side Panel
No tree view showing the full state machine hierarchy alongside the graph. Sub-machine nesting can only be navigated via the breadcrumb bar.

#### 2.15 Multi-Select Property Editing
Selecting multiple states shows a summary list but there's no way to edit a property on all selected items at once.

#### 2.16 Auto-Layout
No automatic graph layout algorithm. Users must manually arrange every state, even on initial import of large graphs.

#### 2.17 Graph Export (SVG/PNG)
No way to export the graph for documentation, presentations, or PR reviews.

#### 2.18 LOD / Zoom Clustering
At high zoom-out levels, states become tiny but still render full labels, icons, and shadow details. No level-of-detail culling or clustering.

#### 2.19 Undo Stack Visualization
The `HistoryChanged` event fires but nothing binds to it in the UI. The undo/redo toolbar buttons (e.g., "Undo: Move States") are not implemented.

#### 2.20 Drag-to-Connect from Node Edge
Connection requires pressing C + clicking target. No visible output/input ports on node edges for drag-to-connect interaction, which is the standard UX in visual scripting tools.

#### 2.21 No Arrow-Key Nudging
Selected states cannot be nudged with arrow keys. Every position adjustment requires a mouse drag.

#### 2.22 No Ctrl+A Select All
No keyboard shortcut to select all visible nodes.

#### 2.23 No Tooltips
Graph node icons (sub-machine arrow, external reference arrow, breakpoint dot) have no tooltips explaining their meaning.

---

### C. Runtime / Debugging

#### 2.24 Live Variable Watch Window
No way to inspect runtime blackboard variable values while the game is running except by selecting the GameObject and looking at the custom inspector.

#### 2.25 Transition Timeline / History
No visual timeline showing past state transitions with timestamps, durations, and which conditions triggered the transition.

#### 2.26 Built-In Profiling
No way to measure: time spent per state, transitions per second, condition evaluation cost, or total state machine overhead.

#### 2.27 Remote / Build Debugging
All debugging requires the Editor and Play Mode. No way to inspect a running state machine on a built player or remote device.

#### 2.28 Build-Safety for Type Resolution
The runtime relies on `TypeName` strings to resolve behaviour/condition types. If a type is stripped from the build (e.g., managed code stripping, or the script was removed from the project), deserialization fails silently — the behaviour/condition is just skipped with no warning.

#### 2.29 Optional Verbose Logging
No built-in logging mode to trace state transitions, condition evaluations, or blackboard mutations at runtime.

---

### D. Architecture / Integration

#### 2.30 Input System Integration
No built-in support for Unity's Input System actions as transition triggers. Users must write a custom `ConditionScript` that reads input.

#### 2.31 Unity Animator Integration
No way to synchronize or blend state machine state with Unity's Mechanim/Animator parameters.

#### 2.32 Serialization Versioning
`SerializableData` has no version field or migration path. If the schema changes (new field on `StateData`, `ConnectionData`, etc.), old `.asset` files silently deserialize with default values — potentially breaking saved graphs.

#### 2.33 Reusable Sub-Graph Templates
No way to save a sub-machine as a reusable asset that can be instantiated across multiple controllers.

#### 2.34 Localization
All UI strings are hardcoded in English. No localization support.

#### 2.35 Network / Multiplayer
No consideration for replicating state machine state across the network. No deterministic mode for lock-step simulation.

---

### E. Minor UX Gaps

- No snap-to-grid for node positioning
- No option to hide/minimize the minimap
- Right-click only for panning (no middle-mouse click shortcut)
- Scroll-wheel zoom anchor is hardcoded to mouse position (no option to change it)
- No "Fit All" shortcut to zoom to content (the animation in `StartSmoothFocusOnContent` runs automatically on load but there's no manual trigger — actually there is at initialization, but no keyboard shortcut to invoke it on demand)
