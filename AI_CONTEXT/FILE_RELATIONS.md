# FILE RELATIONS

## Dependency Graph

```
Runtime/
├── StateMachineController.cs
│   ← StateMachineData.cs (owns SerializableData)
│   ← StateBehaviour.cs (type check, instantiation)
│   ← ConditionScript.cs (type check, instantiation)
│   → ScriptReferenceUtility.cs (editor-time, via UNITY_EDITOR)
│
├── StateMachineComponent.cs
│   ← RuntimeStateMachine.cs (builds + drives it)
│   ← StateMachineController.cs (reads Data from)
│   ← BlackboardVariable.cs (typed accessors → FindVariable)
│
├── StateMachineData.cs  [leaf - depended upon by everyone]
│   Define: SerializableData, SubStateMachineData, StateData,
│           ConnectionData, ConditionEntry, GroupData
│
├── RuntimeStateMachine.cs
│   ← StateMachineData.cs (Build() takes SubStateMachineData)
│   ← ConditionScript.cs (Evaluate on transitions)
│   ← StateBehaviour.cs (OnEnter/Update/Exit hooks)
│   ← BlackboardVariable.cs (clones, owns list)
│
├── StateBehaviour.cs  [leaf - depended upon by StateMachineComponent]
│
├── ConditionScript.cs  [leaf - depended upon by RuntimeStateMachine]
│
├── BlackboardVariable.cs  [leaf - depended upon by Runtime/Editor]
│
├── BlackboardVariableReference.cs
│   ← StateMachineComponent.cs (resolves bindings)
│   ← Runtime/BlackboardVariable.cs (type enum)
│
└── StateMachineAction.cs
    ← StateMachineComponent.cs (Set/GetParameter)
    ← BlackboardVariable.cs (type enum)

Editor/
├── CleanStateMachineWindow.cs
│   ← StateMachineController.cs (load/save)
│   ← StateMachineData.cs (SerializableData manipulation)
│   ← StateView.cs (creates/manages)
│   ← ConnectionView.cs (creates/manages)
│   ← CommentGroupView.cs (creates/manages)
│   ← GridBackground.cs (owns)
│   ← ConnectionArrowsLayer.cs (owns)
│   ← SidePanel.cs (owns)
│   ← SelectionController.cs (owns)
│   ← DragController.cs (owns)
│   ← SelectionBox.cs (owns)
│   ← ConnectionController.cs (owns)
│   ← GraphPanController.cs (owns)
│   ← GraphContextMenu.cs (owns)
│   ← UndoRedoSystem.cs (owns)
│   ← All 13 command classes (imports via namespace)
│   ← ScriptReferenceUtility.cs (GetTypeName, FindScriptByTypeName)
│   ← BlackboardVariable.cs (GetBlackboardVariables)
│
├── SidePanel.cs
│   ← CleanStateMachineWindow.cs (accessors)
│   ← DetailsPanel.cs (owns)
│   ← BlackboardPanel.cs (owns)
│
├── DetailsPanel.cs
│   ← CleanStateMachineWindow.cs (accessors)
│   ← StateView.cs (builds state inspector)
│   ← ConnectionView.cs (builds condition editor)
│   ← CommentGroupView.cs (builds group inspector)
│   ← BlackboardVariable.cs (var ref picker)
│   ← ScriptReferenceUtility.cs (FindFilteredScripts)
│   ← MenuDropdown.cs (shows script picker)
│   ← ConditionScript.cs (GetConditionDisplayName)
│   ← UndoRedo commands (ModifyGroupColorCommand, etc.)
│
├── BlackboardPanel.cs
│   ← CleanStateMachineWindow.cs (accessors)
│   ← BlackboardVariable.cs (data source)
│   ← MenuDropdown.cs (type selector)
│   ← UndoRedo commands (ModifyBlackboardVariableCommand, DeleteBlackboardVariableCommand)
│
├── StateView.cs
│   ← StateMachineData.cs (StateData reference via SourceData)
│   ← StateBehaviour.cs (BehaviourInstance type)
│   ← ScriptReferenceUtility.cs (BehaviourScript lookup)
│
├── ConnectionView.cs
│   ← StateView.cs (From/To endpoints)
│   ← ConditionScript.cs (ConditionEntryView.Instance)
│
├── CommentGroupView.cs
│   ← StateView.cs (Members list)
│
├── ConnectionArrowsLayer.cs
│   ← ConnectionView.cs (renders from/to)
│   ← ConnectionController.cs (pending line)
│
├── GridBackground.cs  [leaf, no deps on other project files]
│
├── ConnectionController.cs
│   ← StateView.cs (source/target)
│
├── DragController.cs
│   ← ISelectable.cs (moves selected items)
│
├── SelectionController.cs  [leaf, depends only on ISelectable]
│
├── SelectionBox.cs  [leaf, no deps on other project files]
│
├── GraphPanController.cs  [leaf, no deps on other project files]
│
├── GraphContextMenu.cs
│   ← MenuDropdown.cs (shows menu)
│   ← IContextMenuProvider.cs (extensible providers)
│
├── MenuDropdown.cs
│   ← ScriptReferenceUtility.cs (LoadStyleSheet)
│
├── IContextMenuProvider.cs  [leaf interface]
├── ISelectable.cs  [leaf interface]
├── ScriptReferenceUtility.cs  [leaf utility]
│
├── StateMachineAssetHandler.cs
│   ← StateMachineController.cs (casts)
│   ← CleanStateMachineWindow.cs (OpenWithController)
│
├── StateMachineComponentEditor.cs
│   ← StateMachineComponent.cs (target)
│   ← StateMachineController.cs (reads Data)
│   ← BlackboardVariable.cs (variable rows)
│   ← ScriptReferenceUtility.cs (LoadStyleSheet)
│
├── StateMachineControllerEditor.cs
│   ← StateMachineController.cs (target)
│   ← CleanStateMachineWindow.cs (OpenWithController)
│   ← ScriptReferenceUtility.cs (LoadStyleSheet)
│
└── StateMachineActionEditor.cs
    ← StateMachineAction.cs (target)
    ← StateMachineController.cs (reads blackboard vars)
    ← BlackboardVariable.cs (type filtering)
    ← MenuDropdown.cs (variable picker)
    ← ScriptReferenceUtility.cs (LoadStyleSheet)

Editor/UndoRedo/
├── IUndoableCommand.cs  [leaf interface]
├── UndoRedoSystem.cs  [depends only on IUndoableCommand]
├── CompositeCommand.cs  [depends on IUndoableCommand]
│
├── CreateStateCommand.cs → StateView, List<StateView>
├── CreateConnectionCommand.cs → ConnectionView, List<ConnectionView>
├── CreateGroupCommand.cs → CommentGroupView, List<CommentGroupView>
├── DeleteStatesCommand.cs → StateView, ConnectionView, CommentGroupView, SelectionController
├── DeleteConnectionCommand.cs → ConnectionView, List<ConnectionView>
├── DeleteBlackboardVariableCommand.cs → BlackboardVariable, List<BlackboardVariable>
├── ModifyBlackboardVariableCommand.cs → BlackboardVariable
├── MoveStatesCommand.cs → ISelectable
├── RenameStateCommand.cs → StateView
├── RenameGroupCommand.cs → CommentGroupView
├── ResizeGroupCommand.cs → CommentGroupView
├── ModifyGroupColorCommand.cs → CommentGroupView
└── UngroupCommand.cs → CommentGroupView, List<CommentGroupView>

Editor/ScriptTemplates/
└── StateMachineScriptCreation.cs
    ← ScriptReferenceUtility.cs (FindAssetPath for templates)

Editor/Styles/
├── StateView.uss ← StateView
├── SidePanel.uss ← SidePanel
├── ComponentInspector.uss ← StateMachineComponentEditor
├── ControllerInspector.uss ← StateMachineControllerEditor
├── StateMachineActionInspector.uss ← StateMachineActionEditor
├── MenuDropdown.uss ← MenuDropdown
└── CommentGroupView.uss ← CommentGroupView
```

