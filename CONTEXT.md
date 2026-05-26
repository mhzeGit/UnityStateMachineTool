<!--
================================================================================
UNIVERSAL AI CONTEXT — Read this first when entering this project.
Every AI agent, in any tool, should be able to understand the project completely
from this one file and start contributing immediately.
================================================================================
-->

# Clean State Machine — Unity Project

## Project Overview
- A visual state machine editor and runtime system for Unity, delivered as an embedded UPM package (`com.cleanstatemachine`)
- Editor tool provides a graph-based canvas with nodes (states), edges (transitions), groups, blackboard variables, behaviours, and conditions
- Runtime executes the graph via `StateMachineComponent`, a MonoBehaviour you attach to any GameObject
- Unity 6000.0+ required; no external package dependencies; MIT licensed
- The project root is a standard Unity project shell that **only exists to host and test the package** — the actual product is the package itself

## Scope / What to Focus On
- **Only the package folder matters**: `Packages/com.cleanstatemachine/`
- **Everything else is a standard Unity project wrapper** (Assets, ProjectSettings, Library, Temp, etc.) — ignore it for code contributions unless you need demo scenes, sample assets, or build settings
- The `README.md` at the project root is documentation for end users of the package

## Architecture & Patterns

### Namespace
- Everything lives in `namespace CleanStateMachine`

### Three-Assembly Split
- `CleanStateMachine.Runtime` — models, runtime execution, abstract bases (no UnityEditor dependency)
- `CleanStateMachine.Editor` — the graph editor window and all editor UI (references Runtime, Editor-only platform)
- `CleanStateMachine.Behaviours` — shim assembly for user-authored behaviour/condition scripts (separates user code from package internals)

### Data Model (MVC-like)
- `SerializableData` is the single source of truth — holds lists of `StateData`, `ConnectionData`, `GroupData`, `BlackboardVariable` plus viewport state (pan, zoom, panel layout)
- `StateMachineController` is a `ScriptableObject` wrapping `SerializableData`; behaviour/condition instances are stored as **sub-assets** of the controller asset
- The editor's `CleanStateMachineWindow` has parallel view-model lists: `States` (List<StateView>), `Connections` (List<ConnectionView>), `Groups` (List<CommentGroupView>), `BlackboardVariables` (List<BlackboardVariable>)
- `GraphSerializer` is the bridge: `SaveCurrentData()` converts views to data, `LoadFromCurrentData()` converts data to views

### Editor Window Architecture
- `CleanStateMachineWindow` is the central hub — it owns all controllers and modules (composition via constructor injection with `this` reference)
- **Controllers** (handle state): `SelectionController`, `UndoRedoSystem`, `GraphPanController`, `DragController`, `SelectionBox`, `ConnectionController`, `GraphContextMenu`
- **Helper Modules** (stateless service objects): `GraphOperations`, `GraphInputHandler`, `ExpandedViewManager`, `GraphSerializer`, `PlayModeTracker`, `GraphViewAnimator`, `GraphValidation`, `GraphSearchPanel` (search/filter UI overlay)
- **UI Layers** (VisualElements in z-order): `GridBackground` to `GroupContainer` to `ConnectionArrowsLayer` to `StateLayer` to `GraphCanvas` (IMGUI overlay) to `SidePanelElement`
- The main loop runs in `OnGUI()` (IMGUI event processing) + UI Toolkit callbacks (visual tree)
- USS stylesheets are loaded via `ScriptReferenceUtility.LoadStyleSheet()` using `Resources.FindObjectsOfTypeAll` + `AssetDatabase.GetAssetPath` pattern

