# PROJECT INDEX: Clean State Machine

## Package
- **Name**: `com.cleanstatemachine` (Clean State Machine)
- **Version**: 1.0.0
- **Unity**: 6000.0
- **Path**: `Packages/com.cleanstatemachine/`
- **Dependencies**: None

## Assembly Definitions
| Assembly | Path | Type |
|---|---|---|
| `CleanStateMachine.Runtime` | `Runtime/` | Runtime logic (MonoBehaviour + ScriptableObject) |
| `CleanStateMachine.Editor` | `Editor/` | Editor tooling (UITK graph editor) |

## Namespace
All code lives under `namespace CleanStateMachine`.

---

## Runtime Layer (10 files)

| File | Type | Role |
|---|---|---|
| `StateMachineController` | `ScriptableObject` | Asset. Owns `SerializableData`. Rebuilds behaviour/condition instances in editor. |
| `StateMachineComponent` | `MonoBehaviour` | Runtime driver. Attached to GameObjects. Builds `RuntimeStateMachine`, drives update loop, exposes blackboard API. |
| `StateMachineData` | Data classes | `SerializableData`, `SubStateMachineData`, `StateData`, `ConnectionData`, `ConditionEntry`, `GroupData`. Pure serialization POCOs. |
| `StateBehaviour` | `abstract ScriptableObject` | Hooks: `OnStateEnter/Update/Exit(StateMachineComponent)`. User-extensible. |
| `ConditionScript` | `abstract ScriptableObject` | `Evaluate(StateMachineComponent) -> bool`. User-extensible. |
| `StateMachineAction` | `abstract MonoBehaviour` | Read/write blackboard vars from any GameObject. Has `RequiredVariableType`. |
| `RuntimeStateMachine` | `internal` class | Recursive FSM engine. Contains `RuntimeState[]`, `RuntimeTransition[]`. Handles hierarchy, transitions, path tracking. |
| `BlackboardVariable` | `[Serializable]` | Typed var (Bool/Int/Float/String/Vector2/Vector3). Stores value as `StringValue`. |
| `BlackboardVariableReference` | `[Serializable]` | Either direct value or blackboard binding. Resolves via `StateMachineComponent`. |

## Editor Layer (19 files + 13 commands + 7 styles + 3 script templates)

### Graph Window

| File | Role |
|---|---|
| `CleanStateMachineWindow` | `EditorWindow`. Entry point (Tools/CleanStateMachine). Hosts all UI, input handling, save/load, clipboard, runtime debug. |
| `SidePanel` | Right-side host containing `DetailsPanel` (top) + `BlackboardPanel` (bottom), draggable splitter. |

### Graph Structure

| File | Role |
|---|---|
| `StateView` | `VisualElement + ISelectable`. Represents one state node. Handles rendering, inline rename, active animation. |
| `ConnectionView` | `ISelectable`. Logical connection between two states. Stores condition entries, hit-testing. |
| `ConnectionArrowsLayer` | `VisualElement`. Renders all connection lines + arrows + active wave animation (Mesh API). |
| `CommentGroupView` | `VisualElement + ISelectable`. Rectangular group overlay. Contains member states, inline rename, color, resize. |
| `GridBackground` | `VisualElement`. Minor/major grid lines (Mesh API). |

### Controllers

| File | Role |
|---|---|
| `ConnectionController` | Manages "connecting mode": source state, pending line, completion hit-test. |
| `DragController` | Handles drag-move of selected items (states/groups). Threshold gated. |
| `GraphPanController` | Right-click drag to pan, scroll-wheel zoom with zoom-to-mouse. |
| `SelectionController` | Manages `List<ISelectable>` selection with events. |
| `SelectionBox` | Rubber-band selection rect (screen-space). |

### Panels & Inspectors

| File | Role |
|---|---|
| `DetailsPanel` | Context inspector: shows state info/behaviour assignment, connection conditions, group color/members, multi-select list. |
| `BlackboardPanel` | Blackboard variable list: add/delete/reorder/double-click rename/edit values. |
| `StateMachineComponentEditor` | Custom Inspector for `StateMachineComponent`: shows current state, runtime variable values (play mode), edit variables (edit mode). |
| `StateMachineControllerEditor` | Custom Inspector for `StateMachineController`: "Open in Graph Editor" button. |
| `StateMachineActionEditor` | Custom Inspector for `StateMachineAction`: blackboard variable picker dropdown. |

### Utilities & Menu

