using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public class CleanStateMachineWindow : EditorWindow
    {
        [System.Flags]
        private enum ResizeEdge
        {
            None = 0,
            Left = 1,
            Right = 2,
            Top = 4,
            Bottom = 8,
        }

        private struct NavigationFrame
        {
            public SerializableData ParentData;
            public SerializableData EnteredData;
            public string StateName;
        }

        [MenuItem("Tools/CleanStateMachine")]
        public static void ShowWindow()
        {
            var window = CreateWindow<CleanStateMachineWindow>();
            window.titleContent = new GUIContent("CleanStateMachine");
            window.Show();
        }

        public static void OpenWithController(StateMachineController controller)
        {
            var window = GetWindow<CleanStateMachineWindow>();
            window.titleContent = new GUIContent(controller.name);
            window._pendingController = controller;
            window.Show();
        }

        [System.Serializable]
        private class CopiedStateData
        {
            public Vector2 position;
            public string name;
            public Vector2 size;
            public MonoScript behaviourScript;
            public StateBehaviour behaviourInstance;
            public SerializableData subMachineData;
            public bool isSubStateMachine;
        }

        private static List<CopiedStateData> _clipboard;

        [SerializeField] private Vector2 _panOffset;
        [SerializeField] private float _zoom = 1f;

        [SerializeField] private bool _showSidePanel = true;
        [SerializeField] private float _sidePanelWidth = 220f;
        private List<BlackboardVariable> _blackboardVariables = new();
        [SerializeField] private StateMachineController _controller;

        private bool _hasUnsavedChanges;
        private bool _isLoading;
        private StateMachineController _pendingController;
        private float _detailsHeightRatio = 0.5f;
        private SidePanel _sidePanelElement;

        private Vector2 _lastMouseGraphPos;

        private GridBackground _gridBackground;
        private ConnectionArrowsLayer _connectionArrowsLayer;
        private VisualElement _stateLayer;
        private VisualElement _groupContainer;
        private IMGUIContainer _graphCanvas;
        private UndoRedoSystem _undoRedoSystem;
        private GraphPanController _panController;
        private GraphContextMenu _contextMenu;
        private SelectionController _selectionController;
        private DragController _dragController;
        private SelectionBox _selectionBox;
        private ConnectionController _connectionController;

        private readonly List<StateView> _states = new();
        private readonly List<ConnectionView> _connections = new();
        private readonly List<CommentGroupView> _groups = new();

        private SerializableData _currentData;
        private readonly List<NavigationFrame> _navigationPath = new();
        private VisualElement _breadcrumbBar;
        private Dictionary<ISelectable, Vector2> _preDragPositions;
        private StateView _entryState;
        private StateView _editingState;
        private CommentGroupView _editingGroup;
        private CommentGroupView _resizingGroup;
        private ResizeEdge _resizeEdgeFlags;
        private Vector2 _resizeStartGraphPos;
        private Rect _resizeStartRect;
        private static readonly Stopwatch _clickStopwatch = Stopwatch.StartNew();
        private long _lastClickTimestamp;
        private StateView _lastDoubleClickCandidate;
        private CommentGroupView _lastDoubleClickCandidateGroup;

        private StateMachineComponent _trackedComponent;
        private int _activeStateLevelIndex = -1;
        private int _activeStateDepth = -1;
        private bool _isAutoNavigating;

        private static readonly Vector2 EntryStatePosition = new Vector2(50f, 200f);
        private const float CollapsedPanelWidth = 35f;
        private const float ResizeHandleScreenSize = 8f;
        private const int DoubleClickTimeMs = 180;

        private void OnEnable()
        {
            wantsMouseMove = true;
            _undoRedoSystem = new UndoRedoSystem();
            _panController = new GraphPanController();
            _contextMenu = new GraphContextMenu();
            _selectionController = new SelectionController();
            _dragController = new DragController();
            _selectionBox = new SelectionBox();
            _connectionController = new ConnectionController();

            EditorApplication.update += OnEditorUpdate;

            if (_controller != null)
            {
                _currentData = _controller.Data;
                LoadFromController();
            }
            else
            {
                _currentData = new SerializableData();
                EnsureEntryStateExists();
            }

            _contextMenu.CreateStateRequested += OnCreateStateRequested;
            _contextMenu.CreateSubStateMachineRequested += OnCreateSubStateMachineRequested;
            _contextMenu.ConnectRequested += OnConnectRequested;
            _contextMenu.UngroupRequested += OnUngroupRequested;
            _contextMenu.CopyRequested += CopySelectedStates;
            _contextMenu.PasteRequested += PasteStates;
            _contextMenu.DeleteRequested += DeleteSelected;
            _selectionController.SelectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            _contextMenu.CreateStateRequested -= OnCreateStateRequested;
            _contextMenu.CreateSubStateMachineRequested -= OnCreateSubStateMachineRequested;
            _contextMenu.ConnectRequested -= OnConnectRequested;
            _contextMenu.UngroupRequested -= OnUngroupRequested;
            _contextMenu.CopyRequested -= CopySelectedStates;
            _contextMenu.PasteRequested -= PasteStates;
            _contextMenu.DeleteRequested -= DeleteSelected;
            _selectionController.SelectionChanged -= OnSelectionChanged;

            EditorApplication.update -= OnEditorUpdate;

            if (_controller != null && _hasUnsavedChanges && !_isLoading)
            {
                SaveCurrentData();
                _controller.Data = _controller.Data;
            }
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            _gridBackground = new GridBackground();
            _gridBackground.style.position = Position.Absolute;
            _gridBackground.style.left = 0f;
            _gridBackground.style.top = 0f;
            _gridBackground.style.right = 0f;
            _gridBackground.style.bottom = 0f;
            _gridBackground.style.overflow = Overflow.Hidden;
            _gridBackground.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            _gridBackground.pickingMode = PickingMode.Ignore;
            rootVisualElement.Add(_gridBackground);

            _groupContainer = new VisualElement();
            _groupContainer.style.position = Position.Absolute;
            _groupContainer.style.left = 0f;
            _groupContainer.style.top = 0f;
            _groupContainer.style.right = 0f;
            _groupContainer.style.bottom = 0f;
            _groupContainer.pickingMode = PickingMode.Ignore;
            _groupContainer.style.overflow = Overflow.Hidden;
            rootVisualElement.Add(_groupContainer);

            var groupStyleSheet = ScriptReferenceUtility.LoadStyleSheet("CommentGroupView");
            if (groupStyleSheet != null)
                _groupContainer.styleSheets.Add(groupStyleSheet);

            _connectionArrowsLayer = new ConnectionArrowsLayer(_connections, _connectionController);
            rootVisualElement.Add(_connectionArrowsLayer);

            _stateLayer = new VisualElement();
            _stateLayer.style.position = Position.Absolute;
            _stateLayer.style.left = 0f;
            _stateLayer.style.top = 0f;
            _stateLayer.style.right = 0f;
            _stateLayer.style.bottom = 0f;
            _stateLayer.pickingMode = PickingMode.Ignore;
            rootVisualElement.Add(_stateLayer);

            var stateStyleSheet = ScriptReferenceUtility.LoadStyleSheet("StateView");
            if (stateStyleSheet != null)
                rootVisualElement.styleSheets.Add(stateStyleSheet);

            _graphCanvas = new IMGUIContainer(OnGraphCanvasGUI);
            _graphCanvas.style.position = Position.Absolute;
            _graphCanvas.style.left = 0f;
            _graphCanvas.style.top = 0f;
            _graphCanvas.style.right = 0f;
            _graphCanvas.style.bottom = 0f;
            _graphCanvas.pickingMode = PickingMode.Ignore;
            rootVisualElement.Add(_graphCanvas);

            _breadcrumbBar = new VisualElement();
            _breadcrumbBar.AddToClassList("breadcrumb-bar");
            _breadcrumbBar.style.position = Position.Absolute;
            _breadcrumbBar.style.left = 0f;
            _breadcrumbBar.style.right = 0f;
            _breadcrumbBar.style.top = 0f;
            _breadcrumbBar.style.height = 24f;
            _breadcrumbBar.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            _breadcrumbBar.style.flexDirection = FlexDirection.Row;
            _breadcrumbBar.style.alignItems = Align.Center;
            _breadcrumbBar.style.paddingLeft = 8f;
            _breadcrumbBar.style.paddingRight = 8f;
            _breadcrumbBar.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            _breadcrumbBar.style.borderBottomWidth = 1f;
            _breadcrumbBar.pickingMode = PickingMode.Position;
            rootVisualElement.Add(_breadcrumbBar);

            rootVisualElement.Add(_selectionBox.Element);

            _sidePanelElement = new SidePanel(this);
            _sidePanelElement.style.position = Position.Absolute;
            _sidePanelElement.style.right = 0f;
            _sidePanelElement.style.top = 0f;
            _sidePanelElement.style.bottom = 0f;
            rootVisualElement.Add(_sidePanelElement);

            SyncGroupElements();
            rootVisualElement.schedule.Execute(() =>
            {
                _sidePanelElement?.SyncFromWindow();
                _sidePanelElement?.UpdateBlackboard();
                _sidePanelElement?.UpdateSelection();
                _gridBackground?.UpdateView(_panOffset, _zoom);
            }).StartingIn(10);
        }

        private void OnGUI()
        {
            if (position.width < 1f || position.height < 1f)
                return;

            if (_pendingController != null)
            {
                var pending = _pendingController;
                _pendingController = null;
                LoadController(pending);
            }

            var e = Event.current;

            float sideW = _showSidePanel ? _sidePanelWidth : CollapsedPanelWidth;
            const float breadcrumbH = 24f;
            Rect graphRect = new Rect(0f, breadcrumbH, position.width - sideW, position.height - breadcrumbH);

            _panController.HandleInput(graphRect, ref _panOffset, ref _zoom);

            UpdateConnectionOffsets();

            _lastMouseGraphPos = (e.mousePosition - _panOffset) / _zoom;

            if (!_connectionController.IsConnecting && _editingState == null)
                HandleKeyboardShortcuts(e);

            if (e.type == EventType.ContextClick && graphRect.Contains(e.mousePosition))
            {
                if (_editingState != null)
                    _editingState.CommitEditing();

                _connectionController.Cancel();
                Vector2 graphMousePosition = (e.mousePosition - _panOffset) / _zoom;
                ISelectable hit = HitTest(graphMousePosition);
                StateView hitState = hit as StateView;
                CommentGroupView hitGroup = hit as CommentGroupView;
                _contextMenu.Show(rootVisualElement, e.mousePosition, graphMousePosition, hitState, hitGroup,
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
                else if (_navigationPath.Count > 0 && _editingState == null)
                {
                    NavigateBack();
                    e.Use();
                    Repaint();
                }
            }

            _gridBackground.UpdateView(_panOffset, _zoom);
            _connectionArrowsLayer.UpdateView(_zoom, _panOffset);
            SyncStateHierarchy();
            UpdateStateTransforms();
            UpdateGroupPositions();
            _graphCanvas.MarkDirtyRepaint();

            if (_panController.IsPanning || _dragController.IsActive || _selectionBox.IsActive || _connectionController.IsConnecting)
                Repaint();
        }

        private void OnGraphCanvasGUI()
        {
            DrawSelectionOverlays();
            _selectionBox.DrawScreen(_zoom, _panOffset);

            if (Event.current.type != EventType.Repaint)
                return;

            UpdateResizeCursor();
        }



        private void HandleKeyboardShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Z && e.control)
            {
                if (_undoRedoSystem.Undo())
                {
                    MarkChanged();
                    SyncGroupElements();
                    _sidePanelElement?.UpdateBlackboard();
                    _sidePanelElement?.UpdateSelection();
                    Repaint();
                }
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Y && e.control)
            {
                if (_undoRedoSystem.Redo())
                {
                    MarkChanged();
                    SyncGroupElements();
                    _sidePanelElement?.UpdateBlackboard();
                    _sidePanelElement?.UpdateSelection();
                    Repaint();
                }
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

            if (e.keyCode == KeyCode.D && e.control)
            {
                DuplicateSelectedStates();
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

            if (e.keyCode == KeyCode.S && e.control)
            {
                if (_controller != null)
                {
                    SaveToController();
                    _controller.Save();
                    MarkSaved();
                }
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

                    if (target != null)
                    {
                        bool targetIsBlockedEntry = target.IsEntry && !target.IsSubEntry;
                        if (target == source || targetIsBlockedEntry)
                        {
                            _connectionController.Cancel();
                            e.Use();
                            Repaint();
                            break;
                        }

                        var cmd = new CompositeCommand("Create Connection");

                        if (source.IsEntry)
                        {
                            ConnectionView existing = GetEntryOutgoingConnection();
                            if (existing != null)
                                cmd.Add(new DeleteConnectionCommand(_connections, existing));
                        }

                        cmd.Add(new CreateConnectionCommand(_connections, new ConnectionView(source, target)));
                        _undoRedoSystem.Execute(cmd);
                        MarkChanged();
                    }
                    else
                    {
                        var newState = new StateView(graphMousePos - new Vector2(80f, 20f)) { DataIndex = _states.Count };
                        var cmd = new CompositeCommand("Create State and Connect");

                        if (source.IsEntry)
                        {
                            ConnectionView existing = GetEntryOutgoingConnection();
                            if (existing != null)
                                cmd.Add(new DeleteConnectionCommand(_connections, existing));
                        }

                        cmd.Add(new CreateStateCommand(_states, newState));
                        cmd.Add(new CreateConnectionCommand(_connections, new ConnectionView(source, newState)));
                        _undoRedoSystem.Execute(cmd);
                        MarkChanged();
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
            // Check for resize on selected group edges first
            if (_editingState == null && _editingGroup == null && !e.shift)
            {
                for (int i = _groups.Count - 1; i >= 0; i--)
                {
                    var group = _groups[i];

                    var edge = GetResizeEdge(group, graphPos);
                    if (edge != ResizeEdge.None)
                    {
                        _resizingGroup = group;
                        _resizeEdgeFlags = edge;
                        _resizeStartGraphPos = graphPos;
                        _resizeStartRect = group.GetGraphBounds();
                        e.Use();
                        return;
                    }
                }
            }

            ISelectable hit = HitTest(graphPos);

            if (hit is StateView sv)
            {
                long now = _clickStopwatch.ElapsedMilliseconds;
                long elapsed = now - _lastClickTimestamp;
                bool sameState = sv == _lastDoubleClickCandidate;

                if (sameState && elapsed < DoubleClickTimeMs)
                {
                    _lastDoubleClickCandidate = null;
                    if (!_selectionController.IsSelected(sv))
                        _selectionController.SelectOnly(sv);

                    if (sv.IsSubStateMachine)
                    {
                        EnterSubStateMachine(sv);
                        e.Use();
                        return;
                    }

                    StartEditing(sv);
                }

                if (!sameState)
                {
                    _lastClickTimestamp = now;
                    _lastDoubleClickCandidate = sv;
                }
                else
                {
                    _lastDoubleClickCandidate = null;
                }
            }
            else if (hit is CommentGroupView gv)
            {
                long now = _clickStopwatch.ElapsedMilliseconds;
                long elapsed = now - _lastClickTimestamp;
                bool sameGroup = gv == _lastDoubleClickCandidateGroup;

                if (sameGroup && elapsed < DoubleClickTimeMs)
                {
                    _lastDoubleClickCandidateGroup = null;
                    if (!_selectionController.IsSelected(gv))
                        _selectionController.SelectOnly(gv);
                    StartEditingGroup(gv);
                }

                if (!sameGroup)
                {
                    _lastClickTimestamp = now;
                    _lastDoubleClickCandidateGroup = gv;
                }
                else
                {
                    _lastDoubleClickCandidateGroup = null;
                }
            }
            else
            {
                _lastDoubleClickCandidate = null;
                _lastDoubleClickCandidateGroup = null;
            }

            if (_editingState != null && hit != _editingState)
            {
                _editingState.CommitEditing();
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

                    if (_editingState == null && _editingGroup == null)
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

                if (_editingState == null && _editingGroup == null)
                    _selectionBox.Start(graphPos);
            }

            if (!(_editingState != null && hit == _editingState))
            {
                e.Use();
            }
        }

        private void OnLeftMouseDrag(Vector2 graphPos, Event e)
        {
            if (_resizingGroup != null)
            {
                Vector2 delta = graphPos - _resizeStartGraphPos;
                Rect r = _resizeStartRect;

                if (_resizeEdgeFlags.HasFlag(ResizeEdge.Left))
                {
                    float newX = Mathf.Min(r.xMax - MinGroupWidth, r.x + delta.x);
                    r.xMin = newX;
                }
                if (_resizeEdgeFlags.HasFlag(ResizeEdge.Right))
                {
                    r.xMax = Mathf.Max(r.xMin + MinGroupWidth, r.xMax + delta.x);
                }
                if (_resizeEdgeFlags.HasFlag(ResizeEdge.Top))
                {
                    float newY = Mathf.Min(r.yMax - MinGroupHeight, r.y + delta.y);
                    r.yMin = newY;
                }
                if (_resizeEdgeFlags.HasFlag(ResizeEdge.Bottom))
                {
                    r.yMax = Mathf.Max(r.yMin + MinGroupHeight, r.yMax + delta.y);
                }

                _resizingGroup.SetRect(r);
                _lastDoubleClickCandidate = null;
                e.Use();
            }
            else if (_dragController.IsActive)
            {
                _dragController.UpdateDrag(graphPos, _zoom);
                if (_dragController.IsMoving)
                    _lastDoubleClickCandidate = null;
            }
            else if (_selectionBox.IsActive)
            {
                _selectionBox.Update(graphPos);
                PerformBoxSelection(_selectionBox.GetGraphRect(), e.shift);
                _lastDoubleClickCandidate = null;
            }

            e.Use();
        }

        private void OnLeftMouseUp(Vector2 graphPos, Event e)
        {
            if (_resizingGroup != null)
            {
                Rect newRect = _resizingGroup.GetGraphBounds();
                if (Mathf.Abs(newRect.x - _resizeStartRect.x) > 0.001f ||
                    Mathf.Abs(newRect.y - _resizeStartRect.y) > 0.001f ||
                    Mathf.Abs(newRect.width - _resizeStartRect.width) > 0.001f ||
                    Mathf.Abs(newRect.height - _resizeStartRect.height) > 0.001f)
                {
                    var cmd = new ResizeGroupCommand(_resizingGroup, _resizeStartRect, newRect);
                    _undoRedoSystem.Execute(cmd);
                    MarkChanged();
                }
                _resizingGroup = null;
                SyncStatesWithGroups();
                e.Use();
            }
            else if (_dragController.IsActive)
            {
                bool wasMoving = _dragController.IsMoving;
                _dragController.EndDrag();
                var moveCmd = CreateMoveCommandIfMoved();
                if (moveCmd != null)
                {
                    _undoRedoSystem.Execute(moveCmd);
                    MarkChanged();
                }
                _preDragPositions = null;
                if (wasMoving)
                    _lastDoubleClickCandidate = null;
                SyncStatesWithGroups();
                e.Use();
            }
            else if (_selectionBox.IsActive)
            {
                PerformBoxSelection(_selectionBox.GetGraphRect(), e.shift);
                _selectionBox.End();
                _lastDoubleClickCandidate = null;
                e.Use();
            }
        }

        private void PerformBoxSelection(Rect graphRect, bool shiftHeld)
        {
            if (!shiftHeld)
                _selectionController.Clear();

            var boxStates = new List<StateView>();
            for (int i = 0; i < _states.Count; i++)
                if (graphRect.Overlaps(_states[i].GetGraphBounds()))
                    boxStates.Add(_states[i]);
            _selectionController.SelectRange(boxStates);

            var boxConnections = new List<ConnectionView>();
            for (int i = 0; i < _connections.Count; i++)
                if (_connections[i].BoxOverlaps(graphRect))
                    boxConnections.Add(_connections[i]);
            _selectionController.SelectRange(boxConnections);

            var boxGroups = new List<CommentGroupView>();
            for (int i = 0; i < _groups.Count; i++)
                if (graphRect.Overlaps(_groups[i].GetGraphBounds()))
                    boxGroups.Add(_groups[i]);
            _selectionController.SelectRange(boxGroups);
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
                if (selected[i] is StateView s && groupMembers.Contains(s))
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

        private const float MinGroupWidth = 60f;
        private const float MinGroupHeight = 50f;

        private ResizeEdge GetResizeEdge(CommentGroupView group, Vector2 graphPos)
        {
            float threshold = ResizeHandleScreenSize / _zoom;
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

        private void UpdateResizeCursor()
        {
            float hs = ResizeHandleScreenSize;

            for (int i = 0; i < _groups.Count; i++)
            {
                var group = _groups[i];

                Rect r = group.GetGraphBounds();
                Vector2 sp = r.position * _zoom + _panOffset;
                Vector2 ss = r.size * _zoom;

                float edgeW = hs * 2f;
                float inset = hs;

                // Left edge (excluding corners)
                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x - hs, sp.y + inset, edgeW, ss.y - inset * 2f),
                    MouseCursor.ResizeHorizontal);
                // Right edge (excluding corners)
                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x + ss.x - hs, sp.y + inset, edgeW, ss.y - inset * 2f),
                    MouseCursor.ResizeHorizontal);

                // Top edge (excluding corners)
                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x + inset, sp.y - hs, ss.x - inset * 2f, edgeW),
                    MouseCursor.ResizeVertical);
                // Bottom edge (excluding corners)
                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x + inset, sp.y + ss.y - hs, ss.x - inset * 2f, edgeW),
                    MouseCursor.ResizeVertical);

                // Corners
                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x - hs, sp.y - hs, edgeW, edgeW),
                    MouseCursor.ResizeUpLeft);
                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x + ss.x - hs, sp.y - hs, edgeW, edgeW),
                    MouseCursor.ResizeUpRight);
                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x - hs, sp.y + ss.y - hs, edgeW, edgeW),
                    MouseCursor.ResizeUpRight);
                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x + ss.x - hs, sp.y + ss.y - hs, edgeW, edgeW),
                    MouseCursor.ResizeUpLeft);
            }
        }

        private void CreateGroupFromSelectedStates()
        {
            var selectedStates = new List<StateView>();
            for (int i = 0; i < _selectionController.Count; i++)
            {
                if (_selectionController.Selected[i] is StateView s && !s.IsEntry)
                    selectedStates.Add(s);
            }

            if (selectedStates.Count < 1) return;

            var group = new CommentGroupView(selectedStates, $"Group {_groups.Count + 1}");
            var cmd = new CreateGroupCommand(_groups, group);
            _undoRedoSystem.Execute(cmd);
            MarkChanged();
            SyncGroupElements();

            _selectionController.Clear();
            _selectionController.Select(group);
            SyncStatesWithGroups();
        }

        private void SyncStatesWithGroups()
        {
            for (int i = 0; i < _groups.Count; i++)
                _groups[i].SyncContainedStates(_states);
        }

        private void DrawSelectionOverlays()
        {
            var selected = _selectionController.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is StateView) continue;
                selected[i].DrawSelectionOverlay(_zoom, _panOffset);
            }
        }

        private void SyncStateHierarchy()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                var state = _states[i];
                if (state.parent == null)
                {
                    _stateLayer.Add(state);
                }
            }

            for (int i = _stateLayer.childCount - 1; i >= 0; i--)
            {
                var child = _stateLayer[i];
                if (child is StateView sv && !_states.Contains(sv))
                {
                    sv.RemoveFromHierarchy();
                }
            }
        }

        private void UpdateStateTransforms()
        {
            for (int i = 0; i < _states.Count; i++)
                _states[i].UpdateTransform(_zoom, _panOffset);
        }

        private void UpdateGroupPositions()
        {
            for (int i = 0; i < _groups.Count; i++)
                _groups[i].UpdateScreenPosition(_zoom, _panOffset);
        }

        private void SyncGroupElements()
        {
            if (_groupContainer == null) return;
            if (_editingGroup != null && !_groups.Contains(_editingGroup))
                _editingGroup = null;
            _groupContainer.Clear();
            for (int i = 0; i < _groups.Count; i++)
            {
                _groupContainer.Add(_groups[i]);
                _groups[i].UpdateScreenPosition(_zoom, _panOffset);
            }
        }

        private void UpdateConnectionOffsets()
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
                    list[i].PerpendicularOffset = count == 1 ? 0f : (i - (count - 1) * 0.5f) * 15f;
                }
            }
        }

        private void OnCreateStateRequested(Vector2 graphMousePosition)
        {
            var state = new StateView(graphMousePosition) { DataIndex = _states.Count };

            if (_entryState != null && GetEntryOutgoingConnection() == null)
            {
                var cmd = new CompositeCommand("Create State");
                cmd.Add(new CreateStateCommand(_states, state));
                cmd.Add(new CreateConnectionCommand(_connections, new ConnectionView(_entryState, state)));
                _undoRedoSystem.Execute(cmd);
            }
            else
            {
                var cmd = new CreateStateCommand(_states, state);
                _undoRedoSystem.Execute(cmd);
            }

            MarkChanged();
            SyncStatesWithGroups();
            Repaint();
        }

        private void OnCreateSubStateMachineRequested(Vector2 graphMousePosition)
        {
            var subData = new SerializableData();
            subData.States.Add(new StateData
            {
                Name = "Sub Entry",
                Position = new Vector2(50f, 200f),
                Size = new Vector2(StateView.DefaultWidth, StateView.DefaultHeight),
                IsEntry = true
            });

            var state = new StateView(graphMousePosition) { DataIndex = _states.Count };
            state.SubMachineData = subData;
            state.IsSubStateMachine = true;
            state.Name = "Sub State Machine";

            if (_entryState != null && GetEntryOutgoingConnection() == null)
            {
                var cmd = new CompositeCommand("Create Sub State Machine");
                cmd.Add(new CreateStateCommand(_states, state));
                cmd.Add(new CreateConnectionCommand(_connections, new ConnectionView(_entryState, state)));
                _undoRedoSystem.Execute(cmd);
            }
            else
            {
                var cmd = new CreateStateCommand(_states, state);
                _undoRedoSystem.Execute(cmd);
            }

            MarkChanged();
            SyncStatesWithGroups();
            Repaint();
        }

        private void OnConnectRequested(StateView source)
        {
            _connectionController.StartConnection(source);
            Repaint();
        }

        private ConnectionView GetEntryOutgoingConnection()
        {
            if (_entryState == null)
                return null;

            for (int i = 0; i < _connections.Count; i++)
            {
                if (_connections[i].From == _entryState)
                    return _connections[i];
            }

            return null;
        }

        private void OnUngroupRequested(CommentGroupView group)
        {
            _selectionController.Deselect(group);
            var cmd = new UngroupCommand(_groups, group);
            _undoRedoSystem.Execute(cmd);
            MarkChanged();
            SyncGroupElements();
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
                        size = s.Size,
                        behaviourScript = s.BehaviourScript,
                        behaviourInstance = s.BehaviourInstance,
                        subMachineData = s.SubMachineData,
                        isSubStateMachine = s.IsSubStateMachine
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
                    Size = data.size,
                    DataIndex = _states.Count,
                    BehaviourScript = data.behaviourScript,
                    SubMachineData = data.subMachineData,
                    IsSubStateMachine = data.isSubStateMachine
                };

                if (data.behaviourScript != null && data.behaviourInstance != null)
                {
                    var type = data.behaviourScript.GetClass();
                    if (type != null && type.IsSubclassOf(typeof(StateBehaviour)))
                    {
                        var instance = (StateBehaviour)ScriptableObject.CreateInstance(type);
                        EditorUtility.CopySerialized(data.behaviourInstance, instance);
                        instance.name = $"{state.Name}_Behaviour";
                        instance.hideFlags = HideFlags.HideInHierarchy;
                        state.BehaviourInstance = instance;
                    }
                }

                composite.Add(new CreateStateCommand(_states, state));
                pastedStates.Add(state);
            }

            _undoRedoSystem.Execute(composite);
            MarkChanged();
            SyncStatesWithGroups();

            for (int i = 0; i < pastedStates.Count; i++)
                _selectionController.Select(pastedStates[i]);

            Repaint();
        }

        private void DuplicateSelectedStates()
        {
            CopySelectedStates();
            if (_clipboard == null || _clipboard.Count == 0) return;

            for (int i = 0; i < _clipboard.Count; i++)
            {
                var d = _clipboard[i];
                d.position += new Vector2(20f, 20f);
            }

            PasteStates();
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
            MarkChanged();
            SyncGroupElements();

            _selectionController.Clear();
            Repaint();
        }

        private void StartEditing(StateView state)
        {
            if (_editingState != null && _editingState != state)
            {
                _editingState.CommitEditing();
            }

            _editingState = state;
            state.EditingCommitted += OnStateEditingCommitted;
            state.StartEditing();
        }

        private void OnStateEditingCommitted(StateView state, string oldName, string newName)
        {
            state.EditingCommitted -= OnStateEditingCommitted;

            if (_editingState != state)
                return;

            _editingState = null;

            if (oldName != newName && !string.IsNullOrEmpty(newName))
            {
                var cmd = new RenameStateCommand(state, oldName, newName);
                _undoRedoSystem.Execute(cmd);
                MarkChanged();
            }
        }

        private void StartEditingGroup(CommentGroupView group)
        {
            _editingGroup = group;
            group.EditingCommitted += OnGroupEditingCommitted;
            group.StartEditing();
        }

        private void OnGroupEditingCommitted(CommentGroupView group, string oldName, string newName)
        {
            group.EditingCommitted -= OnGroupEditingCommitted;
            _editingGroup = null;

            if (oldName != newName && !string.IsNullOrEmpty(newName))
            {
                var cmd = new RenameGroupCommand(group, oldName, newName);
                _undoRedoSystem.Execute(cmd);
                MarkChanged();
            }
        }

        private void CommitEditing()
        {
            if (_editingState != null)
                _editingState.CommitEditing();
        }

        private void CancelEditing()
        {
            if (_editingState != null)
                _editingState.CancelEditing();
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
                _entryState = new StateView(EntryStatePosition, "Entry", isEntry: true) { DataIndex = 0 };
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

            if (_entryState != null && _states.Count > 0 && _states[0] != _entryState)
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
                SyncGroupElements();
            }

            if (_sidePanelElement != null)
                _sidePanelElement.UpdateSelection();
        }

        private void UpdateTitle()
        {
            string name = _controller != null ? _controller.name : "CleanStateMachine";
            titleContent = new GUIContent(name);
        }

        private void MarkChanged()
        {
            if (_isLoading) return;
            _hasUnsavedChanges = true;
            hasUnsavedChanges = true;
            UpdateTitle();
        }

        private void MarkSaved()
        {
            _hasUnsavedChanges = false;
            hasUnsavedChanges = false;
            UpdateTitle();
        }

        // ─── Breadcrumb Navigation ──────────────────────────────────────

        private void UpdateBreadcrumb()
        {
            if (_breadcrumbBar == null) return;
            _breadcrumbBar.Clear();

            var rootLabel = new Label(_controller != null ? _controller.name : "Untitled");
            rootLabel.AddToClassList("breadcrumb-item");
            if (_navigationPath.Count > 0)
            {
                rootLabel.AddToClassList("breadcrumb-item--clickable");
                rootLabel.RegisterCallback<ClickEvent>(_ => NavigateToRoot());
            }
            _breadcrumbBar.Add(rootLabel);

            for (int i = 0; i < _navigationPath.Count; i++)
            {
                var sep = new Label("\u25B8");
                sep.AddToClassList("breadcrumb-separator");
                _breadcrumbBar.Add(sep);

                var stateLabel = new Label(_navigationPath[i].StateName);
                stateLabel.AddToClassList("breadcrumb-item");
                stateLabel.AddToClassList("breadcrumb-item--clickable");
                int capturedIndex = i;
                stateLabel.RegisterCallback<ClickEvent>(_ => NavigateToIndex(capturedIndex));

                _breadcrumbBar.Add(stateLabel);
            }
        }

        private void SaveCurrentData()
        {
            if (_currentData == null) return;
            _currentData.States.Clear();
            _currentData.Connections.Clear();
            _currentData.Groups.Clear();
            _currentData.BlackboardVariables.Clear();

            var stateToIndex = new Dictionary<StateView, int>();
            for (int i = 0; i < _states.Count; i++)
                stateToIndex[_states[i]] = i;

            foreach (var state in _states)
            {
                if (state.BehaviourInstance != null)
                    state.BehaviourInstance.name = $"{state.Name}_Behaviour";

                _currentData.States.Add(new StateData
                {
                    Name = state.Name,
                    Position = state.Position,
                    Size = state.Size,
                    IsEntry = state.IsEntry,
                    IsSubStateMachine = state.IsSubStateMachine,
                    BehaviourType = ScriptReferenceUtility.GetTypeName(state.BehaviourScript),
                    Behaviour = state.BehaviourInstance,
                    SubMachineData = state.SubMachineData
                });
            }

            foreach (var conn in _connections)
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
                    ToIndex = stateToIndex[conn.To]
                };
                for (int j = 0; j < conn.ConditionEntries.Count; j++)
                {
                    var entry = conn.ConditionEntries[j];
                    cd.Conditions.Add(new ConditionEntry
                    {
                        TypeName = ScriptReferenceUtility.GetTypeName(entry.Script),
                        Instance = entry.Instance
                    });
                }
                _currentData.Connections.Add(cd);
            }

            foreach (var group in _groups)
            {
                var gd = new GroupData { Label = group.Label, Color = group.GroupColor };
                foreach (var member in group.Members)
                    gd.MemberIndices.Add(stateToIndex[member]);
                _currentData.Groups.Add(gd);
            }

            foreach (var v in _blackboardVariables)
                _currentData.BlackboardVariables.Add(v.Clone());

            _currentData.PanOffset = _panOffset;
            _currentData.Zoom = _zoom;
            _currentData.ShowSidePanel = _showSidePanel;
            _currentData.SidePanelWidth = _sidePanelWidth;
            _currentData.DetailsHeightRatio = _detailsHeightRatio;
        }

        public void EnterSubStateMachine(StateView stateView)
        {
            if (stateView?.SubMachineData == null) return;

            SaveCurrentData();

            _navigationPath.Add(new NavigationFrame
            {
                ParentData = _currentData,
                EnteredData = stateView.SubMachineData,
                StateName = stateView.Name
            });

            _currentData = stateView.SubMachineData;
            LoadFromCurrentData();
            UpdateBreadcrumb();
        }

        public void NavigateBack()
        {
            if (_navigationPath.Count == 0) return;

            SaveCurrentData();
            _currentData = _navigationPath[_navigationPath.Count - 1].ParentData;
            _navigationPath.RemoveAt(_navigationPath.Count - 1);
            LoadFromCurrentData();
            UpdateBreadcrumb();
        }

        private void NavigateToRoot()
        {
            if (_navigationPath.Count == 0) return;

            SaveCurrentData();
            _currentData = _navigationPath[0].ParentData;
            _navigationPath.Clear();
            LoadFromCurrentData();
            UpdateBreadcrumb();
            UpdateTitle();
        }

        private void NavigateToIndex(int pathIndex)
        {
            if (pathIndex < 0 || pathIndex >= _navigationPath.Count) return;

            SaveCurrentData();
            _currentData = _navigationPath[pathIndex].EnteredData;
            _navigationPath.RemoveRange(pathIndex + 1, _navigationPath.Count - pathIndex - 1);
            LoadFromCurrentData();
            UpdateBreadcrumb();
            UpdateTitle();
        }

        // ─── Internal accessors for UITK SidePanel ─────────────────────

        internal bool GetShowSidePanel() => _showSidePanel;
        internal void SetShowSidePanel(bool value)
        {
            _showSidePanel = value;
            if (_sidePanelElement != null)
                _sidePanelElement.SetExpanded(value);
        }

        internal float GetSidePanelWidth() => _sidePanelWidth;
        internal void SetSidePanelWidth(float value)
        {
            _sidePanelWidth = value;
        }

        internal float GetDetailsHeightRatio() => _detailsHeightRatio;
        internal void SetDetailsHeightRatio(float value)
        {
            _detailsHeightRatio = value;
        }

        internal bool IsAtRootLevel => _navigationPath.Count == 0;
        internal SerializableData CurrentData => _currentData;

        internal UndoRedoSystem UndoRedoSystem => _undoRedoSystem;

        internal List<BlackboardVariable> GetBlackboardVariables() => _blackboardVariables;

        internal IReadOnlyList<ISelectable> GetSelection() => _selectionController.Selected;

        internal List<StateView> GetStates() => _states;

        internal List<ConnectionView> GetConnections() => _connections;

        internal void NotifySidePanelChanged()
        {
            MarkChanged();
            Repaint();
        }

        internal void OnSaveCommand()
        {
            if (_controller != null)
            {
                SaveToController();
                _controller.Save();
                MarkSaved();
            }
            Repaint();
        }

        internal void SaveSidePanelLayout(float detailsHeightRatio)
        {
            _detailsHeightRatio = detailsHeightRatio;
        }

        public override void SaveChanges()
        {
            base.SaveChanges();
            if (_controller != null)
            {
                SaveToController();
                _controller.Save();
                MarkSaved();
            }
        }

        public void LoadController(StateMachineController controller)
        {
            if (controller == null) return;
            if (controller == _controller) return;

            if (_hasUnsavedChanges && _controller != null)
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "Unsaved Changes",
                    $"Save changes to {_controller.name} before switching?",
                    "Save", "Cancel", "Discard");

                if (option == 1) return;

                if (option == 0)
                {
                    SaveToController();
                    _controller.Save();
                }
            }

            _navigationPath.Clear();
            _controller = controller;
            LoadFromController();
            UpdateBreadcrumb();
        }

        private void LoadFromController()
        {
            _currentData = _controller != null ? _controller.Data : new SerializableData();
            LoadFromCurrentData();
        }

        private void LoadFromCurrentData()
        {
            _isLoading = true;

            _editingState = null;
            _selectionController.Clear();
            _states.Clear();
            _connections.Clear();
            _groups.Clear();
            _blackboardVariables.Clear();
            _undoRedoSystem = new UndoRedoSystem();
            ClearActiveStates();
            _trackedComponent = null;

            if (_currentData != null)
            {
                var data = _currentData;

                var stateLookup = new List<StateView>();
                for (int i = 0; i < data.States.Count; i++)
                {
                    var sd = data.States[i];
                    bool isSubEntry = sd.IsEntry && _navigationPath.Count > 0;
                    var state = new StateView(sd.Position, sd.Name, sd.IsEntry, isSubEntry)
                    {
                        Size = sd.Size,
                        BehaviourScript = ScriptReferenceUtility.FindScriptByTypeName(sd.BehaviourType),
                        BehaviourInstance = sd.Behaviour,
                        SubMachineData = sd.SubMachineData,
                        IsSubStateMachine = sd.IsSubStateMachine,
                        DataIndex = i
                    };
                    _states.Add(state);
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
                            DataIndex = i
                        };
                        if (cd.Conditions != null)
                        {
                            for (int j = 0; j < cd.Conditions.Count; j++)
                            {
                                var ce = cd.Conditions[j];
                                conn.ConditionEntries.Add(new ConditionEntryView
                                {
                                    Script = ScriptReferenceUtility.FindScriptByTypeName(ce.TypeName),
                                    Instance = ce.Instance
                                });
                            }
                        }
                        _connections.Add(conn);
                    }
                }

                for (int i = 0; i < data.Groups.Count; i++)
                {
                    var gd = data.Groups[i];
                    var members = new List<StateView>();
                    foreach (int mi in gd.MemberIndices)
                    {
                        if (mi >= 0 && mi < stateLookup.Count && !stateLookup[mi].IsEntry)
                            members.Add(stateLookup[mi]);
                    }
                    var group = new CommentGroupView(members, gd.Label);
                    group.GroupColor = gd.Color;
                    _groups.Add(group);
                }

                for (int i = 0; i < data.BlackboardVariables.Count; i++)
                    _blackboardVariables.Add(data.BlackboardVariables[i].Clone());

                _panOffset = data.PanOffset;
                _zoom = data.Zoom;
                _showSidePanel = data.ShowSidePanel;
                _sidePanelWidth = data.SidePanelWidth;
                _detailsHeightRatio = data.DetailsHeightRatio;
            }

            EnsureEntryStateExists();
            SyncGroupElements();
            _isLoading = false;
            MarkSaved();
            UpdateTitle();
            UpdateBreadcrumb();

            if (_sidePanelElement != null)
            {
                _sidePanelElement.SyncFromWindow();
                _sidePanelElement.UpdateVisibility();
                _sidePanelElement.UpdateSelection();
                _sidePanelElement.UpdateBlackboard();
            }

            Repaint();
        }

        private void SaveToController()
        {
            if (_controller == null) return;
            SaveCurrentData();
            _controller.Data = _controller.Data;
        }

        private void SaveAs()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save State Machine Controller",
                "NewStateMachineController",
                "asset",
                "Save state machine controller as...");

            if (string.IsNullOrEmpty(path)) return;

            var controller = CreateInstance<StateMachineController>();
            AssetDatabase.CreateAsset(controller, path);

            _controller = controller;
            SaveToController();
            _controller.Save();
            MarkSaved();
            Repaint();

            EditorGUIUtility.PingObject(controller);
        }

        private void NewFile()
        {
            if (_hasUnsavedChanges)
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "Unsaved Changes",
                    "You have unsaved changes. What do you want to do?",
                    "Save", "Cancel", "Discard");

                if (option == 1) return;

                if (option == 0)
                {
                    if (_controller != null)
                    {
                        SaveToController();
                        _controller.Save();
                    }
                    else
                    {
                        SaveAs();
                        return;
                    }
                }
            }

            _controller = null;
            _currentData = new SerializableData();
            _navigationPath.Clear();
            _editingState = null;
            _selectionController.Clear();
            _states.Clear();
            _connections.Clear();
            _groups.Clear();
            _blackboardVariables.Clear();
            _undoRedoSystem = new UndoRedoSystem();
            ClearActiveStates();
            _panOffset = Vector2.zero;
            _zoom = 1f;
            _showSidePanel = true;
            _sidePanelWidth = 220f;
            _detailsHeightRatio = 0.5f;

            EnsureEntryStateExists();
            SyncGroupElements();

            MarkSaved();

            if (_sidePanelElement != null)
            {
                _sidePanelElement.UpdateVisibility();
                _sidePanelElement.UpdateSelection();
                _sidePanelElement.UpdateBlackboard();
            }

            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (!Application.isPlaying)
            {
                if (_activeStateLevelIndex >= 0)
                {
                    ClearActiveStates();
                    Repaint();
                }
                return;
            }

            UpdateTrackedComponent();

            if (_trackedComponent != null)
            {
                bool navigated = AutoNavigateToActiveDepth();

                int viewDepth = _navigationPath.Count;
                int runtimeDepth = _trackedComponent.ActiveStateDepth;

                if (navigated)
                {
                    _activeStateLevelIndex = -1;
                    _activeStateDepth = -1;
                }

                if (viewDepth < runtimeDepth)
                {
                    int newActiveIndex = _trackedComponent.GetStateIndexAtDepth(viewDepth);

                    if (newActiveIndex != _activeStateLevelIndex || viewDepth != _activeStateDepth)
                    {
                        _activeStateLevelIndex = newActiveIndex;
                        _activeStateDepth = viewDepth;

                        for (int i = 0; i < _states.Count; i++)
                            _states[i].IsActive = (_states[i].DataIndex == _activeStateLevelIndex);

                        Repaint();
                    }
                }
                else
                {
                    if (_activeStateLevelIndex >= 0)
                    {
                        _activeStateLevelIndex = -1;
                        _activeStateDepth = viewDepth;
                        for (int i = 0; i < _states.Count; i++)
                            _states[i].IsActive = false;

                        Repaint();
                    }
                }

                var transitions = _trackedComponent.RecentTransitions;
                if (transitions.Count > 0)
                {
                    for (int t = 0; t < transitions.Count; t++)
                    {
                        var record = transitions[t];
                        if (record.Level != viewDepth) continue;

                        for (int c = 0; c < _connections.Count; c++)
                        {
                            if (record.ConnectionIndex >= 0)
                            {
                                if (_connections[c].DataIndex == record.ConnectionIndex)
                                {
                                    _connections[c].IsActive = true;
                                    _connections[c].ActivationTime = Time.realtimeSinceStartup;
                                }
                            }
                            else
                            {
                                if (_connections[c].From.DataIndex == record.FromIndex &&
                                    _connections[c].To.DataIndex == record.ToIndex)
                                {
                                    _connections[c].IsActive = true;
                                    _connections[c].ActivationTime = Time.realtimeSinceStartup;
                                }
                            }
                        }
                    }
                    transitions.Clear();
                    Repaint();
                }

                Repaint();
            }
            else if (_activeStateLevelIndex >= 0)
            {
                ClearActiveStates();
                Repaint();
            }
        }

        private bool AutoNavigateToActiveDepth()
        {
            if (_trackedComponent == null) return false;
            if (_isLoading || _isAutoNavigating) return false;

            _isAutoNavigating = true;
            bool changed = false;

            int runtimeDepth = _trackedComponent.ActiveStateDepth;

            while (true)
            {
                int viewDepth = _navigationPath.Count;

                if (viewDepth + 1 < runtimeDepth)
                {
                    int activeIndex = _trackedComponent.GetStateIndexAtDepth(viewDepth);
                    StateView subMachineView = null;
                    for (int i = 0; i < _states.Count; i++)
                    {
                        if (_states[i].DataIndex == activeIndex && _states[i].IsSubStateMachine
                            && _states[i].SubMachineData != null)
                        {
                            subMachineView = _states[i];
                            break;
                        }
                    }
                    if (subMachineView == null) break;

                    bool alreadyViewing = _navigationPath.Count > 0
                        && _navigationPath[^1].EnteredData == subMachineView.SubMachineData;
                    if (alreadyViewing) break;

                    EnterSubStateMachine(subMachineView);
                    changed = true;
                }
                else if (viewDepth > 0 && viewDepth >= runtimeDepth)
                {
                    NavigateBack();
                    changed = true;
                }
                else
                {
                    break;
                }
            }

            _isAutoNavigating = false;
            return changed;
        }

        private void UpdateTrackedComponent()
        {
            if (!Application.isPlaying)
            {
                _trackedComponent = null;
                return;
            }

            if (_controller == null)
            {
                _trackedComponent = null;
                return;
            }

            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                _trackedComponent = null;
                return;
            }

            var component = selected.GetComponent<StateMachineComponent>();
            if (component != null && component.Controller == _controller)
            {
                _trackedComponent = component;
            }
            else
            {
                _trackedComponent = null;
            }
        }

        private void ClearActiveStates()
        {
            _activeStateLevelIndex = -1;
            _activeStateDepth = -1;
            for (int i = 0; i < _states.Count; i++)
                _states[i].IsActive = false;
            for (int i = 0; i < _connections.Count; i++)
                _connections[i].IsActive = false;
        }
    }
}