### State Runtime
- `StateMachineComponent` executes a **path-based** state machine (not flat states): `_activeStatePath` is a `List<int>` from root to leaf, supporting hierarchical sub-state-machines
- Transition evaluation walks from leaf upward (depth-first: leaf to parent to grandparent) checking `ConnectionData` with `ConditionScript.Evaluate()`
- Five state node types: **normal** (has a `StateBehaviour`), **sub-state-machine** (container for child states, no behaviour), **sub-entry** (entry point inside a sub-machine), **external-reference** (references a different `StateMachineComponent` GameObject and executes an action on it when entered — start, set state by name, or set blackboard parameter), **any-state** (transitions from this node are evaluated globally from any active state, like Unity Animator's "Any State")
- Each behaviour/condition is instantiated at runtime via `ScriptableObject.CreateInstance` with `HideFlags.HideAndDontSave` — avoids leaking sub-asset references
- Blackboard variables are **copied** from the controller at init (`Clone()`), so runtime mutations don't affect the asset
- Events: `OnStateChanged`, `OnStateEntered`, `OnStateExited`

### Undo/Redo
- Classic Command pattern: `IUndoableCommand` interface to concrete commands to `UndoRedoSystem` (two stacks, max 50)
- `CompositeCommand` allows grouping multiple atomic commands into one undo step
- Every graph mutation in `GraphOperations` goes through `UndoRedoSystem.Execute()` — never modifies views directly

### Extensibility
- `IContextMenuProvider`: implement to inject custom items into the graph editor's right-click menu; discovered via `TypeCache.GetTypesDerivedFrom<>` at editor startup
- `StateBehaviour` / `ConditionScript`: publicly inheritable ScriptableObject base classes for user-authored behaviours
- `BlackboardVariableReference`: value-type-agnostic field pattern for behaviour/condition parameters (toggle between direct value and blackboard variable)

### Naming Conventions
- C#: PascalCase everywhere (standard Unity convention)
- Internal fields: `_camelCase` prefix for private instance fields
- Public properties: PascalCase (e.g., `PanOffset`, `CurrentStateIndex`)
- Classes: `Noun` for models, `NounView` for visual elements, `NounController` for interaction controllers, `NounCommand` for undo/redo
- Enums: PascalCase values (e.g., `BlackboardVariableType.Bool`)
- USS files: matched to the VisualElement class they style (e.g., `StateView.uss`)

## Package Structure (`Packages/com.cleanstatemachine/`)

```
package.json
Runtime/                          — Runtime C# code (assembly: CleanStateMachine.Runtime)
Editor/                           — Editor-only C# code (assembly: CleanStateMachine.Editor)
  UndoRedo/                       — Command-pattern undo/redo system
  Styles/                         — USS (Unity Style Sheets) for UI Toolkit
  ScriptTemplates/                — .txt templates + creation wizard
Assets/                           — Shim assembly for user-authored behaviours (CleanStateMachine.Behaviours)
  ActionBehaviours/               — Example action scripts
  ConditionBehaviours/            — Example condition scripts
  SateBehaviours/                 — Example state behaviour scripts
Demo/                             — Example .asset controller for testing
```

---

## RUNTIME SYSTEM

The Runtime assembly (`CleanStateMachine.Runtime`) is the engine-agnostic core: it defines the data model, the abstract base classes users extend, and the MonoBehaviour that drives the state machine at play time. All runtime code has zero UnityEditor dependencies. The system is built around a path-based hierarchical state machine — states are organized as a tree with nested sub-state-machines, and transitions are evaluated leaf-upward. Each state can have multiple StateBehaviour ScriptableObjects defining OnStateEnter/Update/Exit logic (all run in order), while Condition ScriptableObjects (one or more per transition) gate transitions with AND logic. A typed blackboard system allows states and conditions to share data via named variables, with runtime copies isolated from the asset to prevent mutation leakage. The `StateMachineController` ScriptableObject is the asset that serializes the entire graph and owns all behaviour/condition instances as sub-assets.

### Scripts

**`StateMachineData.cs`** — Defines the serializable data model classes that represent the entire state machine graph. `SerializableData` is the root container: it holds `List<StateData>` (each with name, position, size, entry/sub-entry/sub-machine/external-reference/any-state flags, child indices for nesting, a `List<BehaviourEntry>` of behaviour entries with type names + instance references, external state machine action/config), `List<ConnectionData>` (each with from/to state indices, `MinStateTime` for per-connection transition cooldown, and a `List<ConditionEntry>` of conditions with type names + instance references), `List<GroupData>` (label, color, member state indices), `List<BlackboardVariable>` (typed variables), `List<BreakpointData>` (breakpoint state indices with parent path for hierarchical support), plus viewport state (PanOffset, Zoom, ExpandedSubStateIndices). Window layout (ShowSidePanel, SidePanelWidth, DetailsHeightRatio) was removed — those are now global EditorPrefs. `BehaviourEntry` mirrors `ConditionEntry` with a `TypeName` string and `StateBehaviour Instance`. `StateData` implements `ISerializationCallbackReceiver` to migrate legacy single-behaviour data into the new list format.

**`BreakpointData.cs`** — Serializable data class for storing breakpoint state indices. Each breakpoint records the state index (`StateIndex`) and an optional parent path (`ParentPath`) for hierarchical state machine support. Stored as a list in `SerializableData.Breakpoints` and persisted with the controller asset.

**`StateMachineController.cs`** — A `ScriptableObject` with `[CreateAssetMenu]` that wraps `SerializableData` and manages sub-asset lifecycle. Iterates all `BehaviourEntry` lists per state and `ConditionEntry` lists per connection to create `ScriptableObject` instances via `CreateInstance`, store them as sub-assets, and hide them from the project hierarchy (`HideFlags.HideInHierarchy`). Methods: `RebuildBehaviourInstances()` recreates all instances from type names; `EnsureSubAssets()` removes orphaned sub-assets no longer referenced; `Save()` persists to disk. During `OnValidate()`, it auto-heals by recreating null instances without re-registering sub-assets.

**`StateMachineComponent.cs`** — The main runtime MonoBehaviour. Attach to any GameObject with a `StateMachineController` reference. At initialization, clones blackboard variables, builds the initial state path by walking from the root entry through sub-state entries to the deepest leaf, and calls `OnStateEnter` on all behaviours in the path. Each `Update()`, calls `OnStateUpdate` on all behaviours of the leaf state, then evaluates Any State (global) transitions first, followed by bottom-up per-state transitions. Each connection has a `MinStateTime` — if set, the transition is blocked until that many seconds have elapsed since entering the current leaf state. When all conditions on a connection return true (and the cooldown has passed), it transitions: exits all behaviours from the divergent path segment, fires events, enters all behaviours on the new segment, and logs a `TransitionRecord`. Public API: typed getter/setters for all six blackboard variable types, `SetState(string stateName)`, `ResetStateMachine()`, properties for current state name/index/path, and a `List<TransitionRecord>` log. External reference state nodes execute the configured `ExternalStateMachineAction` on the referenced target `StateMachineComponent` when entered.

**`StateBehaviour.cs`** — Abstract `ScriptableObject` base class for per-state logic. Users subclass and override the three virtual lifecycle methods: `OnStateEnter(StateMachineComponent)`, `OnStateUpdate(StateMachineComponent)`, `OnStateExit(StateMachineComponent)`. Has a virtual `DisplayName` property (defaults to the type name) used by the editor UI. Instances are created as sub-assets of the controller at edit time, then invoked by `StateMachineComponent` during runtime. The base methods are empty — override only what you need.

**`ConditionScript.cs`** — Abstract `ScriptableObject` base class for transition conditions. Users subclass and override the single abstract method `bool Evaluate(StateMachineComponent)`. Multiple conditions on a connection form AND logic (all must return true). Also has a virtual `DisplayName` property (defaults to the type name) used by the editor UI. Like behaviours, instances are stored as sub-assets.

**`StateMachineAction.cs`** — Abstract `MonoBehaviour` base class for external scripts that interact with the state machine via blackboard. Stores a serialized reference to a `StateMachineComponent`, a blackboard variable name, and a type hint. Provides protected `SetBlackboardValue(T)` and `GetBlackboard{T}()` methods (overloaded for all six types) that delegate to the component's parameter API. Used by action behaviours like `TriggerEnter_SetBool` that react to Unity events (collisions, triggers) by modifying state machine variables.

**`BlackboardVariable.cs`** — A `[Serializable]` class representing a single blackboard variable. All typed access goes through a single `string StringValue` backing field, parsed/formatted with invariant culture. Exposes typed getter/setter properties (`BoolValue`, `IntValue`, `FloatValue`, `Vector2Value`, `Vector3Value`) that serialize to/from the string. Has `string Name` and `BlackboardVariableType Type` (an enum: Bool, Int, Float, String, Vector2, Vector3). `Clone()` creates an independent copy — used by `StateMachineComponent` to create runtime-isolated variables from the controller's asset data.

**`BlackboardVariableReference.cs`** — A `[Serializable]` utility class used by behaviour/condition scripts to toggle between reading a value from the blackboard or using a hardcoded default. Fields: `bool UseBlackboard`, `string BlackboardVariableName`, `BlackboardVariableType ValueType`, `string DefaultValue`. Provides typed getter methods (`GetBoolValue`, `GetFloatValue`, `GetVector3Value`, etc.) that check `UseBlackboard` and either call the component's parameter API or parse `DefaultValue` (with invariant culture and comma-separated vector parsing).

**`BlackboardVariableSelector.cs`** — A `[Serializable]` utility class for selecting a blackboard variable by name in behaviour/condition inspector fields. Has `string VariableName` and `BlackboardVariableType ValueType`. Rendered as a dropdown button in `DetailsPanel.BuildBbVarSelectorField` — clicking shows all blackboard variables from the controller and sets both name and type on selection.

---

## EDITOR SYSTEM

The Editor assembly (`CleanStateMachine.Editor`) contains the entire graph editor UI, built on Unity's UI Toolkit with some IMGUI for event processing. The architecture follows a hub-and-spoke pattern: `CleanStateMachineWindow` (the EditorWindow) owns every controller and module, passing itself as a dependency. Controllers manage interactive state (selection, dragging, connecting, panning, context menus), helper modules provide stateless services (serialization, graph mutations, expanded view management, play-mode tracking, animation), and UI layers are stacked VisualElements rendered via procedural mesh generation or USS styling. All graph mutations flow through the `UndoRedoSystem` via `GraphOperations`. The editor supports hierarchical sub-state-machines via an expansion/drill-down system with breadcrumb navigation.

### Core Window & Persistence

**`CleanStateMachineWindow.cs`** — The main `EditorWindow` (opened via Tools > CleanStateMachine or by double-clicking a controller asset). It owns all controllers (`SelectionController`, `UndoRedoSystem`, `GraphPanController`, `DragController`, `SelectionBox`, `ConnectionController`, `GraphContextMenu`) and modules (`GraphOperations`, `GraphInputHandler`, `ExpandedViewManager`, `GraphSerializer`, `PlayModeTracker`, `GraphViewAnimator`), plus all UI layer VisualElements (`GridBackground`, `ConnectionArrowsLayer`, `GraphPreview`, `StateLayer`, `GroupContainer`, `SidePanelElement`, breadcrumb bar). The main loop runs in `OnGUI()` for IMGUI event dispatching (keyboard shortcuts, mouse events) and UI Toolkit callbacks for visual rendering. Manages viewport state (pan/zoom), unsaved-changes tracking, clipboard, and sub-state-machine expansion stack. Opens controllers via `OpenWithController(StateMachineController)`. `OnSelectionChange()` auto-opens the graph when a GameObject with a `StateMachineComponent` (and an assigned controller) is selected. Window layout (showSidePanel, sidePanelWidth, detailsHeightRatio) is stored in EditorPrefs (global) instead of per-asset — nested `LayoutPrefs` static class handles save/load.

**`GraphSerializer.cs`** — Bridge between editor views and the serialized `SerializableData` model. `SaveCurrentData()` walks all StateViews, ConnectionViews, CommentGroupViews, and BlackboardVariables and writes them into `CurrentData` (converting views to data model). `LoadFromController()` reads the controller asset and deserializes into views. `LoadFromCurrentData()` rebuilds the entire graph view from already-loaded data (used on re-enter edit mode). `SaveToController()` persists the current editor state back to the ScriptableObject asset. `SaveAs()` opens a file dialog to create a new controller asset. `NewFile()` resets to blank with unsaved-changes prompting. `LoadController()` switches to a different controller. This is the single point of contact for all disk I/O and serialization format. Per-asset data: pan/zoom/expandedStack (viewports). Window layout (side panel) is not touched — it's now EditorPrefs-global.

**`StateMachineAssetHandler.cs`** — A static class with an `[OnOpenAsset]` callback that intercepts double-click on `StateMachineController` assets and opens them in the graph editor instead of the default inspector. Also has a `[MenuItem]` to create new controller assets via the Create menu.

### Graph Interaction Controllers

**`GraphInputHandler.cs`** — Translates raw Unity `Event` data into high-level graph interactions. `HandleKeyboardShortcuts()` processes Ctrl+Z/Y (undo/redo), Ctrl+G (group), Ctrl+C/V/D (copy/paste/duplicate), F2 (rename), Delete/Backspace, and C (connect mode). `HandleLeftClickInteraction()` dispatches mouse down/drag/up for select, drag-move, resize, and box-select based on hit-testing. `HandleConnectingInput()` manages the connection-drawing drag state (completing or cancelling). `HitTest()` returns the topmost selectable under a graph-space point; `HitTestState()` specifically for states; `GetResizeEdge()` for group resize handles. `PerformBoxSelection()` runs a marquee hit-test across states, connections, and groups and adds matches to the selection.

**`GraphOperations.cs`** — The command-execution layer for all graph mutations. Every user-triggered change flows through here and is wrapped in an `IUndoableCommand` executed via `UndoRedoSystem`. Operations: `CreateState()` (creates a state node, optionally auto-connects from entry), `CreateSubStateMachine()` (creates a sub-machine container), `CreateExternalReferenceState()` (creates an external state machine reference node), `CreateAnyState()` (creates an "Any State" node for global transitions), `DeleteSelected()` (removes states/connections/groups, protects entry state, cleans up sub-machine/expanded stack), `CopySelectedStates()` / `PasteStates()` / `DuplicateSelectedStates()` (clipboard operations), `CreateGroupFromSelectedStates()` (wraps selected states in a comment group), `UngroupRequested()` (dissolves group), `StartEditing()` / `StartEditingGroup()` (inline rename), `EnsureEntryStateExists()` (guarantees one entry state). Also manages sub-machine containment sync and expanded-view updates.

**`SelectionController.cs`** — Central selection state tracker. Methods: `Select()`, `Deselect()`, `Toggle()`, `SelectOnly()`, `SelectRange()`, `Clear()`. Provides `Selected` (IReadOnlyList<ISelectable>) and `Count`. Fires `SelectionChanged` event after any change. All selectable items (StateView, ConnectionView, CommentGroupView) implement `ISelectable`.

**`DragController.cs`** — Manages drag-to-move for selected states and groups. `StartDrag()` records initial positions of all selected items. `UpdateDrag()` applies mouse delta once a movement threshold is exceeded (to prevent accidental nudges). `EndDrag()` finalizes and creates a `MoveStatesCommand` for undo/redo. Works with mixed selections (states + groups).

**`SelectionBox.cs`** — Marquee (rubber-band) selection visual. `Start()` begins at a graph position, `Update()` tracks the mouse, `End()` hides the visual. `GetGraphRect()` returns the graph-space rectangle used for hit-testing. `DrawScreen()` converts the rect to screen space accounting for zoom/pan, updating an overlay VisualElement.

**`ConnectionController.cs`** — Connection-drawing state machine for the "Press C to connect" flow. `StartConnection()` sets the source state and enters connecting mode. `UpdatePending()` tracks mouse position for rendering the ghost line. `TryComplete()` either connects to an existing state under the mouse or creates a new state and connects to it. `Cancel()` aborts. Fires `ConnectionCompleted` event.

**`GraphPanController.cs`** — Handles right-click/middle-mouse drag panning and scroll-wheel zooming (with anchor-point zoom for mouse wheel). Distinguishes touchpad pan (horizontal scroll + low-delta vertical) from mouse-wheel zoom. `HandleInput()` processes events each frame; `ConsumeContextClickIfPanned()` prevents context menu after a pan-drag; `CancelPanning()` aborts panning. Exposes `IsPanning` and `UserInteractedThisFrame` flags.

**`GraphContextMenu.cs`** — Right-click context menu builder. `Show()` constructs a `MenuDropdown` with context-sensitive items: Create State, Create Sub State Machine, Create External Reference, Create Any State, Connect (on state), Copy, Paste (when clipboard has data), Delete (on selection), Ungroup (on group). Also invokes registered `IContextMenuProvider` extensions. Fires action events (`CreateStateRequested`, `CreateExternalReferenceRequested`, `CreateAnyStateRequested`, `DeleteRequested`, etc.) that the window subscribes to.

### Graph Visual Elements

**`StateView.cs`** — VisualElement representing a single state node. Renders a rounded rectangle with shadow, selection glow, fill (dark gray default, green entry, orange/brown sub-machine, gold sub-entry, blue-purple external-reference, magenta any-state, lighter active), a centered name label, and sub-machine (↗), external-reference (⬅), or any-state (∞) icons. Implements `ISelectable`. Features: inline rename, rounded-rectangle hit-test, `UpdateTransform()` for zoom/pan, `ReactivateFlash()` for active-state glow. Exposes `Position`, `Size`, `Name`, `IsEntry`, `IsSubEntry`, `IsSubStateMachine`, `IsExternalReference`, `IsAnyState`, `IsSelected`, `IsActive`, `BehaviourEntries` (List<BehaviourEntry>), `ChildIndices`, external reference fields, `DataIndex`, and `EditingCommitted` event. Uses the unified `BehaviourEntry` from Runtime (resolves `MonoScript` from `TypeName` via `BehaviourEntryExtensions.GetScript()`) instead of a separate Editor-only view class.

**`ConnectionView.cs`** — Data model for a transition edge (does not render itself — that's `ConnectionArrowsLayer`). Stores `From` and `To` StateViews, a `List<ConditionEntry>` (using the unified Runtime type; resolves `MonoScript` from `TypeName` via `ConditionEntryExtensions.GetScript()`), selection/active state, a `PerpendicularOffset` for visually separating parallel connections between the same two states, and a `DataIndex`. Implements `ISelectable`: `GetGraphBounds()` returns the bounding rect of line + arrowhead, `ContainsPoint()` does distance-based hit-test against the line segment and arrowhead triangle, `BoxOverlaps()` for marquee selection intersection, `DrawSelectionOverlay()` delegates to the arrows layer. `Position` getter returns bounds position, setter is a no-op.

**`ConnectionArrowsLayer.cs`** — Procedural mesh VisualElement that renders all connection lines, arrowheads, and visual effects. Draws each connection with variable styling: default (thin gray), selected (blue highlight), active (fade/wave animation). Renders the pending connection drag line during connect mode. Uses feathered lines and arrowheads for anti-aliasing, plus traveling "wave" circles on recently activated connections. `IsConnectionHidden` delegate filters out connections whose endpoints are not visible (e.g., inside collapsed sub-machines).

**`CommentGroupView.cs`** — VisualElement representing a visual grouping box. Renders a semi-transparent rounded rectangle with a colored header bar and label. Implements `ISelectable`. Members: manages a list of `StateView` members via `AddMember()` / `RemoveMember()`, and `SyncContainedStates()` auto-adds/removes states geometrically inside the group. Features: inline rename (double-click/F2), drag-to-move (moves all member states), resize via corner handles (creates `ResizeGroupCommand`), selection/deselection border styling, `SetRect()` for direct position/size control. Exposes `Label`, `GroupColor`, `Members` (IReadOnlyList<StateView>), `IsEditing`, and an `EditingCommitted` event.

**`GridBackground.cs`** — VisualElement that renders a procedurally-generated minor/major grid via `MeshGenerationContext`. Grid spacing scales with zoom; lines offset with pan. Provides spatial reference on the graph canvas.

**`GraphPreview.cs`** — Minimap overlay (200x150px, bottom-right corner) that renders a miniature view of the entire graph. Procedurally generates meshes for states (colored rectangles), connections (lines), and a viewport indicator rectangle. Updated via `UpdateView()` which receives all states, connections, pan offset, zoom, and screen rect. Hidden when no graph is loaded.

### Side Panel & Inspector

**`SidePanel.cs`** — The right-side panel container VisualElement. Manages collapsed (thin bar) / expanded (full panel) states with a toggle button. Contains an internal splitter between the `DetailsPanel` (top) and `BlackboardPanel` (bottom), plus a draggable left-edge splitter to resize the entire panel width. `SetExpanded()`, `UpdateVisibility()`, `SyncFromWindow()` (reads saved height ratio), `UpdateSelection()` (delegates to DetailsPanel), `UpdateBlackboard()` (delegates to BlackboardPanel).

**`DetailsPanel.cs`** — The inspector VisualElement inside the side panel. For a normal state selection, displays name, position, and a multi-slot behaviour list with script pickers, property editors, per-slot remove buttons, drag-to-reorder handles, and a "+ Add Behaviour" button. For external-reference states, displays target GameObject field, action type dropdown, and conditional parameter fields. For a single connection selection, displays the transition direction and a list of condition entries (script picker, properties, remove button, drag-to-reorder handles) plus an "Add Condition" button. For a single group selection, displays label, color picker, member list. For multi-selection, shows a summary badge list. Conditions and behaviours support drag-to-reorder via the `_conditionEntryElements`/`_behaviourEntryElements` reorderable lists with auto-scroll and `container.userData` index tracking. All property editing reads/writes via `SerializedObject` on the ScriptableObject instances.

**`BlackboardPanel.cs`** — The blackboard sub-panel VisualElement. Lists all blackboard variables with type-appropriate inline editors: toggle for Bool, numeric field for Int/Float, text field for String, axis fields for Vector2/Vector3. Supports: adding variables via a dropdown (all six types), inline rename on double-click/F2, row selection (click) and deletion (Delete key), drag-to-reorder rows, and inline value editing. All mutations create undoable commands (`DeleteBlackboardVariableCommand`, `ModifyBlackboardVariableCommand`).

### Expanded View & Play Mode

**`ExpandedViewManager.cs`** — Manages the sub-state-machine drill-down feature. Maintains an expansion stack; `EnterExpandSubState()` pushes a sub-machine state and animates focus to its children. `ExitExpandedSubState()` pops and animates back. `IsStateVisible()` and `IsConnectionVisible()` filter what's rendered based on the current expansion context. `ComputeVisibleContentBounds()` calculates the bounding rect of visible states for camera animations. `UpdateExpandedModeBar()` builds/hides the breadcrumb navigation bar. `FindActiveStateHierarchy()` traces the parent chain from a leaf state through sub-machine containers (used during play mode auto-expand).

**`PlayModeTracker.cs`** — Bridges the editor graph view with runtime play-mode state. `OnEditorUpdate()` (called each editor frame) polls the active `StateMachineComponent`, detects active state changes via `StateEnterTime`, updates the `IsActive` flag on StateViews (triggering glow animations), highlights recently-active connections (triggering wave effects), auto-expands into the correct sub-state hierarchy when the active state is nested, and tracks `RecentTransitions` for deferred visual effects. `OnPlayModeStateChanged()` handles enter/exit play mode: saves data before entering, clears play-mode visual state on exit. Subscribes to `StateMachineComponent.OnStateEnteredGlobal` for breakpoint detection — when a breakpoint state is entered, pauses the editor, selects the corresponding GameObject, and opens/focuses the graph window.

**`GraphViewAnimator.cs`** — Smooth animated camera transitions using cubic ease-in-out interpolation. `StartSmoothFocusOnContent()` computes target pan/zoom to frame given content bounds. `UpdateAnimation()` advances the animation each frame, interpolating pan offset and zoom between current and target values. Used when entering/exiting sub-state-machines and after loading a controller.

### Other Utilities

**`ISelectable.cs`** — Interface contract for all selectable graph items. Properties: `bool IsSelected`, `Vector2 Position`, `Vector2 Size`. Methods: `Rect GetGraphBounds()`, `bool ContainsPoint(Vector2 graphPoint)`, `void DrawSelectionOverlay(float zoom, Vector2 panOffset)`. Implemented by StateView, ConnectionView, CommentGroupView.

**`IContextMenuProvider.cs`** — Extension point interface. Single method: `void AddItemsToMenu(MenuDropdown.IBuilder menu, Vector2 graphMousePosition)`. Implementors are discovered via `TypeCache.GetTypesDerivedFrom<>` at editor startup and can inject custom items into the graph's right-click context menu.

**`MenuDropdown.cs`** — Reusable dropdown menu system built on VisualElement. Static `Show()` method creates a full-screen overlay (click-to-dismiss) and a positioned menu panel. Passes an `IBuilder` interface to the caller for adding items (`AddItem` with label + action), separators (`AddSeparator`), and disabled items (`AddDisabledItem`). Auto-repositions if the menu would overflow screen edges. Used by `GraphContextMenu` and available for extensions.

**`ShortcutGuide.cs`** — Modal overlay that displays all keyboard shortcuts in a clean grouped list. Triggered via `Ctrl+/` or the `?` button on the DetailsPanel header. Shows a dark semi-transparent backdrop with a centered panel listing shortcuts by category (General, States, Navigation). Each entry shows the key combination (bold yellow) and description (light gray). Closes on Escape, clicking the backdrop, or the X button.

**`GraphSearchPanel.cs`** — Floating search overlay (Ctrl+F or magnifying glass icon) that searches across all graph elements: state names, behaviour script types (TypeName and DisplayName), condition script types (TypeName and DisplayName), connection endpoints, and blackboard variable names. Shows a centred input panel with live-filtered results in a scrollable list, each row displaying a type icon, display text, detail text, context path (breadcrumb for nested states), and a type badge. Result rows are keyboard-navigable (Up/Down, Enter to select) and hover-responsive. Clicking a result navigates the graph: auto-enters sub-state hierarchies via ExpandedSubStateStack, selects and smooth-focuses to the target with a highlight blink (bright cyan border on StateView for 1.5s, cyan-colored connection rendering with blink pulses), and opens the side panel for variable selections. Closes on Escape or click-off.

**`GraphSearchPanel.uss`** — USS styles for the search panel overlay, input field, result rows with hover/selection states, type badges, close button, no-results label, and the `state-view__fill--search-highlight` class used for the blink animation on highlighted states.

**`GraphValidation.cs`** — Graph analysis module for detecting structural issues. Computes `StateValidationStatus` (Ignored = no connections at all, Unreachable = outgoing-only, DeadEnd = incoming-only) per state node and updates visual color coding + warning/error icon overlays on the state node via USS classes. Provides static validation methods consumed by DetailsPanel to display error/warning messages about null MonoScript references on behaviour/condition entries, broken external reference targets, missing entry points, duplicate state names, and missing blackboard variable references on behaviour/condition serialized properties. Integrates with `CleanStateMachineWindow` via dirty-flag pattern — `MarkDirty()` is called on graph mutations and `RunAndUpdate()` is invoked each frame in `OnGUI` to apply validation visuals.

**`ScriptReferenceUtility.cs`** — Static utility for resolving type identity to Unity asset references. `GetTypeName(MonoScript)` returns the fully qualified type name. `FindScriptByTypeName(string)` searches all loaded MonoScripts in the AssetDatabase for a matching type (used when deserializing behaviour/condition type strings). `LoadStyleSheet(string)` loads a USS file by name from the package's Styles folder. `FindAssetPath(string)` locates an asset by filename via AssetDatabase search.

**`EntryExtensions.cs`** — Extension methods in the Editor assembly that bridge the unified `BehaviourEntry`/`ConditionEntry` Runtime types with `MonoScript` references. `GetScript()` resolves a `MonoScript` from the entry's `TypeName` via `ScriptReferenceUtility.FindScriptByTypeName`. `SetScript()` sets the entry's `TypeName` from a `MonoScript` via `ScriptReferenceUtility.GetTypeName`. This eliminates the need for separate Editor-only view classes (`BehaviourEntryView`/`ConditionEntryView`) and avoids conversion overhead in the save/load cycle.

**`MonoScriptCache.cs`** — Static cache for MonoScripts filtered by base type (`StateBehaviour`, `ConditionScript`). `GetScriptsByBaseType<T>()` returns a sorted, cached list of MonoScripts with types derived from `T`. The cache auto-refreshes via a nested `AssetPostprocessor` subclass that invalidates the cache when `.cs` files are imported or deleted. Used by `DetailsPanel.FindFilteredScripts()` to avoid scanning every MonoScript every dropdown open — `AssetDatabase.FindAssets("t:MonoScript")` + `script.GetClass()` per script is now paid only when the asset database changes.

### Custom Inspectors

**`StateMachineControllerEditor.cs`** — Custom `Editor` for `StateMachineController` (`[CustomEditor(typeof(StateMachineController))]`). Replaces the default inspector with a button that opens the graph editor and explanatory text. Exposes `CreateInspectorGUI()` returning a VisualElement.

**`StateMachineComponentEditor.cs`** — Custom `Editor` for `StateMachineComponent` (`[CustomEditor(typeof(StateMachineComponent))]`). Shows the assigned controller with "Open Graph" button to launch the graph editor, the current state name (live-updating), and an editable list of all blackboard variables with type-appropriate input fields. Uses `EditorApplication.update` for periodic refresh at runtime.

**`StateMachineActionEditor.cs`** — Custom `Editor` for `StateMachineAction` and its subclasses (`[CustomEditor(typeof(StateMachineAction), true)]`). Provides a UI to select a `StateMachineComponent` reference, then picks a specific blackboard variable of the type required by the action (determined by `RequiredVariableType`). Shows a variable-type badge and a clear-selection button.

---

## UNDO/REDO SYSTEM

The undo/redo system uses the classic Command pattern. Every reversible graph mutation is encapsulated as an `IUndoableCommand` with `Execute()`, `Undo()`, `Redo()`, and a `Description`. The `UndoRedoSystem` maintains two stacks (undo and redo) with a configurable max history (default 50), and fires a `HistoryChanged` event for UI updates. `CompositeCommand` groups multiple commands into one atomic undo step (e.g., deleting a state also deletes its connections and removes it from groups). All graph changes in `GraphOperations` flow through `UndoRedoSystem.Execute()` — views are never directly modified. Each command captures enough state at construction time to fully reverse the operation (positions, indices, cloned variables, etc.).

### Commands

**`IUndoableCommand.cs`** — Interface: `void Execute()`, `void Undo()`, `void Redo()`, `string Description { get; }`. The contract all commands implement.

**`UndoRedoSystem.cs`** — Central manager. Two `Stack<IUndoableCommand>` (undo + redo), `int MaxHistory` (min 1). `Execute(command)` runs the command, pushes to undo, clears redo, trims overflow. `Undo()` pops from undo, calls Undo(), pushes to redo. `Redo()` pops from redo, calls Redo(), pushes to undo. `Clear()` empties both. `GetUndoDescription()` / `GetRedoDescription()` for UI labels (e.g., "Undo Delete 3 Item(s)"). Fires `HistoryChanged` event.

**`CompositeCommand.cs`** — Wraps a list of sub-commands. `Execute()` runs all forward; `Undo()` runs all in reverse (LIFO); `Redo()` runs all forward. Accepts commands via constructor array or `Add()`. Description is set at construction.

**`CreateStateCommand.cs`** — Adds/removes a `StateView` to/from the window's `States` list. Description: "Create State '<name>'".

**`CreateConnectionCommand.cs`** — Adds/removes a `ConnectionView` to/from the window's `Connections` list. Description: "Create Connection".

**`CreateGroupCommand.cs`** — Adds/removes a `CommentGroupView` to/from the window's `Groups` list. Description: "Create Group '<label>'".

**`DeleteStatesCommand.cs`** — The most complex command. On `Execute()`, removes all selected states, connections incident to those states, and selected groups from their respective lists, removes deleted states from any groups they belonged to, and removes deleted states from parent sub-state-machine `ChildIndices`. At construction, also captures parent-child relationships (which sub-state-machine containers had deleted states as children) and each deleted state's `IsSubEntry` flag, enabling full restoration on `Undo()` — including restoring child indices to parent containers and restoring `IsSubEntry`. Description: "Delete N Item(s)".

**`DeleteConnectionCommand.cs`** — Removes a single `ConnectionView` from the list. `Undo()` re-adds it. Description: "Delete Connection".

**`DeleteBlackboardVariableCommand.cs`** — Clones the variable at construction, removes it from the list by index. `Undo()` inserts the clone at the original index. Description: "Delete Variable".

**`MoveStatesCommand.cs`** — Captures start and end positions for a list of `ISelectable` items (states and groups). `Execute()` applies end positions; `Undo()` restores start positions. Supports mixed selections. Description: "Move N State(s)".

**`RenameStateCommand.cs`** — Captures old and new name strings for a `StateView`. `Execute()` sets new; `Undo()` restores old. Description: "Rename state to '<newName>'".

**`RenameGroupCommand.cs`** — Captures old and new labels for a `CommentGroupView`. Same pattern as rename state. Description: "Rename Group '<oldLabel>'".

**`ModifyGroupColorCommand.cs`** — Snaps the current `GroupColor` at construction, then applies the new color. `Undo()` restores the old color. Description: "Change Group Color".

**`ModifyBlackboardVariableCommand.cs`** — Captures old and new `StringValue` for a `BlackboardVariable`. Description: "Modify Variable".

**`RenameBlackboardVariableCommand.cs`** — Captures old and new `Name` for a `BlackboardVariable`. Description: "Rename Variable". Used by `BlackboardPanel.CommitNameEdit()` to make variable renames undoable, replacing a direct field assignment that bypassed the command system.

**`ResizeGroupCommand.cs`** — Captures old and new `Rect` for a `CommentGroupView`. `Execute()` calls `SetRect(newRect)`; `Undo()` calls `SetRect(oldRect)`. Description: "Resize '<label>'".

**`UngroupCommand.cs`** — Removes a `CommentGroupView` from the list without deleting its member states. `Undo()` re-adds the group. Description: "Ungroup '<label>'".

**`ToggleBreakpointCommand.cs`** — Toggles a breakpoint on a state by adding/removing its DataIndex from the window's `BreakpointStateIndices` set and syncing visual indicators. Supports undo/redo with descriptions "Add Breakpoint (N)" / "Remove Breakpoint (N)".

---

## BREAKPOINT SYSTEM

The breakpoint system allows users to pause the Unity Editor when a specific state is entered during play mode. Breakpoints are persisted with the controller asset and support undo/redo.

### Data Model
- `BreakpointData` (`Runtime/BreakpointData.cs`) stores a `StateIndex` (index in the serialized `Data.States` list) and an optional `ParentPath` for future hierarchical support.
- Breakpoints are stored in `SerializableData.Breakpoints` as a `List<BreakpointData>`.

### Editor UI
- Right-click a state in the graph → "Add Breakpoint" / "Remove Breakpoint" toggles a breakpoint.
- A red circular indicator appears at the top-left corner of the state node when a breakpoint is set (USS class `state-view__breakpoint`).
- Breakpoint toggling is handled by `ToggleBreakpointCommand` (undo/redo support).
- Breakpoints are tracked in `CleanStateMachineWindow.BreakpointStateIndices` (HashSet of DataIndex values).

### Play Mode Pausing
- `StateMachineComponent` fires a static `OnStateEnteredGlobal` event whenever a state is entered (in `TransitionToState`, `TransitionToStateDirect`, and `Initialize`).
- `PlayModeTracker` subscribes to this event on window creation and checks if the entered state has a breakpoint.
- When a breakpoint hits: the editor pauses (`EditorApplication.isPaused = true`), the correct GameObject is selected, and the graph window opens/focuses.
- A `TriggeredBreakpointIndices` HashSet prevents re-pausing when the user resumes while on the same state. It is cleared when `ActiveStateIndex` changes (state transition detected in `OnEditorUpdate`).
- Breakpoints for deleted states are cleaned up in `GraphOperations.DeleteSelected()` and during `GraphSerializer.SaveCurrentData()` (by only saving breakpoints matching existing states).

---

## STYLES SYSTEM

USS (Unity Style Sheets) files in `Editor/Styles/` define the visual appearance of every UI Toolkit element in the editor. Stylesheets are loaded at runtime via `ScriptReferenceUtility.LoadStyleSheet()` using the `Resources.FindObjectsOfTypeAll` + `AssetDatabase.GetAssetPath` pattern. Each USS file maps to a specific VisualElement class or panel.

**`StateView.uss`** — State node styling: absolute positioning, shadow, yellow glow (animated on highlight), rounded-rectangle fill with color variants (dark for default, green for entry, orange/brown for sub-machine, gold for sub-entry, lighter for active), blue border highlight for selected, centered white label, transparent inline-edit text field. Also contains breadcrumb bar selectors (horizontal flex row, clickable items with hover underline, dim separators).

**`CommentGroupView.uss`** — Group node styling: semi-transparent dark header bar, bold left-aligned label with ellipsis, transparent inline-edit text field with no visible borders.

**`SidePanel.uss`** — The largest stylesheet. Panel structure (expanded/collapsed, edge splitter, shadow), headers, toggle/resize handles, details panel (scroll, sections, dividers, empty states, script picker dropdown, behaviour entry cards with per-slot remove buttons, add-behaviour button, property cards, condition entry cards with remove button, add-condition button, sub-machine enter button), blackboard panel (variable rows with selection/drag state, drag handle, inline name/value editors, axis fields, type badges, add button), blackboard variable reference editor (mode toggle, dropdown), and multi-selection summary rows.

**`GraphPreview.uss`** — Minimap styling: 200x150px absolutely-positioned box, semi-transparent dark background, rounded corners, gray border, hidden state via display:none.

**`MenuDropdown.uss`** — Context menu styling: full-screen transparent overlay (click-to-dismiss), absolutely-positioned dark panel with rounded corners and border, 24px selectable rows with blue hover/active highlight, 11px light-gray labels with ellipsis, horizontal separator lines, grayed-out disabled items.

**`ComponentInspector.uss`** — StateMachineComponent inspector: padded column layout, dark state info container, bold state name label, variable foldout with scroll view, compact variable rows with hover effect, small blue type badges, value containers with nested styling for Toggle/Vector2/Vector3/Integer/Float/TextField inputs, muted help text, empty placeholder.

**`ControllerInspector.uss`** — Controller inspector: padded column layout, centered "Open" button (32px, dark background, hover/active states), muted explanatory text.

**`StateMachineActionInspector.uss`** — Action inspector: padded column layout, bold header with bottom border, dropdown/button styling for action type picker, blue-tinted selection card with uppercase label, variable type badge, red-tinted clear button with hover highlight, help text.

---

## SCRIPT TEMPLATES SYSTEM

The `Editor/ScriptTemplates/` directory provides code generation for user-authored behaviour and condition scripts. Two `.txt` templates contain skeleton C# classes with `#SCRIPTNAME#` placeholders for the filename. `StateMachineScriptCreation.cs` registers two `[MenuItem]` entries under Assets > Create > Clean State Machine — "State Behaviour" and "Condition Script" — which invoke `ProjectWindowUtil.CreateScriptAssetFromTemplateFile` to instantiate the template in the user's project.

**`ConditionScriptTemplate.txt`** — Skeleton class inheriting from `ConditionScript` with a stub `Evaluate()` method returning `true`.

**`StateBehaviourTemplate.txt`** — Skeleton class inheriting from `StateBehaviour` with stubs for `OnStateEnter`, `OnStateUpdate`, `OnStateExit` (all calling `base`).

**`StateMachineScriptCreation.cs`** — Static class with two `[MenuItem]` static methods that locate the template `.txt` files via `ScriptReferenceUtility.FindAssetPath` and call `ProjectWindowUtil.CreateScriptAssetFromTemplateFile`.

---

## BEHAVIOURS SYSTEM (Example Scripts)

The `Assets/` folder (with its own `CleanStateMachine.Behaviours.asmdef` referencing `CleanStateMachine.Runtime`) contains example scripts that demonstrate how users can extend the state machine. These are not part of the core package — they serve as reference implementations and are the target assembly for user-authored behaviours. Three categories mirror the three scriptable extension points.

**`TriggerEnter_SetBool.cs`** — Inherits from `StateMachineAction`. Responds to `OnTriggerEnter` by writing a serialized `bool _value` (default `true`) into its assigned blackboard variable. Requires a Bool-typed variable. Demonstrates how external MonoBehaviours can react to physics events by modifying state machine state.

**`UltimateCompare_ConditionBehaviours.cs`** — Inherits from `ConditionScript`. Unified condition that supports all six variable types (Bool, Int, Float, String, Vector2, Vector3) in a single script. User selects `variableType` to determine the comparison mode, then configures two `BlackboardVariableReference` inputs (each can be a direct value or blackboard variable) and a `CompareType` (Equal, NotEqual, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual). Bool/String types only use Equal/NotEqual. Float comparisons use `Mathf.Approximately` for equality. Vector ordering comparisons use `sqrMagnitude`. String comparisons support `ignoreCase` toggle. Display name: "Compare Variable".

**`DebugLog_StateBehaviour.cs`** — Inherits from `StateBehaviour`. Overrides `OnStateEnter` to log a configurable string `message` (default "Hello World") via `Debug.Log`. `OnStateUpdate` and `OnStateExit` only call base. Simple diagnostic tool for tracing state machine flow.

**`SetVariable_StateBehaviour.cs`** — Inherits from `StateBehaviour`. Overrides `OnStateEnter` to write a `value` into a `target` blackboard variable on the same state machine. `target` is a `BlackboardVariableSelector` (rendered as a dropdown of all blackboard variables), `value` is a `BlackboardVariableReference` (direct value or reference to another variable).

---

## DEMO

The `Demo/` folder contains a single example `StateMachineController` asset (`NewStateMachineController.asset`) that can be used for testing and as a starting point for new graphs.

## Files/Folders to Ignore
- `Assets/` — standard Unity user-content folder; may contain test scenes but is not part of the package product
- `ProjectSettings/`, `Library/`, `Temp/`, `Logs/`, `UserSettings/` — Unity-generated project data; never read these
- `UIElementsSchema/` — auto-generated UI Toolkit schema
- `Assembly-CSharp.csproj`, `Assembly-CSharp-Editor.csproj` — auto-generated C# project files
- `CleanStateMachine.*.csproj` — auto-generated from asmdef files
- `*.slnx` — solution file for IDE
- `.gitignore`, `.cgcignore`, `.vscode/`, `.kilo/` — config and ignore files
- `AGENTS.md` — AI agent instructions
- `compile_log.txt` — build log artifact
- `REVIEW.md` — Technical review document covering performance issues, bad practices, and missing features for a commercial state machine graph. Generated by AI review of the full codebase.