| File | Role |
|---|---|
| `GraphContextMenu` | Right-click menu builder. Fires events for Create/Copy/Paste/Delete/Connect/Ungroup. Extensible via `IContextMenuProvider`. |
| `IContextMenuProvider` | Interface for 3rd-party menu extensions. |
| `MenuDropdown` | Static UITK dropdown menu builder. |
| `ScriptReferenceUtility` | Static helpers: `GetTypeName()`, `FindScriptByTypeName()`, `LoadStyleSheet()`, `FindAssetPath()`. |
| `StateMachineAssetHandler` | `[OnOpenAsset]`: double-click controller asset opens graph window. |
| `ISelectable` | Interface for selectable graph items (Position, Size, IsSelected, HitTest). |

### Undo/Redo System (13 files in `Editor/UndoRedo/`)

| File | Undoes |
|---|---|
| `IUndoableCommand` | Interface: `Execute/Undo/Redo/Description` |
| `UndoRedoSystem` | Two-stack (50 max) manager. |
| `CompositeCommand` | Groups multiple commands as one unit. |
| `CreateStateCommand` | Add/remove from `_states` |
| `CreateConnectionCommand` | Add/remove from `_connections` |
| `CreateGroupCommand` | Add/remove from `_groups` |
| `DeleteStatesCommand` | Remove states + cascading connections + group memberships |
| `DeleteConnectionCommand` | Remove/add single connection |
| `DeleteBlackboardVariableCommand` | Remove/insert var with clone |
| `ModifyBlackboardVariableCommand` | Change `StringValue` (store old/new) |
| `MoveStatesCommand` | Restore start/end positions for `ISelectable[]` |
| `RenameStateCommand` | StateView name old/new |
| `RenameGroupCommand` | CommentGroupView label old/new |
| `ResizeGroupCommand` | Group rect old/new |
| `ModifyGroupColorCommand` | Group color old/new |
| `UngroupCommand` | Remove/add group from `_groups` |

### Script Templates (in `Editor/ScriptTemplates/`)

| File | Menu Path |
|---|---|
| `StateMachineScriptCreation` | Assets/Create/Clean State Machine/State Behaviour & Condition Script |
| `StateBehaviourTemplate.txt` | Template for `StateBehaviour` subclass |
| `ConditionScriptTemplate.txt` | Template for `ConditionScript` subclass |

### Styles (in `Editor/Styles/`)

| USS File | Target |
|---|---|
| `StateView.uss` | StateView visual tree |
| `SidePanel.uss` | SidePanel layout |
| `ComponentInspector.uss` | StateMachineComponentEditor |
| `ControllerInspector.uss` | StateMachineControllerEditor |
| `StateMachineActionInspector.uss` | StateMachineActionEditor |
| `MenuDropdown.uss` | MenuDropdown overlay |
| `CommentGroupView.uss` | CommentGroupView |

---

## Key Types

### Data Model Hierarchy
```
SerializableData
  ├── RootMachine: SubStateMachineData
  │     ├── States: List<StateData>
  │     │     └── (nested) SubStateMachine: SubStateMachineData (recursive)
  │     ├── Connections: List<ConnectionData>
  │     │     └── Conditions: List<ConditionEntry>
  │     └── Variables: List<BlackboardVariable>
  ├── Groups: List<GroupData>
  ├── PanOffset, Zoom, ShowSidePanel, SidePanelWidth, DetailsHeightRatio
```

### Runtime Model Hierarchy
```
RuntimeStateMachine
  ├── States: RuntimeState[]
  │     ├── Behaviour: StateBehaviour
  │     └── NestedMachine: RuntimeStateMachine (recursive)
  ├── Transitions: RuntimeTransition[]
  │     ├── From/To: RuntimeState
  │     └── Conditions: ConditionEntry[]
  └── Variables: List<BlackboardVariable>
```

### Editor Graph Hierarchy
```
CleanStateMachineWindow
  ├── GridBackground (Mesh)
  ├── groupContainer: contains CommentGroupView[]
  ├── ConnectionArrowsLayer (Mesh)
  ├── stateLayer: contains StateView[]
  ├── IMGUIContainer (selection overlays + resize cursors)
  ├── expandedModeBar (sub-state breadcrumb)
  ├── SelectionBox.Element
  ├── SidePanel
  │     ├── DetailsPanel (state/connection/group inspector)
  │     ├── internalSplitter (draggable)
  │     └── BlackboardPanel (variable list)
  └── _states, _connections, _groups (logical lists)
```

### Blackboard Variable Type System
```
BlackboardVariableType: Bool | Int | Float | String | Vector2 | Vector3
```
All stored as `StringValue` (serialization-friendly). Typed accessors parse it.

### Selection System
```
ISelectable (interface)
  ├── StateView
  ├── ConnectionView
  └── CommentGroupView
```
Managed by `SelectionController`. Used by `DragController`, `DetailsPanel`, `DeleteStatesCommand`.

