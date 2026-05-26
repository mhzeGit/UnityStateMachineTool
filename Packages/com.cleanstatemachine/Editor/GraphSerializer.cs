using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    internal class GraphSerializer
    {
        private readonly CleanStateMachineWindow _window;

        public GraphSerializer(CleanStateMachineWindow window)
        {
            _window = window;
        }

        public void SaveCurrentData()
        {
            if (_window.CurrentData == null) return;
            _window.CurrentData.States.Clear();
            _window.CurrentData.Connections.Clear();
            _window.CurrentData.Groups.Clear();
            _window.CurrentData.BlackboardVariables.Clear();

            var stateToIndex = new Dictionary<StateView, int>();
            var indexToView = new Dictionary<int, StateView>();
            for (int i = 0; i < _window.States.Count; i++)
            {
                stateToIndex[_window.States[i]] = i;
                indexToView[_window.States[i].DataIndex] = _window.States[i];
            }

            foreach (var state in _window.States)
            {
                for (int j = 0; j < state.BehaviourEntries.Count; j++)
                {
                    var entry = state.BehaviourEntries[j];
                    if (entry.Instance != null)
                        entry.Instance.name = $"{state.Name}_Behaviour_{j}";
                }

                var childIndices = new List<int>();
                if (state.IsSubStateMachine)
                {
                    foreach (var childDataIdx in state.ChildIndices)
                    {
                        if (indexToView.TryGetValue(childDataIdx, out var childView))
                            childIndices.Add(stateToIndex[childView]);
                    }
                }

                var sd = new StateData
                {
                    Name = state.Name,
                    Position = state.Position,
                    Size = state.Size,
                    IsEntry = state.IsEntry,
                    IsSubEntry = state.IsSubEntry,
                    IsSubStateMachine = state.IsSubStateMachine,
                    IsExternalReference = state.IsExternalReference,
                    IsAnyState = state.IsAnyState,
                    AutoRun = state.AutoRun,
                    ChildIndices = childIndices,
                    ExternalAction = state.ExternalAction,
                    ExternalStateMachine = state.ExternalStateMachine,
                    ExternalTargetStateName = state.ExternalTargetStateName,
                    ExternalBlackboardParmName = state.ExternalBlackboardParmName,
                    ExternalBlackboardParmType = state.ExternalBlackboardParmType,
                    ExternalBlackboardParmValue = state.ExternalBlackboardParmValue,
                    Behaviours = state.BehaviourEntries
                };

                _window.CurrentData.States.Add(sd);
            }

            foreach (var conn in _window.Connections)
            {
                for (int j = 0; j < conn.ConditionEntries.Count; j++)
                {
                    var entry = conn.ConditionEntries[j];
                    if (entry.Instance != null)
                    {
                        string fromName = conn.From?.Name ?? "?";
                        string toName = conn.To?.Name ?? "?";
                        entry.Instance.name = $"{fromName}->{toName}_Condition_{j}";
                    }
                }

                var cd = new ConnectionData
                {
                    FromIndex = stateToIndex[conn.From],
                    ToIndex = stateToIndex[conn.To],
                    MinStateTime = conn.MinStateTime,
                    Conditions = conn.ConditionEntries
                };
                _window.CurrentData.Connections.Add(cd);
            }

            foreach (var group in _window.Groups)
            {
                var gd = new GroupData { Label = group.Label, Color = group.GroupColor };
                foreach (var member in group.Members)
                    gd.MemberIndices.Add(stateToIndex[member]);
                _window.CurrentData.Groups.Add(gd);
            }

            foreach (var v in _window.BlackboardVariables)
                _window.CurrentData.BlackboardVariables.Add(v.Clone());

            _window.CurrentData.PanOffset = _window.PanOffset;
            _window.CurrentData.Zoom = _window.Zoom;

            _window.CurrentData.ExpandedSubStateIndices.Clear();
            foreach (int dataIdx in _window.ExpandedSubStateStack)
            {
                if (indexToView.TryGetValue(dataIdx, out var expandedView))
                    _window.CurrentData.ExpandedSubStateIndices.Add(stateToIndex[expandedView]);
            }

            _window.CurrentData.Breakpoints.Clear();
            foreach (int bpDataIdx in _window.BreakpointStateIndices)
            {
                if (indexToView.TryGetValue(bpDataIdx, out var bpView))
                {
                    _window.CurrentData.Breakpoints.Add(new BreakpointData
                    {
                        StateIndex = stateToIndex[bpView]
                    });
                }
            }
        }

        public void LoadFromController()
        {
            _window.CurrentData = _window.Controller != null ? _window.Controller.Data : new SerializableData();
            LoadFromCurrentData();
        }

        public void LoadFromCurrentData()
        {
            _window.IsLoading = true;

            _window.EditingState = null;
            _window.SelectionController.Clear();
            _window.States.Clear();
            _window.Connections.Clear();
            _window.Groups.Clear();
            _window.BlackboardVariables.Clear();
            _window.UndoRedoSystemClear();
            _window.ActiveStateIndex = -1;
            _window.TrackedComponent = null;
            _window.PendingExpandStack = null;
            _window.LastTransitionFromIndex = -1;
            _window.LastTransitionToIndex = -1;
            _window.LastTransitionConnectionIndex = -1;
            if (_window.CurrentData != null)
            {
                var data = _window.CurrentData;

                var stateLookup = new List<StateView>();
                for (int i = 0; i < data.States.Count; i++)
                {
                    var sd = data.States[i];
                    var state = new StateView(sd.Position, sd.Name, sd.IsEntry, sd.IsSubEntry, sd.IsAnyState)
                    {
                        Size = sd.Size,
                        AutoRun = sd.AutoRun,
                        ChildIndices = new List<int>(sd.ChildIndices),
                        IsSubStateMachine = sd.IsSubStateMachine,
                        IsExternalReference = sd.IsExternalReference,
                        ExternalAction = sd.ExternalAction,
                        ExternalStateMachine = sd.ExternalStateMachine,
                        ExternalTargetStateName = sd.ExternalTargetStateName,
                        ExternalBlackboardParmName = sd.ExternalBlackboardParmName,
                        ExternalBlackboardParmType = sd.ExternalBlackboardParmType,
                        ExternalBlackboardParmValue = sd.ExternalBlackboardParmValue,
                        DataIndex = i
                    };

                    for (int j = 0; j < sd.Behaviours.Count; j++)
                    {
                        var be = sd.Behaviours[j];
                        state.BehaviourEntries.Add(be);
                    }
                    _window.States.Add(state);
                    stateLookup.Add(state);
                }

                for (int i = 0; i < data.Connections.Count; i++)
                {
                    var cd = data.Connections[i];
                    if (cd.FromIndex >= 0 && cd.FromIndex < stateLookup.Count &&
                        cd.ToIndex >= 0 && cd.ToIndex < stateLookup.Count)
                    {
                        var conn = new ConnectionView(
                            stateLookup[cd.FromIndex], stateLookup[cd.ToIndex])
                        {
                            DataIndex = i,
                            MinStateTime = cd.MinStateTime
                        };
                        if (cd.Conditions != null)
                        {
                            conn.ConditionEntries = cd.Conditions;
                        }
                        _window.Connections.Add(conn);
                    }
                }

                for (int i = 0; i < data.Groups.Count; i++)
                {
                    var gd = data.Groups[i];
                    var members = new List<StateView>();
                    foreach (int mi in gd.MemberIndices)
                    {
                        if (mi >= 0 && mi < stateLookup.Count && !stateLookup[mi].IsEntry && !stateLookup[mi].IsAnyState)
                            members.Add(stateLookup[mi]);
                    }
                    var group = new CommentGroupView(members, gd.Label);
                    group.GroupColor = gd.Color;
                    _window.Groups.Add(group);
                }

                for (int i = 0; i < data.BlackboardVariables.Count; i++)
                    _window.BlackboardVariables.Add(data.BlackboardVariables[i].Clone());

                _window.PanOffset = data.PanOffset;
                _window.Zoom = data.Zoom;

                _window.ExpandedSubStateStack.Clear();
                if (data.ExpandedSubStateIndices != null && data.ExpandedSubStateIndices.Count > 0)
                {
                    for (int i = 0; i < data.ExpandedSubStateIndices.Count; i++)
                    {
                        int idx = data.ExpandedSubStateIndices[i];
                        if (idx >= 0 && idx < _window.States.Count && _window.States[idx].IsSubStateMachine)
                            _window.ExpandedSubStateStack.Add(idx);
                    }
                }

                _window.BreakpointStateIndices.Clear();
                if (data.Breakpoints != null && data.Breakpoints.Count > 0)
                {
                    for (int i = 0; i < data.Breakpoints.Count; i++)
                    {
                        int si = data.Breakpoints[i].StateIndex;
                        if (si >= 0 && si < _window.States.Count)
                            _window.BreakpointStateIndices.Add(_window.States[si].DataIndex);
                    }
                }
            }

            if (_window.ExpandedModeBar != null)
                _window.ExpandedView.UpdateExpandedModeBar();

            _window.SyncStateBreakpointVisuals();
            _window.EnsureEntryStateExistsInternal();
            _window.GraphOperations.SyncGroupElements();
            _window.GraphOperations.SyncStatesWithSubMachines();
            _window.ResetDataIndexCounter();
            _window.IsLoading = false;
            if (_window.GraphValidation != null)
            {
                _window.GraphValidation.MarkDirty();
                _window.GraphValidation.RunAndUpdate();
            }
            _window.MarkSavedInternal();
            _window.UpdateTitleInternal();

            if (_window.SidePanelElement != null)
            {
                _window.SidePanelElement.SyncFromWindow();
                _window.SidePanelElement.UpdateVisibility();
                _window.SidePanelElement.UpdateSelection();
                _window.SidePanelElement.UpdateBlackboard();
            }

            _window.Repaint();
        }

        public void SaveToController()
        {
            if (_window.Controller == null) return;
            SaveCurrentData();
            _window.Controller.Data = _window.Controller.Data;
        }

        public void SaveAs()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save State Machine Controller",
                "NewStateMachineController",
                "asset",
                "Save state machine controller as...");

            if (string.IsNullOrEmpty(path)) return;

            var controller = ScriptableObject.CreateInstance<StateMachineController>();
            AssetDatabase.CreateAsset(controller, path);

            _window.Controller = controller;
            SaveToController();
            _window.Controller.Save();
            _window.MarkSavedInternal();
            _window.Repaint();

            EditorGUIUtility.PingObject(controller);
        }

        public void NewFile()
        {
            if (_window.HasUnsavedChanges)
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "Unsaved Changes",
                    "You have unsaved changes. What do you want to do?",
                    "Save", "Cancel", "Discard");

                if (option == 1) return;

                if (option == 0)
                {
                    if (_window.Controller != null)
                    {
                        SaveToController();
                        _window.Controller.Save();
                    }
                    else
                    {
                        SaveAs();
                        return;
                    }
                }
            }

            _window.Controller = null;
            _window.CurrentData = new SerializableData();
            _window.EditingState = null;
            _window.SelectionController.Clear();
            _window.States.Clear();
            _window.Connections.Clear();
            _window.Groups.Clear();
            _window.BlackboardVariables.Clear();
            _window.UndoRedoSystemClear();
            _window.ActiveStateIndex = -1;
            _window.PanOffset = Vector2.zero;
            _window.Zoom = 1f;
            _window.ExpandedSubStateStack.Clear();
            _window.BreakpointStateIndices.Clear();
            _window.PendingExpandStack = null;
            _window.LastTransitionFromIndex = -1;
            _window.LastTransitionToIndex = -1;
            _window.LastTransitionConnectionIndex = -1;
            if (_window.ExpandedModeBar != null)
                _window.ExpandedModeBar.style.display = DisplayStyle.None;

            _window.EnsureEntryStateExistsInternal();
            _window.GraphOperations.SyncGroupElements();
            _window.ResetDataIndexCounter();

            if (_window.GraphValidation != null)
            {
                _window.GraphValidation.MarkDirty();
                _window.GraphValidation.RunAndUpdate();
            }
            _window.MarkSavedInternal();

            if (_window.SidePanelElement != null)
            {
                _window.SidePanelElement.UpdateVisibility();
                _window.SidePanelElement.UpdateSelection();
                _window.SidePanelElement.UpdateBlackboard();
            }

            _window.Repaint();
        }

        public void LoadController(StateMachineController controller)
        {
            if (controller == null) return;
            if (controller == _window.Controller) return;

            if (_window.HasUnsavedChanges && _window.Controller != null)
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "Unsaved Changes",
                    $"Save changes to {_window.Controller.name} before switching?",
                    "Save", "Cancel", "Discard");

                if (option == 1) return;

                if (option == 0)
                {
                    SaveToController();
                    _window.Controller.Save();
                }
            }

            _window.Controller = controller;
            LoadFromController();
            _window.StartSmoothFocusOnContentInternal();
        }
    }
}