## Event Flow Map

```
StateMachineComponent.OnRuntimeTransition()
  → TransitionRecord (append to _recentTransitions)
  → OnStateExited?.Invoke() (if path changed)
  → OnStateEntered?.Invoke() (if path changed)
  → OnStateChanged?.Invoke()

CleanStateMachineWindow.OnEditorUpdate() (Play Mode)
  → UpdateTrackedComponent()       // find component on selected GameObject
  → FindActiveStateViewFromPath()   // resolve path string → StateView
  → Set all states' IsActive        // highlight active state
  → Read _recentTransitions         // activate connection animations
  → Auto-navigate _viewScope        // enter/exit sub-state machine view

SelectionController.SelectionChanged
  → CleanStateMachineWindow.OnSelectionChanged()
    → Reorder _states/_connections/_groups (selected items to end)
    → SidePanel.UpdateSelection()

UndoRedoSystem.HistoryChanged → (not subscribed, for extensibility)
```

## Key File Coupling Summary

| File | Most Tightly Coupled To |
|---|---|
| `CleanStateMachineWindow.cs` | EVERY editor file (central hub, ~15+ dependencies) |
| `DetailsPanel.cs` | StateView, ConnectionView, CommentGroupView, BlackboardVariable, MenuDropdown |
| `StateMachineComponent.cs` | RuntimeStateMachine (build/execute), StateMachineController (data source) |
| `RuntimeStateMachine.cs` | StateMachineData.cs (Build), StateBehaviour, ConditionScript |
| `StateMachineData.cs` | Lowest-level — depended by Runtime + Editor |
| `BlackboardVariable.cs` | Depended by Runtime + Editor + all inspector UIs |
| `CommentGroupView.cs` | StateView (members), CleanStateMachineWindow (parent) |
| `CleanStateMachineWindow.cs` ↔ `SidePanel.cs` | Bidirectional via accessor pattern (window exposes internal methods, side panel calls them) |
| `CleanStateMachineWindow.cs` ↔ `DetailsPanel.cs` | Bidirectional (window provides state/connection lists, details panel calls NotifySidePanelChanged) |

