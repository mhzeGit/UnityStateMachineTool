using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    internal class GraphInputHandler
    {
        private readonly CleanStateMachineWindow _window;
        private readonly GraphOperations _operations;
        private Dictionary<ISelectable, Vector2> _preDragPositions;

        public GraphInputHandler(CleanStateMachineWindow window, GraphOperations operations)
        {
            _window = window;
            _operations = operations;
        }

        public bool HandleKeyboardShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown) return false;

            if (e.keyCode == KeyCode.Z && e.control)
            {
                if (_window.UndoRedoSystem.Undo())
                {
                    _window.MarkChangedInternal();
                    _operations.SyncGroupElements();
                    _operations.SyncStatesWithSubMachines();
                    _window.SidePanelElement?.UpdateBlackboard();
                    _window.SidePanelElement?.UpdateSelection();
                    _window.Repaint();
                }
                e.Use();
                return true;
            }

            if (e.keyCode == KeyCode.Y && e.control)
            {
                if (_window.UndoRedoSystem.Redo())
                {
                    _window.MarkChangedInternal();
                    _operations.SyncGroupElements();
                    _operations.SyncStatesWithSubMachines();
                    _window.SidePanelElement?.UpdateBlackboard();
                    _window.SidePanelElement?.UpdateSelection();
                    _window.Repaint();
                }
                e.Use();
                return true;
            }

            if (e.keyCode == KeyCode.G && e.control)
            {
                _operations.CreateGroupFromSelectedStates();
                e.Use();
                _window.Repaint();
                return true;
            }

            if (e.keyCode == KeyCode.C && e.control)
            {
                _operations.CopySelectedStates();
                e.Use();
                _window.Repaint();
                return true;
            }

            if (e.keyCode == KeyCode.C && !e.control)
            {
                StateView source = null;
                for (int i = 0; i < _window.SelectionController.Count; i++)
                {
                    if (_window.SelectionController.Selected[i] is StateView s)
                    {
                        if (source != null) { source = null; break; }
                        source = s;
                    }
                }

                if (source != null)
                {
                    _window.ConnectionController.StartConnection(source);
                    e.Use();
                    _window.Repaint();
                    return true;
                }
            }

            if (e.keyCode == KeyCode.B && !e.control)
            {
                for (int i = 0; i < _window.SelectionController.Count; i++)
                {
                    if (_window.SelectionController.Selected[i] is StateView s)
                    {
                        bool hasBp = _window.BreakpointStateIndices.Contains(s.DataIndex);
                        var cmd = new ToggleBreakpointCommand(_window, s.DataIndex, !hasBp);
                        _window.UndoRedoSystem.Execute(cmd);
                    }
                }
                _window.MarkChangedInternal();
                e.Use();
                _window.Repaint();
                return true;
            }

            if (e.keyCode == KeyCode.F && !e.control && !e.shift && !e.alt)
            {
                double now = UnityEditor.EditorApplication.timeSinceStartup;
                bool doubleTap = (now - _window.LastFPressTime) < (CleanStateMachineWindow.DoubleClickTimeMs / 1000.0);
                _window.LastFPressTime = now;

                if (doubleTap)
                    _window.FocusOnAll();
                else
                    _window.FocusOnSelection();

                e.Use();
                _window.Repaint();
                return true;
            }

            if (e.keyCode == KeyCode.Home)
            {
                _window.FocusOnAll();
                e.Use();
                _window.Repaint();
                return true;
            }

            if (e.keyCode == KeyCode.V && e.control)
            {
                _operations.PasteStates();
                e.Use();
                _window.Repaint();
                return true;
            }

            if (e.keyCode == KeyCode.D && e.control)
            {
                _operations.DuplicateSelectedStates();
                e.Use();
                _window.Repaint();
                return true;
            }

            if (e.keyCode == KeyCode.F2)
            {
                Vector2 graphPos = (_window.LastMouseGraphPos - _window.PanOffset) / _window.Zoom;
                ISelectable hovered = HitTest(graphPos);

                if (hovered is StateView hoveredState)
                {
                    _operations.StartEditing(hoveredState);
                    e.Use();
                    _window.Repaint();
                    return true;
                }

                if (hovered is CommentGroupView hoveredGroup)
                {
                    _operations.StartEditingGroup(hoveredGroup);
                    e.Use();
                    _window.Repaint();
                    return true;
                }

                StateView singleState = null;
                for (int i = 0; i < _window.SelectionController.Count; i++)
                {
                    if (_window.SelectionController.Selected[i] is StateView s)
                    {
                        if (singleState != null) { singleState = null; break; }
                        singleState = s;
                    }
                }

                if (singleState != null)
                {
                    _operations.StartEditing(singleState);
                    e.Use();
                    _window.Repaint();
                    return true;
                }

                CommentGroupView singleGroup = null;
                for (int i = 0; i < _window.SelectionController.Count; i++)
                {
                    if (_window.SelectionController.Selected[i] is CommentGroupView g)
                    {
                        if (singleGroup != null) { singleGroup = null; break; }
                        singleGroup = g;
                    }
                }

                if (singleGroup != null)
                {
                    _operations.StartEditingGroup(singleGroup);
                    e.Use();
                    _window.Repaint();
                    return true;
                }
            }

            if (e.keyCode is KeyCode.Delete or KeyCode.Backspace)
            {
                _operations.DeleteSelected();
                e.Use();
                _window.Repaint();
            }

            if (e.keyCode == KeyCode.F && e.control)
            {
                _window.SearchPanel.Show();
                e.Use();
                _window.Repaint();
                return true;
            }

            if (e.keyCode == KeyCode.S && e.control)
            {
                _window.OnSaveCommandInternal();
                e.Use();
                _window.Repaint();
            }

            return false;
        }

        public void HandleConnectingInput(Rect viewRect)
        {
            var e = Event.current;
            Vector2 graphMousePos = (e.mousePosition - _window.PanOffset) / _window.Zoom;

            switch (e.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    _window.ConnectionController.UpdatePending(graphMousePos);
                    _window.Repaint();
                    e.Use();
                    break;

                case EventType.MouseDown when e.button == 0 && viewRect.Contains(e.mousePosition):
                {
                    StateView source = _window.ConnectionController.SourceNode;
                    StateView target = HitTestState(graphMousePos);

                    if (target != null)
                    {
                        bool targetIsBlockedEntry = (target.IsEntry && !target.IsSubEntry) || target.IsAnyState;
                        if (target == source || targetIsBlockedEntry)
                        {
                            _window.ConnectionController.Cancel();
                            e.Use();
                            _window.Repaint();
                            break;
                        }

                        var cmd = new CompositeCommand("Create Connection");

                        if (source.IsEntry)
                        {
                            ConnectionView existing = _operations.GetEntryOutgoingConnection();
                            if (existing != null)
                                cmd.Add(new DeleteConnectionCommand(_window.Connections, existing));
                        }

                        cmd.Add(new CreateConnectionCommand(_window.Connections, new ConnectionView(source, target)));
                        _window.UndoRedoSystem.Execute(cmd);
                        _window.MarkChangedInternal();
                    }
                    else
                    {
                        var newState = new StateView(graphMousePos - new Vector2(80f, 20f)) { DataIndex = _window.GetNextDataIndex() };
                        var cmd = new CompositeCommand("Create State and Connect");

                        if (source.IsEntry)
                        {
                            ConnectionView existing = _operations.GetEntryOutgoingConnection();
                            if (existing != null)
                                cmd.Add(new DeleteConnectionCommand(_window.Connections, existing));
                        }

                        cmd.Add(new CreateStateCommand(_window.States, newState));
                        cmd.Add(new CreateConnectionCommand(_window.Connections, new ConnectionView(source, newState)));
                        _window.UndoRedoSystem.Execute(cmd);

                        _operations.AddToExpandedContainer(newState);
                        _window.MarkChangedInternal();
                    }

                    _window.ConnectionController.Cancel();
                    e.Use();
                    _window.Repaint();
                    break;
                }

                case EventType.MouseDown when e.button == 1:
                    _window.ConnectionController.Cancel();
                    e.Use();
                    _window.Repaint();
                    break;
            }
        }

        public void HandleLeftClickInteraction(Rect viewRect)
        {
            var e = Event.current;
            if (e.button != 0)
                return;

            Vector2 graphMousePos = (e.mousePosition - _window.PanOffset) / _window.Zoom;

            switch (e.type)
            {
                case EventType.MouseDown when viewRect.Contains(e.mousePosition):
                    OnLeftMouseDown(graphMousePos, e);
                    break;

                case EventType.MouseDrag when viewRect.Contains(e.mousePosition):
                    OnLeftMouseDrag(graphMousePos, e);
                    break;

                case EventType.MouseUp:
                    OnLeftMouseUp(graphMousePos, e);
                    break;
            }
        }

        private void OnLeftMouseDown(Vector2 graphPos, Event e)
        {
            var focused = _window.rootVisualElement?.panel?.focusController?.focusedElement;
            if (focused is VisualElement ve && ve.focusable)
            {
                ve.Blur();
            }

            if (_window.EditingState == null && _window.EditingGroup == null && !e.shift)
            {
                for (int i = _window.Groups.Count - 1; i >= 0; i--)
                {
                    var group = _window.Groups[i];
                    var edge = GetResizeEdge(group, graphPos);
                    if (edge != ResizeEdge.None)
                    {
                        _window.BeginGroupResize(group, edge, graphPos);
                        e.Use();
                        return;
                    }
                }
            }

            ISelectable hit = HitTest(graphPos);

            HandleDoubleClickDetection(hit);

            if (_window.EditingState != null && hit != _window.EditingState)
            {
                _window.EditingState.CommitEditing();
            }

            if (hit != null)
            {
                if (e.shift)
                {
                    _window.SelectionController.Toggle(hit);
                }
                else if (!_window.SelectionController.IsSelected(hit))
                {
                    _window.SelectionController.SelectOnly(hit);
                }

                if (_window.EditingState == null && _window.EditingGroup == null)
                {
                    var dragItems = GetDragItems();
                    CapturePreDragPositions(dragItems);
                    _window.DragController.StartDrag(graphPos, dragItems);
                }
            }
            else
            {
                if (!e.shift)
                    _window.SelectionController.Clear();

                if (_window.EditingState == null && _window.EditingGroup == null)
                    _window.SelectionBox.Start(graphPos);
            }

            if (!(_window.EditingState != null && hit == _window.EditingState))
            {
                e.Use();
            }
        }

        private void HandleDoubleClickDetection(ISelectable hit)
        {
            if (hit is StateView sv)
            {
                long now = CleanStateMachineWindow.ClickStopwatch.ElapsedMilliseconds;
                long elapsed = now - _window.LastClickTimestamp;
                bool sameState = sv == _window.LastDoubleClickCandidate;
                bool doubleClick = sameState && elapsed < CleanStateMachineWindow.DoubleClickTimeMs;

                if (doubleClick)
                {
                    _window.LastDoubleClickCandidate = null;
                    if (!_window.SelectionController.IsSelected(sv))
                        _window.SelectionController.SelectOnly(sv);
                    _operations.StartEditing(sv);
                }

                if (!doubleClick)
                {
                    _window.LastClickTimestamp = now;
                    _window.LastDoubleClickCandidate = sv;
                }
            }
            else if (hit is CommentGroupView gv)
            {
                long now = CleanStateMachineWindow.ClickStopwatch.ElapsedMilliseconds;
                long elapsed = now - _window.LastClickTimestamp;
                bool sameGroup = gv == _window.LastDoubleClickCandidateGroup;
                bool doubleClick = sameGroup && elapsed < CleanStateMachineWindow.DoubleClickTimeMs;

                if (doubleClick)
                {
                    _window.LastDoubleClickCandidateGroup = null;
                    if (!_window.SelectionController.IsSelected(gv))
                        _window.SelectionController.SelectOnly(gv);
                    _operations.StartEditingGroup(gv);
                }

                if (!doubleClick)
                {
                    _window.LastClickTimestamp = now;
                    _window.LastDoubleClickCandidateGroup = gv;
                }
            }
            else
            {
                _window.LastDoubleClickCandidate = null;
                _window.LastDoubleClickCandidateGroup = null;
            }
        }

        private void OnLeftMouseDrag(Vector2 graphPos, Event e)
        {
            if (_window.ResizingGroup != null)
            {
                Vector2 delta = graphPos - _window.ResizeStartGraphPos;
                Rect r = _window.ResizeStartRect;

                if (_window.ResizeEdgeFlags.HasFlag(ResizeEdge.Left))
                {
                    float newX = Mathf.Min(r.xMax - CleanStateMachineWindow.MinGroupWidth, r.x + delta.x);
                    r.xMin = newX;
                }
                if (_window.ResizeEdgeFlags.HasFlag(ResizeEdge.Right))
                {
                    r.xMax = Mathf.Max(r.xMin + CleanStateMachineWindow.MinGroupWidth, r.xMax + delta.x);
                }
                if (_window.ResizeEdgeFlags.HasFlag(ResizeEdge.Top))
                {
                    float newY = Mathf.Min(r.yMax - CleanStateMachineWindow.MinGroupHeight, r.y + delta.y);
                    r.yMin = newY;
                }
                if (_window.ResizeEdgeFlags.HasFlag(ResizeEdge.Bottom))
                {
                    r.yMax = Mathf.Max(r.yMin + CleanStateMachineWindow.MinGroupHeight, r.yMax + delta.y);
                }

                _window.ResizingGroup.SetRect(r);
                _window.LastDoubleClickCandidate = null;
                e.Use();
            }
            else if (_window.DragController.IsActive)
            {
                _window.DragController.UpdateDrag(graphPos, _window.Zoom);
                if (_window.DragController.IsMoving)
                    _window.LastDoubleClickCandidate = null;
            }
            else if (_window.SelectionBox.IsActive)
            {
                _window.SelectionBox.Update(graphPos);
                PerformBoxSelection(_window.SelectionBox.GetGraphRect(), e.shift);
                _window.LastDoubleClickCandidate = null;
            }

            e.Use();
        }

        private void OnLeftMouseUp(Vector2 graphPos, Event e)
        {
            if (_window.ResizingGroup != null)
            {
                Rect newRect = _window.ResizingGroup.GetGraphBounds();
                if (Mathf.Abs(newRect.x - _window.ResizeStartRect.x) > 0.001f ||
                    Mathf.Abs(newRect.y - _window.ResizeStartRect.y) > 0.001f ||
                    Mathf.Abs(newRect.width - _window.ResizeStartRect.width) > 0.001f ||
                    Mathf.Abs(newRect.height - _window.ResizeStartRect.height) > 0.001f)
                {
                    var cmd = new ResizeGroupCommand(_window.ResizingGroup, _window.ResizeStartRect, newRect);
                    _window.UndoRedoSystem.Execute(cmd);
                    _window.MarkChangedInternal();
                }
                _window.ResizingGroup = null;
                _operations.SyncStatesWithGroups();
                _operations.SyncStatesWithSubMachines();
                e.Use();
            }
            else if (_window.DragController.IsActive)
            {
                bool wasMoving = _window.DragController.IsMoving;
                _window.DragController.EndDrag();
                var moveCmd = CreateMoveCommandIfMoved();
                if (moveCmd != null)
                {
                    _window.UndoRedoSystem.Execute(moveCmd);
                    _window.MarkChangedInternal();
                }
                _preDragPositions = null;
                if (wasMoving)
                    _window.LastDoubleClickCandidate = null;
                _operations.SyncStatesWithGroups();
                _operations.SyncStatesWithSubMachines();
                e.Use();
            }
            else if (_window.SelectionBox.IsActive)
            {
                if (_window.SelectionBox.HasValidDrag(_window.Zoom))
                    PerformBoxSelection(_window.SelectionBox.GetGraphRect(), e.shift);
                _window.SelectionBox.End();
                _window.LastDoubleClickCandidate = null;
                e.Use();
            }
        }

        public void PerformBoxSelection(Rect graphRect, bool shiftHeld)
        {
            if (!shiftHeld)
                _window.SelectionController.Clear();

            var boxStates = new List<StateView>();
            for (int i = 0; i < _window.States.Count; i++)
            {
                if (!_window.IsStateVisible(_window.States[i])) continue;
                if (graphRect.Overlaps(_window.States[i].GetGraphBounds()))
                    boxStates.Add(_window.States[i]);
            }
            _window.SelectionController.SelectRange(boxStates);

            var boxConnections = new List<ConnectionView>();
            for (int i = 0; i < _window.Connections.Count; i++)
            {
                if (!_window.IsConnectionVisible(_window.Connections[i])) continue;
                if (_window.Connections[i].BoxOverlaps(graphRect))
                    boxConnections.Add(_window.Connections[i]);
            }
            _window.SelectionController.SelectRange(boxConnections);

            var boxGroups = new List<CommentGroupView>();
            for (int i = 0; i < _window.Groups.Count; i++)
                if (graphRect.Overlaps(_window.Groups[i].GetGraphBounds()))
                    boxGroups.Add(_window.Groups[i]);
            _window.SelectionController.SelectRange(boxGroups);
        }

        private List<ISelectable> GetDragItems()
        {
            List<ISelectable> selected = new(_window.SelectionController.Selected);
            HashSet<StateView> groupMembers = new();
            for (int i = 0; i < _window.Groups.Count; i++)
            {
                if (_window.SelectionController.IsSelected(_window.Groups[i]))
                {
                    for (int j = 0; j < _window.Groups[i].Members.Count; j++)
                        groupMembers.Add(_window.Groups[i].Members[j]);
                }
            }

            HashSet<StateView> subChildren = new();
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is StateView sv && sv.IsSubStateMachine)
                {
                    for (int j = 0; j < sv.ChildIndices.Count; j++)
                    {
                        var child = _operations.GetStateByIndex(sv.ChildIndices[j]);
                        if (child != null)
                            subChildren.Add(child);
                    }
                }
            }

            for (int i = selected.Count - 1; i >= 0; i--)
            {
                if (selected[i] is StateView s && (groupMembers.Contains(s) || subChildren.Contains(s)))
                    selected.RemoveAt(i);
            }

            selected.AddRange(subChildren);
            return selected;
        }

        private void CapturePreDragPositions(List<ISelectable> items)
        {
            _preDragPositions = new Dictionary<ISelectable, Vector2>(items.Count);
            for (int i = 0; i < items.Count; i++)
                _preDragPositions[items[i]] = items[i].Position;
        }

        private MoveStatesCommand CreateMoveCommandIfMoved()
        {
            if (_preDragPositions == null || _preDragPositions.Count == 0)
                return null;

            var items = new List<ISelectable>(_preDragPositions.Count);
            var startPositions = new List<Vector2>(_preDragPositions.Count);
            var endPositions = new List<Vector2>(_preDragPositions.Count);
            bool moved = false;

            foreach (var kvp in _preDragPositions)
            {
                Vector2 currentPos = kvp.Key.Position;
                if ((currentPos - kvp.Value).sqrMagnitude > 0.0001f)
                    moved = true;

                items.Add(kvp.Key);
                startPositions.Add(kvp.Value);
                endPositions.Add(currentPos);
            }

            return moved ? new MoveStatesCommand(items, startPositions, endPositions) : null;
        }

        public ISelectable HitTest(Vector2 graphPos)
        {
            for (int i = _window.States.Count - 1; i >= 0; i--)
            {
                if (!_window.IsStateVisible(_window.States[i])) continue;
                if (_window.States[i].ContainsPoint(graphPos))
                    return _window.States[i];
            }

            for (int i = _window.Connections.Count - 1; i >= 0; i--)
            {
                if (!_window.IsConnectionVisible(_window.Connections[i])) continue;
                if (_window.Connections[i].ContainsPoint(graphPos))
                    return _window.Connections[i];
            }

            for (int i = _window.Groups.Count - 1; i >= 0; i--)
            {
                if (_window.Groups[i].ContainsPoint(graphPos))
                    return _window.Groups[i];
            }

            return null;
        }

        public StateView HitTestState(Vector2 graphPos)
        {
            for (int i = _window.States.Count - 1; i >= 0; i--)
            {
                if (!_window.IsStateVisible(_window.States[i])) continue;
                if (_window.States[i].ContainsPoint(graphPos))
                    return _window.States[i];
            }

            return null;
        }

        public ResizeEdge GetResizeEdge(CommentGroupView group, Vector2 graphPos)
        {
            float threshold = CleanStateMachineWindow.ResizeHandleScreenSize / _window.Zoom;
            Rect r = group.GetGraphBounds();

            bool nearLeft = Mathf.Abs(graphPos.x - r.xMin) <= threshold;
            bool nearRight = Mathf.Abs(graphPos.x - r.xMax) <= threshold;
            bool nearTop = Mathf.Abs(graphPos.y - r.yMin) <= threshold;
            bool nearBottom = Mathf.Abs(graphPos.y - r.yMax) <= threshold;

            ResizeEdge edge = ResizeEdge.None;
            if (nearLeft) edge |= ResizeEdge.Left;
            if (nearRight) edge |= ResizeEdge.Right;
            if (nearTop) edge |= ResizeEdge.Top;
            if (nearBottom) edge |= ResizeEdge.Bottom;

            return edge;
        }
    }
}
