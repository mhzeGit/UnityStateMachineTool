using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public class CleanStateMachineWindow : EditorWindow
    {
        [MenuItem("Tools/CleanStateMachine")]
        public static void ShowWindow()
        {
            var window = CreateWindow<CleanStateMachineWindow>();
            window.titleContent = new GUIContent("CleanStateMachine");
            window.Show();
        }

        [System.Serializable]
        private class CopiedStateData
        {
            public Vector2 position;
            public string name;
            public Vector2 size;
        }

        private static List<CopiedStateData> _clipboard;

        [SerializeField] private Vector2 _panOffset;
        [SerializeField] private float _zoom = 1f;

        [SerializeField] private bool _showBlackboard = true;
        [SerializeField] private float _blackboardWidth = 220f;
        [SerializeField] private float _detailsWidth = 220f;
        [SerializeField] private List<BlackboardVariable> _blackboardVariables = new();

        private BlackboardView _blackboardView;
        private DetailsPanelView _detailsView;
        private bool _isDraggingLeftSplitter;
        private bool _isDraggingRightSplitter;

        private Vector2 _lastMouseGraphPos;

        private UndoRedoSystem _undoRedoSystem;
        private GraphView _graphView;
        private GraphPanController _panController;
        private GraphContextMenu _contextMenu;
        private SelectionController _selectionController;
        private DragController _dragController;
        private SelectionBox _selectionBox;
        private ConnectionController _connectionController;

        private readonly List<StateView> _states = new();
        private readonly List<ConnectionView> _connections = new();
        private readonly List<CommentGroupView> _groups = new();
        private Dictionary<ISelectable, Vector2> _preDragPositions;
        private StateView _entryState;
        private StateView _editingState;
        private string _editingOriginalName;
        private bool _focusRequested;
        private bool _selectAllRequested;
        private double _lastClickTime;
        private StateView _lastDoubleClickCandidate;

        private static readonly Vector2 EntryStatePosition = new Vector2(50f, 200f);

        private void OnEnable()
        {
            wantsMouseMove = true;
            _undoRedoSystem = new UndoRedoSystem();
            _graphView = new GraphView();
            _panController = new GraphPanController();
            _contextMenu = new GraphContextMenu();
            _selectionController = new SelectionController();
            _dragController = new DragController();
            _selectionBox = new SelectionBox();
            _connectionController = new ConnectionController();

            _blackboardView = new BlackboardView();
            _detailsView = new DetailsPanelView();

            _blackboardView.VariablesChanged += Repaint;

            EnsureEntryStateExists();

            _contextMenu.CreateStateRequested += OnCreateStateRequested;
            _contextMenu.ConnectRequested += OnConnectRequested;
            _contextMenu.UngroupRequested += OnUngroupRequested;
            _contextMenu.CopyRequested += CopySelectedStates;
            _contextMenu.PasteRequested += PasteStates;
            _contextMenu.DeleteRequested += DeleteSelected;
            _connectionController.ConnectionCompleted += OnConnectionCompleted;
            _selectionController.SelectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            _contextMenu.CreateStateRequested -= OnCreateStateRequested;
            _contextMenu.ConnectRequested -= OnConnectRequested;
            _contextMenu.UngroupRequested -= OnUngroupRequested;
            _contextMenu.CopyRequested -= CopySelectedStates;
            _contextMenu.PasteRequested -= PasteStates;
            _contextMenu.DeleteRequested -= DeleteSelected;
            _connectionController.ConnectionCompleted -= OnConnectionCompleted;
            _selectionController.SelectionChanged -= OnSelectionChanged;

            _blackboardView.VariablesChanged -= Repaint;
        }

        private void OnGUI()
        {
            if (position.width < 1f || position.height < 1f)
                return;

            var e = Event.current;

            ComputeLayout(out Rect graphRect, out Rect leftRect, out Rect rightRect,
                out Rect leftSplitterRect, out Rect rightSplitterRect);

            _panController.HandleInput(graphRect, ref _panOffset, ref _zoom);

            if (_editingState != null)
            {
                if (e.type == EventType.KeyDown)
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        CommitEditing();
                        e.Use();
                        Repaint();
                    }
                    else if (e.keyCode == KeyCode.Escape)
                    {
                        CancelEditing();
                        e.Use();
                        Repaint();
                    }
                }
            }

            _lastMouseGraphPos = (e.mousePosition - _panOffset) / _zoom;

            if (!_connectionController.IsConnecting && _editingState == null)
                HandleKeyboardShortcuts(e);

            if (e.type == EventType.ContextClick && graphRect.Contains(e.mousePosition))
            {
                if (_editingState != null)
                    CommitEditing();

                _connectionController.Cancel();
                Vector2 graphMousePosition = (e.mousePosition - _panOffset) / _zoom;
                ISelectable hit = HitTest(graphMousePosition);
                StateView hitState = hit as StateView;
                CommentGroupView hitGroup = hit as CommentGroupView;
                _contextMenu.Show(graphMousePosition, hitState, hitGroup,
                    _selectionController.Count > 0,
                    _clipboard is { Count: > 0 });
                e.Use();
            }

            if (_connectionController.IsConnecting)
                HandleConnectingInput(graphRect);
            else
                HandleLeftClickInteraction(graphRect);

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (_connectionController.IsConnecting)
                {
                    _connectionController.Cancel();
                    e.Use();
                    Repaint();
                }
            }

            _graphView.Draw(graphRect, _panOffset, _zoom);
            DrawGroups();
            DrawConnections();
            _connectionController.DrawPending(_zoom, _panOffset);
            DrawStates();

            if (_focusRequested)
            {
                EditorGUI.FocusTextInControl("StateRenameField");
                _focusRequested = false;
                _selectAllRequested = true;
            }

            if (_selectAllRequested && Event.current.type == EventType.Repaint)
            {
                if (GUI.GetNameOfFocusedControl() == "StateRenameField")
                {
                    var textEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                    if (textEditor != null)
                        textEditor.SelectAll();
                    _selectAllRequested = false;
                }
            }

            DrawSelectionOverlays();
            _selectionBox.DrawScreen(_zoom, _panOffset);

            if (_showBlackboard)
            {
                _blackboardView.Draw(leftRect, _blackboardVariables);
                DrawBlackboardToggle(leftRect);
            }
            else
                DrawCollapsedPanel(leftRect, ">", ref _showBlackboard);

            if (_selectionController.Count > 0)
                _detailsView.Draw(rightRect, _selectionController.Selected, _states, _connections);

            HandleSplitter(leftSplitterRect, _showBlackboard, ref _isDraggingLeftSplitter, isLeft: true);
            HandleSplitter(rightSplitterRect, _selectionController.Count > 0, ref _isDraggingRightSplitter, isLeft: false);

            if (_panController.IsPanning || _dragController.IsActive || _selectionBox.IsActive || _connectionController.IsConnecting)
                Repaint();
        }

        private void ComputeLayout(out Rect graphRect, out Rect leftRect, out Rect rightRect,
            out Rect leftSplitterRect, out Rect rightSplitterRect)
        {
            float leftW = _showBlackboard ? _blackboardWidth : UITheme.CollapsedWidth;
            float rightW = _selectionController.Count > 0 ? _detailsWidth : 0f;
            float splitter = UITheme.SplitterWidth;

            float minGraph = 200f;
            float leftMax = position.width - rightW - splitter * 2f - minGraph;
            float rightMax = position.width - leftW - splitter * 2f - minGraph;

            if (_showBlackboard && _blackboardWidth > leftMax)
                _blackboardWidth = Mathf.Max(UITheme.MinPanelWidth, leftMax);

            if (_selectionController.Count > 0 && _detailsWidth > rightMax)
                _detailsWidth = Mathf.Max(UITheme.MinPanelWidth, rightMax);

            leftW = _showBlackboard ? _blackboardWidth : UITheme.CollapsedWidth;
            rightW = _selectionController.Count > 0 ? _detailsWidth : 0f;

            leftRect = new Rect(0f, 0f, leftW, position.height);

            float rightX = position.width - rightW;
            rightRect = new Rect(rightX, 0f, rightW, position.height);

            float gx = leftW + (_showBlackboard ? splitter : 0f);
            float gw = position.width - leftW - rightW
                - (_showBlackboard ? splitter : 0f)
                - (_selectionController.Count > 0 ? splitter : 0f);
            graphRect = new Rect(gx, 0f, gw, position.height);

            leftSplitterRect = _showBlackboard
                ? new Rect(leftW, 0f, splitter, position.height)
                : new Rect(0f, 0f, 0f, 0f);

            rightSplitterRect = _selectionController.Count > 0
                ? new Rect(rightX - splitter, 0f, splitter, position.height)
                : new Rect(0f, 0f, 0f, 0f);
        }

        private void DrawBlackboardToggle(Rect panelRect)
        {
            float toggleSize = 20f;
            Rect toggleRect = new Rect(
                panelRect.xMax - toggleSize,
                panelRect.y + 4f,
                toggleSize,
                toggleSize
            );

            bool hover = toggleRect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(toggleRect, hover ? UITheme.ButtonHover : UITheme.PanelHeaderBg);

            var style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = hover ? Color.white : UITheme.TextSecondary }
            };
            GUI.Label(toggleRect, "<", style);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && toggleRect.Contains(Event.current.mousePosition))
            {
                _showBlackboard = false;
                Event.current.Use();
                Repaint();
            }
        }

        private static void DrawCollapsedPanel(Rect rect, string label, ref bool showPanel)
        {
            EditorGUI.DrawRect(rect, UITheme.PanelHeaderBg);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1f, rect.y, 1f, rect.height), UITheme.PanelBorder);

            float labelH = 22f;
            Rect labelRect = new Rect(rect.x, rect.y + 4f, rect.width, labelH);
            bool hover = labelRect.Contains(Event.current.mousePosition);
            var style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = hover ? Color.white : UITheme.TextSecondary }
            };
            GUI.Label(labelRect, label, style);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                showPanel = true;
                e.Use();
            }
        }

        private void HandleSplitter(Rect rect, bool visible, ref bool isDragging, bool isLeft)
        {
            if (!visible || rect.width <= 0f)
                return;

            bool hover = rect.Contains(Event.current.mousePosition);
            UITheme.DrawSplitter(rect, hover || isDragging);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && rect.Contains(e.mousePosition):
                    isDragging = true;
                    e.Use();
                    break;
                case EventType.MouseDrag when isDragging:
                {
                    if (isLeft)
                        _blackboardWidth = Mathf.Clamp(e.mousePosition.x, UITheme.MinPanelWidth, UITheme.MaxPanelWidth);
                    else
                        _detailsWidth = Mathf.Clamp(
                            position.width - e.mousePosition.x - UITheme.SplitterWidth,
                            UITheme.MinPanelWidth, UITheme.MaxPanelWidth);
                    e.Use();
                    Repaint();
                    break;
                }
                case EventType.MouseUp when isDragging:
                    isDragging = false;
                    e.Use();
                    break;
            }
        }

        private void HandleKeyboardShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Z && e.control)
            {
                if (_undoRedoSystem.Undo())
                    Repaint();
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Y && e.control)
            {
                if (_undoRedoSystem.Redo())
                    Repaint();
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.G && e.control)
            {
                CreateGroupFromSelectedStates();
                e.Use();
                Repaint();
                return;
            }

            if (e.keyCode == KeyCode.C && e.control)
            {
                CopySelectedStates();
                e.Use();
                Repaint();
                return;
            }

            if (e.keyCode == KeyCode.C && !e.control)
            {
                StateView source = null;
                for (int i = 0; i < _selectionController.Count; i++)
                {
                    if (_selectionController.Selected[i] is StateView s)
                    {
                        if (source != null) { source = null; break; }
                        source = s;
                    }
                }

                if (source != null)
                {
                    _connectionController.StartConnection(source);
                    e.Use();
                    Repaint();
                    return;
                }
            }

            if (e.keyCode == KeyCode.V && e.control)
            {
                PasteStates();
                e.Use();
                Repaint();
                return;
            }

            if (e.keyCode is KeyCode.Delete or KeyCode.Backspace)
            {
                DeleteSelected();
                e.Use();
                Repaint();
            }
        }

        private void HandleConnectingInput(Rect viewRect)
        {
            var e = Event.current;
            Vector2 graphMousePos = (e.mousePosition - _panOffset) / _zoom;

            switch (e.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    _connectionController.UpdatePending(graphMousePos);
                    Repaint();
                    e.Use();
                    break;

                case EventType.MouseDown when e.button == 0 && viewRect.Contains(e.mousePosition):
                {
                    StateView source = _connectionController.SourceNode;
                    StateView target = HitTestState(graphMousePos);

                    if (target != null && target != source)
                    {
                        var connection = new ConnectionView(source, target);
                        var cmd = new CreateConnectionCommand(_connections, connection);
                        _undoRedoSystem.Execute(cmd);
                    }
                    else
                    {
                        var newState = new StateView(graphMousePos);
                        var connection = new ConnectionView(source, newState);
                        var composite = new CompositeCommand("Create State and Connect");
                        composite.Add(new CreateStateCommand(_states, newState));
                        composite.Add(new CreateConnectionCommand(_connections, connection));
                        _undoRedoSystem.Execute(composite);
                    }

                    _connectionController.Cancel();
                    e.Use();
                    Repaint();
                    break;
                }

                case EventType.MouseDown when e.button == 1:
                    _connectionController.Cancel();
                    e.Use();
                    Repaint();
                    break;
            }
        }

        private void HandleLeftClickInteraction(Rect viewRect)
        {
            var e = Event.current;
            if (e.button != 0)
                return;

            Vector2 graphMousePos = (e.mousePosition - _panOffset) / _zoom;

            switch (e.type)
            {
                case EventType.MouseDown when viewRect.Contains(e.mousePosition):
                    OnLeftMouseDown(graphMousePos, e);
                    break;

                case EventType.MouseDrag:
                    OnLeftMouseDrag(graphMousePos, e);
                    break;

                case EventType.MouseUp:
                    OnLeftMouseUp(graphMousePos, e);
                    break;
            }
        }

        private void OnLeftMouseDown(Vector2 graphPos, Event e)
        {
            ISelectable hit = HitTest(graphPos);

            if (hit is StateView sv && !sv.IsEntry)
            {
                double now = EditorApplication.timeSinceStartup;
                if (sv == _lastDoubleClickCandidate && (now - _lastClickTime) < 0.3)
                {
                    _lastDoubleClickCandidate = null;
                    if (!_selectionController.IsSelected(sv))
                        _selectionController.SelectOnly(sv);
                    StartEditing(sv);
                    e.Use();
                    return;
                }
                _lastClickTime = now;
                _lastDoubleClickCandidate = sv;
            }
            else
            {
                _lastDoubleClickCandidate = null;
            }

            if (_editingState != null && hit != _editingState)
            {
                CommitEditing();
            }

            if (hit != null)
            {
                if (e.shift)
                {
                    _selectionController.Toggle(hit);
                }
                else if (!_selectionController.IsSelected(hit))
                {
                    _selectionController.SelectOnly(hit);
                }

                if (!(hit is StateView s && s.IsEntry) && _editingState == null)
                {
                    var dragItems = GetDragItems();
                    CapturePreDragPositions(dragItems);
                    _dragController.StartDrag(graphPos, dragItems);
                }
            }
            else
            {
                if (!e.shift)
                    _selectionController.Clear();

                if (_editingState == null)
                    _selectionBox.Start(graphPos);
            }

            if (!(_editingState != null && hit == _editingState))
            {
                e.Use();
            }
        }

        private void OnLeftMouseDrag(Vector2 graphPos, Event e)
        {
            if (_dragController.IsActive)
            {
                _dragController.UpdateDrag(graphPos, _zoom);
                if (_dragController.IsMoving)
                    _lastDoubleClickCandidate = null;
            }
            else if (_selectionBox.IsActive)
            {
                _selectionBox.Update(graphPos);
                _lastDoubleClickCandidate = null;
            }

            e.Use();
        }

        private void OnLeftMouseUp(Vector2 graphPos, Event e)
        {
            if (_dragController.IsActive)
            {
                bool wasMoving = _dragController.IsMoving;
                _dragController.EndDrag();
                var moveCmd = CreateMoveCommandIfMoved();
                if (moveCmd != null)
                    _undoRedoSystem.Execute(moveCmd);
                _preDragPositions = null;
                if (wasMoving)
                    _lastDoubleClickCandidate = null;
            }
            else if (_selectionBox.IsActive)
            {
                if (!e.shift)
                    _selectionController.Clear();

                Rect r = _selectionBox.GetGraphRect();

                var boxStates = new List<StateView>();
                for (int i = 0; i < _states.Count; i++)
                    if (r.Overlaps(_states[i].GetGraphBounds()))
                        boxStates.Add(_states[i]);
                _selectionController.SelectRange(boxStates);

                var boxConnections = new List<ConnectionView>();
                for (int i = 0; i < _connections.Count; i++)
                    if (r.Overlaps(_connections[i].GetGraphBounds()))
                        boxConnections.Add(_connections[i]);
                _selectionController.SelectRange(boxConnections);

                var boxGroups = new List<CommentGroupView>();
                for (int i = 0; i < _groups.Count; i++)
                    if (r.Overlaps(_groups[i].GetGraphBounds()))
                        boxGroups.Add(_groups[i]);
                _selectionController.SelectRange(boxGroups);

                _selectionBox.End();
                _lastDoubleClickCandidate = null;
            }

            e.Use();
        }

        private List<ISelectable> GetDragItems()
        {
            List<ISelectable> selected = new(_selectionController.Selected);
            HashSet<StateView> groupMembers = new();
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_selectionController.IsSelected(_groups[i]))
                {
                    for (int j = 0; j < _groups[i].Members.Count; j++)
                        groupMembers.Add(_groups[i].Members[j]);
                }
            }

            for (int i = selected.Count - 1; i >= 0; i--)
            {
                if (selected[i] is StateView s && (groupMembers.Contains(s) || s.IsEntry))
                    selected.RemoveAt(i);
            }

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

        private ISelectable HitTest(Vector2 graphPos)
        {
            for (int i = _states.Count - 1; i >= 0; i--)
            {
                if (_states[i].ContainsPoint(graphPos))
                    return _states[i];
            }

            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                if (_connections[i].ContainsPoint(graphPos))
                    return _connections[i];
            }

            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                if (_groups[i].ContainsPoint(graphPos))
                    return _groups[i];
            }

            return null;
        }

        private StateView HitTestState(Vector2 graphPos)
        {
            for (int i = _states.Count - 1; i >= 0; i--)
            {
                if (_states[i].ContainsPoint(graphPos))
                    return _states[i];
            }

            return null;
        }

        private void DrawGroups()
        {
            for (int i = 0; i < _groups.Count; i++)
                _groups[i].Draw(_zoom, _panOffset);
        }

        private void CreateGroupFromSelectedStates()
        {
            var selectedStates = new List<StateView>();
            for (int i = 0; i < _selectionController.Count; i++)
            {
                if (_selectionController.Selected[i] is StateView s)
                    selectedStates.Add(s);
            }

            if (selectedStates.Count < 1) return;

            var group = new CommentGroupView(selectedStates, $"Group {_groups.Count + 1}");
            var cmd = new CreateGroupCommand(_groups, group);
            _undoRedoSystem.Execute(cmd);

            _selectionController.Clear();
            _selectionController.Select(group);
        }

        private void DrawSelectionOverlays()
        {
            var selected = _selectionController.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                selected[i].DrawSelectionOverlay(_zoom, _panOffset);
            }
        }

        private void DrawStates()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                _states[i].Draw(_zoom, _panOffset);
            }
        }

        private void DrawConnections()
        {
            var groups = new Dictionary<(StateView, StateView), List<ConnectionView>>();
            for (int i = 0; i < _connections.Count; i++)
            {
                var conn = _connections[i];
                var key = conn.From.GetHashCode() < conn.To.GetHashCode()
                    ? (conn.From, conn.To)
                    : (conn.To, conn.From);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<ConnectionView>();
                    groups[key] = list;
                }
                list.Add(conn);
            }

            foreach (var kvp in groups)
            {
                var list = kvp.Value;
                list.Sort((a, b) => a.GetHashCode().CompareTo(b.GetHashCode()));
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    float offset = count == 1 ? 0f : (i - (count - 1) * 0.5f) * 15f;
                    list[i].PerpendicularOffset = offset;
                    list[i].Draw(_zoom, _panOffset);
                }
            }
        }

        private void OnCreateStateRequested(Vector2 graphMousePosition)
        {
            var state = new StateView(graphMousePosition);
            var cmd = new CreateStateCommand(_states, state);
            _undoRedoSystem.Execute(cmd);
            Repaint();
        }

        private void OnConnectRequested(StateView source)
        {
            _connectionController.StartConnection(source);
            Repaint();
        }

        private void OnConnectionCompleted(StateView from, StateView to)
        {
            var connection = new ConnectionView(from, to);
            var cmd = new CreateConnectionCommand(_connections, connection);
            _undoRedoSystem.Execute(cmd);
            Repaint();
        }

        private void OnUngroupRequested(CommentGroupView group)
        {
            _selectionController.Deselect(group);
            var cmd = new UngroupCommand(_groups, group);
            _undoRedoSystem.Execute(cmd);
            Repaint();
        }

        private void CopySelectedStates()
        {
            _clipboard = new List<CopiedStateData>();
            for (int i = 0; i < _selectionController.Count; i++)
            {
                if (_selectionController.Selected[i] is StateView s && !s.IsEntry)
                {
                    _clipboard.Add(new CopiedStateData
                    {
                        position = s.Position,
                        name = s.Name,
                        size = s.Size
                    });
                }
            }
        }

        private void PasteStates()
        {
            if (_clipboard == null || _clipboard.Count == 0) return;

            Vector2 mouseGraphPos = _lastMouseGraphPos;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < _clipboard.Count; i++)
            {
                var d = _clipboard[i];
                if (d.position.x < minX) minX = d.position.x;
                if (d.position.x + d.size.x > maxX) maxX = d.position.x + d.size.x;
                if (d.position.y < minY) minY = d.position.y;
                if (d.position.y + d.size.y > maxY) maxY = d.position.y + d.size.y;
            }

            Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            Vector2 offset = mouseGraphPos - center;

            _selectionController.Clear();

            var composite = new CompositeCommand("Paste States");
            var pastedStates = new List<StateView>();

            for (int i = 0; i < _clipboard.Count; i++)
            {
                var data = _clipboard[i];
                var state = new StateView(data.position + offset, data.name)
                {
                    Size = data.size
                };
                composite.Add(new CreateStateCommand(_states, state));
                pastedStates.Add(state);
            }

            _undoRedoSystem.Execute(composite);

            for (int i = 0; i < pastedStates.Count; i++)
                _selectionController.Select(pastedStates[i]);

            Repaint();
        }

        private void DeleteSelected()
        {
            if (_selectionController.Count == 0) return;

            for (int i = _selectionController.Count - 1; i >= 0; i--)
            {
                if (_selectionController.Selected[i] is StateView s && s.IsEntry)
                    _selectionController.Deselect(s);
            }

            if (_selectionController.Count == 0) return;

            var cmd = new DeleteStatesCommand(_states, _connections, _groups, _selectionController);
            _undoRedoSystem.Execute(cmd);

            _selectionController.Clear();
            Repaint();
        }

        private void StartEditing(StateView state)
        {
            if (_editingState != null && _editingState != state)
            {
                _editingState.IsEditing = false;
                _editingState.Name = _editingOriginalName;
            }

            state.IsEditing = true;
            state.EditingBuffer = state.Name;
            _editingOriginalName = state.Name;
            _editingState = state;
            _focusRequested = true;
            Repaint();
        }

        private void CommitEditing()
        {
            if (_editingState == null)
                return;

            _editingState.IsEditing = false;
            string newName = _editingState.EditingBuffer;

            if (newName != _editingOriginalName && !string.IsNullOrEmpty(newName))
            {
                var cmd = new RenameStateCommand(_editingState, _editingOriginalName, newName);
                _undoRedoSystem.Execute(cmd);
            }
            else
            {
                _editingState.Name = _editingOriginalName;
            }

            _editingState = null;
            Repaint();
        }

        private void CancelEditing()
        {
            if (_editingState == null)
                return;

            _editingState.IsEditing = false;
            _editingState.Name = _editingOriginalName;

            _editingState = null;
            Repaint();
        }

        private void EnsureEntryStateExists()
        {
            _entryState = null;
            for (int i = 0; i < _states.Count; i++)
            {
                if (_states[i].IsEntry)
                {
                    _entryState = _states[i];
                    break;
                }
            }

            if (_entryState == null)
            {
                _entryState = new StateView(EntryStatePosition, "Entry", isEntry: true);
                _states.Insert(0, _entryState);
            }
        }

        private void OnSelectionChanged()
        {
            List<StateView> pickedStates = new();
            for (int i = 0; i < _states.Count; i++)
                if (_states[i].IsSelected)
                    pickedStates.Add(_states[i]);

            if (pickedStates.Count > 0)
            {
                for (int i = _states.Count - 1; i >= 0; i--)
                    if (_states[i].IsSelected)
                        _states.RemoveAt(i);
                _states.AddRange(pickedStates);
            }

            if (_entryState != null && _states[0] != _entryState)
            {
                _states.Remove(_entryState);
                _states.Insert(0, _entryState);
            }

            List<ConnectionView> pickedConnections = new();
            for (int i = 0; i < _connections.Count; i++)
                if (_connections[i].IsSelected)
                    pickedConnections.Add(_connections[i]);

            if (pickedConnections.Count > 0)
            {
                for (int i = _connections.Count - 1; i >= 0; i--)
                    if (_connections[i].IsSelected)
                        _connections.RemoveAt(i);
                _connections.AddRange(pickedConnections);
            }

            List<CommentGroupView> pickedGroups = new();
            for (int i = 0; i < _groups.Count; i++)
                if (_groups[i].IsSelected)
                    pickedGroups.Add(_groups[i]);

            if (pickedGroups.Count > 0)
            {
                for (int i = _groups.Count - 1; i >= 0; i--)
                    if (_groups[i].IsSelected)
                        _groups.RemoveAt(i);
                _groups.AddRange(pickedGroups);
            }
        }
    }
}