## File Size Overview

| File | Lines |
|---|---|
| `CleanStateMachineWindow.cs` | 2353 |
| `DetailsPanel.cs` | 987 |
| `BlackboardPanel.cs` | 568 |
| `StateView.cs` | 441 |
| `CommentGroupView.cs` | 340 |
| `StateMachineComponentEditor.cs` | 396 |
| `StateMachineActionEditor.cs` | 342 |
| `StateMachineComponent.cs` | 326 |
| `RuntimeStateMachine.cs` | 313 |
| `ConnectionArrowsLayer.cs` | 307 |
| `StateMachineController.cs` | 213 |
| `ConnectionView.cs` | 182 |
| `MenuDropdown.cs` | 179 |
| `DeleteStatesCommand.cs` | 141 |
| `GraphPanController.cs` | 103 |
| `GridBackground.cs` | 94 |
| `StateMachineAction.cs` | 93 |
| `BlackboardVariableReference.cs` | 91 |
| `UndoRedoSystem.cs` | 85 |
| `SelectionBox.cs` | 85 |
| `SelectionController.cs` | 78 |
| `DragController.cs` | 57 |
| `ConnectionController.cs` | 54 |
| `ScriptReferenceUtility.cs` | 51 |
| `CompositeCommand.cs` | 48 |
| `StateMachineControllerEditor.cs` | 37 |
| `StateMachineAssetHandler.cs` | 23 |
| `StateMachineScriptCreation.cs` | 24 |
| All other UndoRedo commands | ~35 each |
| `StateMachineData.cs` | 64 (data only) |
| `StateBehaviour.cs` | 11 (abstract) |
| `ConditionScript.cs` | 11 (abstract) |
| `IContextMenuProvider.cs` | 9 (interface) |
| `IUndoableCommand.cs` | 10 (interface) |
| `ISelectable.cs` | 14 (interface) |

## Circular Dependency Risk

None found. The dependency graph is a DAG with `StateMachineData.cs` and `BlackboardVariable.cs` as root data nodes. The editor window (`CleanStateMachineWindow`) is the hub that ties all editor components together but doesn't create cycles.
