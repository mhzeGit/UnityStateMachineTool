using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    internal class GraphOperations
    {
        private readonly CleanStateMachineWindow _window;

        public GraphOperations(CleanStateMachineWindow window)
        {
            _window = window;
        }

        public void CreateState(Vector2 graphMousePosition)
        {
            var state = new StateView(graphMousePosition) { DataIndex = _window.GetNextDataIndex() };

            if (_window.EntryState != null && GetEntryOutgoingConnection() == null)
            {
                var cmd = new CompositeCommand("Create State");
                cmd.Add(new CreateStateCommand(_window.States, state));
                cmd.Add(new CreateConnectionCommand(_window.Connections, new ConnectionView(_window.EntryState, state)));
                _window.UndoRedoSystem.Execute(cmd);
            }
            else
            {
                var cmd = new CreateStateCommand(_window.States, state);
                _window.UndoRedoSystem.Execute(cmd);
            }

            _window.MarkChangedInternal();
            SyncStatesWithGroups();
            SyncStatesWithSubMachines();
            AddToExpandedContainer(state);
            _window.Repaint();
        }

        public void CreateSubStateMachine(Vector2 graphMousePosition)
        {
            var container = new StateView(graphMousePosition, "Sub State Machine")
            {
                DataIndex = _window.GetNextDataIndex(),
                IsSubStateMachine = true
            };

            if (_window.EntryState != null && GetEntryOutgoingConnection() == null)
            {
                var cmd = new CompositeCommand("Create Sub State Machine");
                cmd.Add(new CreateStateCommand(_window.States, container));
                cmd.Add(new CreateConnectionCommand(_window.Connections, new ConnectionView(_window.EntryState, container)));
                _window.UndoRedoSystem.Execute(cmd);
            }
            else
            {
                var cmd = new CreateStateCommand(_window.States, container);
                _window.UndoRedoSystem.Execute(cmd);
            }

            _window.MarkChangedInternal();
            SyncStatesWithGroups();
            SyncStatesWithSubMachines();
            AddToExpandedContainer(container);
            _window.Repaint();
        }

        public void CreateExternalReferenceState(Vector2 graphMousePosition)
        {
            var state = new StateView(graphMousePosition, "External Reference")
            {
                DataIndex = _window.GetNextDataIndex(),
                IsExternalReference = true
            };

            if (_window.EntryState != null && GetEntryOutgoingConnection() == null)
            {
                var cmd = new CompositeCommand("Create External Reference");
                cmd.Add(new CreateStateCommand(_window.States, state));
                cmd.Add(new CreateConnectionCommand(_window.Connections, new ConnectionView(_window.EntryState, state)));
                _window.UndoRedoSystem.Execute(cmd);
            }
            else
            {
                var cmd = new CreateStateCommand(_window.States, state);
                _window.UndoRedoSystem.Execute(cmd);
            }

            _window.MarkChangedInternal();
            SyncStatesWithGroups();
            AddToExpandedContainer(state);
            _window.Repaint();
        }

        public void CreateAnyState(Vector2 graphMousePosition)
        {
            var state = new StateView(graphMousePosition, "Any State", isAnyState: true)
            {
                DataIndex = _window.GetNextDataIndex()
            };

            var cmd = new CreateStateCommand(_window.States, state);
            _window.UndoRedoSystem.Execute(cmd);

            _window.MarkChangedInternal();
            SyncStatesWithGroups();
            AddToExpandedContainer(state);
            _window.Repaint();
        }

        public void ConnectRequested(StateView source)
        {
            _window.ConnectionController.StartConnection(source);
            _window.Repaint();
        }

        public ConnectionView GetEntryOutgoingConnection()
        {
            if (_window.EntryState == null)
                return null;

            for (int i = 0; i < _window.Connections.Count; i++)
            {
                if (_window.Connections[i].From == _window.EntryState)
                    return _window.Connections[i];
            }

            return null;
        }

        public void UngroupRequested(CommentGroupView group)
        {
            _window.SelectionController.Deselect(group);
            var cmd = new UngroupCommand(_window.Groups, group);
            _window.UndoRedoSystem.Execute(cmd);
            _window.MarkChangedInternal();
            SyncGroupElements();
            _window.Repaint();
        }

        public void CopySelectedStates()
        {
            _window.Clipboard = new List<CopiedStateData>();
            _window.CopiedConnections = new List<CopiedConnectionData>();

            var toCopy = new HashSet<int>();
            for (int i = 0; i < _window.SelectionController.Count; i++)
            {
                if (_window.SelectionController.Selected[i] is StateView s && !s.IsEntry)
                    CollectCopySet(s, toCopy);
            }

            var indexToState = new Dictionary<int, StateView>();
            for (int i = 0; i < _window.States.Count; i++)
                indexToState[_window.States[i].DataIndex] = _window.States[i];

            foreach (int dataIdx in toCopy)
            {
                if (!indexToState.TryGetValue(dataIdx, out var s)) continue;

                var copiedEntries = new List<BehaviourEntry>();
                for (int j = 0; j < s.BehaviourEntries.Count; j++)
                {
                    var entry = s.BehaviourEntries[j];
                    copiedEntries.Add(new BehaviourEntry
                    {
                        TypeName = entry.TypeName,
                        Instance = entry.Instance
                    });
                }

                _window.Clipboard.Add(new CopiedStateData
                {
                    sourceDataIndex = s.DataIndex,
                    position = s.Position,
                    name = s.Name,
                    size = s.Size,
                    behaviourEntries = copiedEntries,
                    childIndices = new List<int>(s.ChildIndices),
                    isSubStateMachine = s.IsSubStateMachine,
                    isExternalReference = s.IsExternalReference,
                    isAnyState = s.IsAnyState,
                    externalAction = s.ExternalAction,
                    externalStateMachine = s.ExternalStateMachine,
                    externalTargetStateName = s.ExternalTargetStateName,
                    externalBlackboardParmName = s.ExternalBlackboardParmName,
                    externalBlackboardParmType = s.ExternalBlackboardParmType,
                    externalBlackboardParmValue = s.ExternalBlackboardParmValue
                });
            }

            for (int i = 0; i < _window.Connections.Count; i++)
            {
                var conn = _window.Connections[i];
                if (conn.From == null || conn.To == null) continue;
                if (toCopy.Contains(conn.From.DataIndex) && toCopy.Contains(conn.To.DataIndex))
                {
                    var copiedConditions = new List<ConditionEntry>();
                    for (int j = 0; j < conn.ConditionEntries.Count; j++)
                    {
                        var ce = conn.ConditionEntries[j];
                        copiedConditions.Add(new ConditionEntry
                        {
                            TypeName = ce.TypeName,
                            Instance = ce.Instance
                        });
                    }

                    _window.CopiedConnections.Add(new CopiedConnectionData
                    {
                        fromSourceIndex = conn.From.DataIndex,
                        toSourceIndex = conn.To.DataIndex,
                        conditionEntries = copiedConditions
                    });
                }
            }
        }

        private void CollectCopySet(StateView state, HashSet<int> set)
        {
            if (state.IsEntry) return;
            if (!set.Add(state.DataIndex)) return;

            if (state.IsSubStateMachine)
            {
                for (int i = 0; i < state.ChildIndices.Count; i++)
                {
                    var child = GetStateByIndex(state.ChildIndices[i]);
                    if (child != null)
                        CollectCopySet(child, set);
                }
            }
        }

        public void PasteStates()
        {
            if (_window.Clipboard == null || _window.Clipboard.Count == 0) return;

            Vector2 mouseGraphPos = _window.LastMouseGraphPos;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < _window.Clipboard.Count; i++)
            {
                var d = _window.Clipboard[i];
                if (d.position.x < minX) minX = d.position.x;
                if (d.position.x + d.size.x > maxX) maxX = d.position.x + d.size.x;
                if (d.position.y < minY) minY = d.position.y;
                if (d.position.y + d.size.y > maxY) maxY = d.position.y + d.size.y;
            }

            Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            Vector2 offset = mouseGraphPos - center;

            _window.SelectionController.Clear();

            var composite = new CompositeCommand("Paste States");
            var pastedStates = new List<StateView>();
            var oldToNewIndex = new Dictionary<int, int>();

            for (int i = 0; i < _window.Clipboard.Count; i++)
            {
                var data = _window.Clipboard[i];
                int newIndex = _window.GetNextDataIndex();
                oldToNewIndex[data.sourceDataIndex] = newIndex;

                var state = new StateView(data.position + offset, data.name, isAnyState: data.isAnyState)
                {
                    Size = data.size,
                    DataIndex = newIndex,
                    IsSubStateMachine = data.isSubStateMachine,
                    IsSubEntry = false,
                    IsExternalReference = data.isExternalReference,
                    ExternalAction = data.externalAction,
                    ExternalStateMachine = data.externalStateMachine,
                    ExternalTargetStateName = data.externalTargetStateName,
                    ExternalBlackboardParmName = data.externalBlackboardParmName,
                    ExternalBlackboardParmType = data.externalBlackboardParmType,
                    ExternalBlackboardParmValue = data.externalBlackboardParmValue
                };
                state.ChildIndices.Clear();

                if (data.behaviourEntries != null)
                {
                    for (int j = 0; j < data.behaviourEntries.Count; j++)
                    {
                        var src = data.behaviourEntries[j];
                        var script = src.GetScript();
                        if (script == null) continue;
                        var type = script.GetClass();
                        if (type == null || !type.IsSubclassOf(typeof(StateBehaviour)))
                            continue;

                        var clone = new BehaviourEntry { TypeName = src.TypeName };
                        if (src.Instance != null)
                        {
                            var instance = (StateBehaviour)ScriptableObject.CreateInstance(type);
                            EditorUtility.CopySerialized(src.Instance, instance);
                            instance.name = $"{state.Name}_Behaviour_{j}";
                            instance.hideFlags = HideFlags.HideInHierarchy;
                            clone.Instance = instance;
                        }
                        state.BehaviourEntries.Add(clone);
                    }
                }

                composite.Add(new CreateStateCommand(_window.States, state));
                pastedStates.Add(state);
            }

            for (int i = 0; i < (_window.CopiedConnections?.Count ?? 0); i++)
            {
                var cd = _window.CopiedConnections[i];
                if (!oldToNewIndex.TryGetValue(cd.fromSourceIndex, out int newFrom)) continue;
                if (!oldToNewIndex.TryGetValue(cd.toSourceIndex, out int newTo)) continue;

                var fromState = pastedStates.Find(s => s.DataIndex == newFrom);
                var toState = pastedStates.Find(s => s.DataIndex == newTo);
                if (fromState == null || toState == null) continue;

                var connView = new ConnectionView(fromState, toState);
                if (cd.conditionEntries != null)
                {
                    for (int j = 0; j < cd.conditionEntries.Count; j++)
                    {
                        var src = cd.conditionEntries[j];
                        var clonedCe = new ConditionEntry { TypeName = src.TypeName };
                        if (src.Instance != null)
                        {
                            var script = src.GetScript();
                            var type = script?.GetClass();
                            if (type != null && type.IsSubclassOf(typeof(ConditionScript)))
                            {
                                var instance = (ConditionScript)ScriptableObject.CreateInstance(type);
                                EditorUtility.CopySerialized(src.Instance, instance);
                                instance.hideFlags = HideFlags.HideInHierarchy;
                                clonedCe.Instance = instance;
                            }
                        }
                        connView.ConditionEntries.Add(clonedCe);
                    }
                }

                composite.Add(new CreateConnectionCommand(_window.Connections, connView));
            }

            _window.UndoRedoSystem.Execute(composite);

            _window.MarkChangedInternal();
            SyncStatesWithGroups();
            SyncStatesWithSubMachines();

            for (int i = 0; i < _window.Clipboard.Count; i++)
            {
                var data = _window.Clipboard[i];
                if (data.childIndices != null && data.childIndices.Count > 0 && data.isSubStateMachine)
                {
                    if (!oldToNewIndex.TryGetValue(data.sourceDataIndex, out int newContainerIndex))
                        continue;

                    var container = pastedStates.Find(s => s.DataIndex == newContainerIndex);
                    if (container != null)
                    {
                        container.ChildIndices.Clear();
                        foreach (var oldChildIdx in data.childIndices)
                        {
                            if (oldToNewIndex.TryGetValue(oldChildIdx, out int newChildIdx))
                            {
                                container.ChildIndices.Add(newChildIdx);
                                var child = _window.States.Find(s => s.DataIndex == newChildIdx);
                                if (child != null)
                                    child.IsSubEntry = true;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < pastedStates.Count; i++)
                AddToExpandedContainer(pastedStates[i]);

            for (int i = 0; i < pastedStates.Count; i++)
                _window.SelectionController.Select(pastedStates[i]);

            _window.Repaint();
        }

        public void DuplicateSelectedStates()
        {
            CopySelectedStates();
            if (_window.Clipboard == null || _window.Clipboard.Count == 0) return;

            for (int i = 0; i < _window.Clipboard.Count; i++)
            {
                var d = _window.Clipboard[i];
                d.position += new Vector2(20f, 20f);
            }

            PasteStates();
        }

        public void DeleteSelected()
        {
            if (_window.SelectionController.Count == 0) return;

            for (int i = _window.SelectionController.Count - 1; i >= 0; i--)
            {
                if (_window.SelectionController.Selected[i] is StateView s && s.IsEntry)
                    _window.SelectionController.Deselect(s);
            }

            if (_window.SelectionController.Count == 0) return;

            var deletedStates = new List<StateView>();
            for (int i = 0; i < _window.SelectionController.Count; i++)
            {
                if (_window.SelectionController.Selected[i] is StateView s)
                    deletedStates.Add(s);
            }

            if (_window.ExpandedSubStateStack.Count > 0)
            {
                bool expandedBeingDeleted = false;
                for (int i = 0; i < deletedStates.Count; i++)
                {
                    if (_window.ExpandedSubStateStack.Contains(deletedStates[i].DataIndex))
                    {
                        expandedBeingDeleted = true;
                        break;
                    }
                }
                if (expandedBeingDeleted)
                {
                    for (int i = _window.ExpandedSubStateStack.Count - 1; i >= 0; i--)
                    {
                        for (int j = 0; j < deletedStates.Count; j++)
                        {
                            if (_window.ExpandedSubStateStack[i] == deletedStates[j].DataIndex)
                            {
                                _window.ExpandedSubStateStack.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    UpdateExpandedModeBar();
                }
            }

            for (int i = 0; i < deletedStates.Count; i++)
                _window.BreakpointStateIndices.Remove(deletedStates[i].DataIndex);

            var cmd = new DeleteStatesCommand(_window.States, _window.Connections, _window.Groups, _window.SelectionController);
            _window.UndoRedoSystem.Execute(cmd);
            _window.MarkChangedInternal();
            _window.SyncStateBreakpointVisuals();
            SyncGroupElements();

            _window.SelectionController.Clear();
            _window.Repaint();
        }

        public void StartEditing(StateView state)
        {
            if (_window.EditingState != null && _window.EditingState != state)
            {
                _window.EditingState.CommitEditing();
            }

            _window.EditingState = state;
            state.EditingCommitted += OnStateEditingCommitted;
            state.StartEditing();
        }

        private void OnStateEditingCommitted(StateView state, string oldName, string newName)
        {
            state.EditingCommitted -= OnStateEditingCommitted;

            if (_window.EditingState != state)
                return;

            _window.EditingState = null;

            if (oldName != newName && !string.IsNullOrEmpty(newName))
            {
                var cmd = new RenameStateCommand(state, oldName, newName);
                _window.UndoRedoSystem.Execute(cmd);
                _window.MarkChangedInternal();
            }
        }

        public void StartEditingGroup(CommentGroupView group)
        {
            _window.EditingGroup = group;
            group.EditingCommitted += OnGroupEditingCommitted;
            group.StartEditing();
        }

        private void OnGroupEditingCommitted(CommentGroupView group, string oldName, string newName)
        {
            group.EditingCommitted -= OnGroupEditingCommitted;
            _window.EditingGroup = null;

            if (oldName != newName && !string.IsNullOrEmpty(newName))
            {
                var cmd = new RenameGroupCommand(group, oldName, newName);
                _window.UndoRedoSystem.Execute(cmd);
                _window.MarkChangedInternal();
            }
        }

        public void EnsureEntryStateExists()
        {
            _window.EntryState = null;
            for (int i = 0; i < _window.States.Count; i++)
            {
                if (_window.States[i].IsEntry)
                {
                    _window.EntryState = _window.States[i];
                    break;
                }
            }

            if (_window.EntryState == null)
            {
                _window.EntryState = new StateView(CleanStateMachineWindow.EntryStatePosition, "Entry", isEntry: true) { DataIndex = 0 };
                _window.States.Insert(0, _window.EntryState);
                _window.ResetDataIndexCounter();
            }
        }

        public void CreateGroupFromSelectedStates()
        {
            var selectedStates = new List<StateView>();
            for (int i = 0; i < _window.SelectionController.Count; i++)
            {
                if (_window.SelectionController.Selected[i] is StateView s && !s.IsEntry && !s.IsAnyState)
                    selectedStates.Add(s);
            }

            if (selectedStates.Count < 1) return;

            var group = new CommentGroupView(selectedStates, $"Group {_window.Groups.Count + 1}");
            var cmd = new CreateGroupCommand(_window.Groups, group);
            _window.UndoRedoSystem.Execute(cmd);
            _window.MarkChangedInternal();
            SyncGroupElements();

            _window.SelectionController.Clear();
            _window.SelectionController.Select(group);
            SyncStatesWithGroups();
        }

        public void SyncStatesWithGroups()
        {
            for (int i = 0; i < _window.Groups.Count; i++)
                _window.Groups[i].SyncContainedStates(_window.States);
        }

        public void SyncGroupElements()
        {
            if (_window.GroupContainer == null) return;
            if (_window.EditingGroup != null && !_window.Groups.Contains(_window.EditingGroup))
                _window.EditingGroup = null;
            _window.GroupContainer.Clear();
            for (int i = 0; i < _window.Groups.Count; i++)
            {
                _window.GroupContainer.Add(_window.Groups[i]);
                _window.Groups[i].UpdateScreenPosition(_window.Zoom, _window.PanOffset);
            }
        }

        public void SyncStatesWithSubMachines()
        {
            const float cellSize = 250f;

            var grid = new Dictionary<int, Dictionary<int, List<StateView>>>();

            for (int j = 0; j < _window.States.Count; j++)
            {
                var state = _window.States[j];
                if (state.IsEntry || state.IsAnyState) continue;

                var bounds = state.GetGraphBounds();
                int minCX = (int)Math.Floor(bounds.xMin / cellSize);
                int maxCX = (int)Math.Floor(bounds.xMax / cellSize);
                int minCY = (int)Math.Floor(bounds.yMin / cellSize);
                int maxCY = (int)Math.Floor(bounds.yMax / cellSize);

                for (int cx = minCX; cx <= maxCX; cx++)
                {
                    if (!grid.TryGetValue(cx, out var col))
                    {
                        col = new Dictionary<int, List<StateView>>();
                        grid[cx] = col;
                    }
                    for (int cy = minCY; cy <= maxCY; cy++)
                    {
                        if (!col.TryGetValue(cy, out var list))
                        {
                            list = new List<StateView>();
                            col[cy] = list;
                        }
                        list.Add(state);
                    }
                }
            }

            for (int i = 0; i < _window.States.Count; i++)
            {
                var container = _window.States[i];
                if (!container.IsSubStateMachine) continue;

                float cLeft = container.Position.x;
                float cTop = container.Position.y;
                float cRight = cLeft + container.Size.x;
                float cBottom = cTop + container.Size.y;

                int minCX = (int)Math.Floor(cLeft / cellSize);
                int maxCX = (int)Math.Floor(cRight / cellSize);
                int minCY = (int)Math.Floor(cTop / cellSize);
                int maxCY = (int)Math.Floor(cBottom / cellSize);

                var checkedStates = new HashSet<StateView>();

                for (int cx = minCX; cx <= maxCX; cx++)
                {
                    if (!grid.TryGetValue(cx, out var col)) continue;
                    for (int cy = minCY; cy <= maxCY; cy++)
                    {
                        if (!col.TryGetValue(cy, out var cellStates)) continue;
                        foreach (var child in cellStates)
                        {
                            if (!checkedStates.Add(child)) continue;
                            if (child == container) continue;

                            bool alreadyChild = container.ChildIndices.Contains(child.DataIndex);

                            Rect childRect = child.GetGraphBounds();
                            bool inside = childRect.xMin >= cLeft - 0.001f &&
                                          childRect.yMin >= cTop - 0.001f &&
                                          childRect.xMax <= cRight + 0.001f &&
                                          childRect.yMax <= cBottom + 0.001f;

                            if (inside && !alreadyChild)
                            {
                                container.ChildIndices.Add(child.DataIndex);
                                bool hasSubEntry = false;
                                for (int k = 0; k < container.ChildIndices.Count; k++)
                                {
                                    int ci = container.ChildIndices[k];
                                    var childState = GetStateByIndex(ci);
                                    if (childState != null && childState.IsSubEntry)
                                    {
                                        hasSubEntry = true;
                                        break;
                                    }
                                }
                                if (!hasSubEntry)
                                    child.IsSubEntry = true;
                            }
                        }
                    }
                }
            }
        }

        public StateView GetStateByIndex(int index)
        {
            for (int i = 0; i < _window.States.Count; i++)
            {
                if (_window.States[i].DataIndex == index)
                    return _window.States[i];
            }
            return null;
        }

        public void AddToExpandedContainer(StateView state)
        {
            if (_window.ExpandedSubStateStack.Count == 0) return;
            int topExpanded = _window.ExpandedSubStateStack[_window.ExpandedSubStateStack.Count - 1];
            var container = GetStateByIndex(topExpanded);
            if (container != null && !container.ChildIndices.Contains(state.DataIndex))
                container.ChildIndices.Add(state.DataIndex);
        }

        public void UpdateExpandedModeBar()
        {
            _window.ExpandedView.UpdateExpandedModeBar();
        }
    }
}