### Extensibility Points
- `StateBehaviour` subclass → custom per-state logic
- `ConditionScript` subclass → custom transition conditions
- `StateMachineAction` subclass → custom action MonoBehaviour
- `IContextMenuProvider` → custom context menu items
- `BlackboardVariableReference` → data binding in `ConditionScript` fields

---

## Additional Systems

### Event System
| Source | Event | Args |
|---|---|---|
| `StateMachineComponent` | `OnStateChanged` | `int previousGlobalIndex, int newGlobalIndex` |
| `StateMachineComponent` | `OnStateEntered` | `string stateName` |
| `StateMachineComponent` | `OnStateExited` | `string stateName` |
| `RuntimeMachineContext` | `OnTransition()` callback | full state+path+index info (via `StateMachineComponent.OnRuntimeTransition`) |
| `SelectionController` | `SelectionChanged` | none (check `Selected`) |
| `UndoRedoSystem` | `HistoryChanged` | none |

### TransitionRecord System
`StateMachineComponent.TransitionRecord` stores:
- `FromIndex/ToIndex/ConnectionIndex`
- `FromStateName/ToStateName`
- `FromPath/ToPath`

Collected in `_recentTransitions` list. Read by editor window for connection activation animation.

### Clipboard Systems
- **State clipboard**: `CopiedStateData` (private class in `CleanStateMachineWindow`). Stores position, name, size, `MonoScript`, `StateBehaviour`, child indices, sub-state-machine flag. Supports copy, paste (with offset to mouse), duplicate.
- **Condition clipboard**: `_conditionClipboard` (static in `DetailsPanel`). Copies `MonoScript` + serialized `ConditionScript` instance. Paste appends or inserts.

### Variable Scoping
Each `SubStateMachineData` has its own `Variables` list. At runtime, `FindVariableRecursive()` walks the active hierarchy (current scope → active child scope → ...) to resolve a variable by name. `CollectVariables()` flattens all scopes for display/export.

### Sub-Asset Lifecycle
`StateMachineController` manages `StateBehaviour` and `ConditionScript` instances as hidden sub-assets (`HideFlags.HideInHierarchy`) of the `.asset` file via `EnsureSubAssets()` / `CollectReferencedSubAssets()`. Orphaned sub-assets are destroyed. `RebuildBehaviourInstances()` creates instances from `StateData.BehaviourType` / `ConditionEntry.TypeName` strings.

### Flat Index System
Groups use flat (recursive) indices to track member states. `FindFlatIndexRecursive()` walks the full `SubStateMachineData` tree depth-first, incrementing a counter. Saved in `GroupData.MemberIndices`. Rebuilt on save via `SaveGroups()`.

### StateView Animation
| Animation | Trigger | Effect |
|---|---|---|
| Active glow | `IsActive = true` | Pulsing CSS opacity (sine wave, 2.5Hz, 35-85% alpha) on `_glow` element |
| Brief flash | `IsActive` → `false` within 200ms | Blinks opacity down over 250ms, then hides |
| Sub-state icon | `IsSubStateMachine = true` | Shows `↗` label overlay |

### Connection Animation
| Animation | Trigger | Effect |
|---|---|---|
| Active wave | `IsActive = true` | Dots (5 circles) travel along line at 0.8 speed. Burst: 250ms width/scale ramp, white flash, then fade over 3s. |

### State Path System
`GetActiveStatePath()` returns `"LeafState"` for flat machines or `"ParentState/ChildState"` for composite. Used by:
- `StateMachineComponent.CurrentStatePath` (public API)
- Editor debug visualization (auto-navigation, active state highlight)
- TransitionRecord tracking

### Entry State
- Created automatically by `EnsureEntryStateExists()` if none exists
- Rendered with distinct style (`state-view__fill--entry`)
- Always kept at index 0 in `_states` list
- May only have one outgoing connection (auto-replaced on creating new)
- Protected from deletion by `DeleteSelected()`
- Not draggable into groups

### Hierarchy Flattening
On load, `FlattenHierarchy()` does a DFS walk of `SubStateMachineData` tree, producing a flat `List<StateView>`. Each state tracks its `ParentScope` and `LocalIndexInScope`. Visibility is determined by `_viewScope` filter. `FlattenConnections()` similarly flattens all scope-local transition data.

### Lazy Initialization
`StateMachineComponent` uses a two-phase init: `Awake()` calls `Initialize()` guarded by `_initialized` flag. If `_controller` is null at Awake, `Start()` retries. `ResetStateMachine()` re-runs the full cycle.
