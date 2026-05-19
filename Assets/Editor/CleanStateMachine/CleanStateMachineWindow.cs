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

        [SerializeField] private Vector2 _panOffset;
        [SerializeField] private float _zoom = 1f;

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

        private void OnEnable()
        {
            wantsMouseMove = true;
            _graphView = new GraphView();
            _panController = new GraphPanController();
            _contextMenu = new GraphContextMenu();
            _selectionController = new SelectionController();
            _dragController = new DragController();
            _selectionBox = new SelectionBox();
            _connectionController = new ConnectionController();

            _contextMenu.CreateStateRequested += OnCreateStateRequested;
            _contextMenu.ConnectRequested += OnConnectRequested;
            _contextMenu.UngroupRequested += OnUngroupRequested;
            _connectionController.ConnectionCompleted += OnConnectionCompleted;
        }

        private void OnDisable()
        {
            _contextMenu.CreateStateRequested -= OnCreateStateRequested;
            _contextMenu.ConnectRequested -= OnConnectRequested;
            _contextMenu.UngroupRequested -= OnUngroupRequested;
            _connectionController.ConnectionCompleted -= OnConnectionCompleted;
        }

        private void OnGUI()
        {
            if (position.width < 1f || position.height < 1f)
                return;

            var rect = new Rect(0f, 0f, position.width, position.height);
            var e = Event.current;

            _panController.HandleInput(rect, ref _panOffset, ref _zoom);

            HandleKeyboardShortcuts(e);

            if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
            {
                _connectionController.Cancel();
                Vector2 graphMousePosition = (e.mousePosition - _panOffset) / _zoom;
                ISelectable hit = HitTest(graphMousePosition);
                StateView hitState = hit as StateView;
                CommentGroupView hitGroup = hit as CommentGroupView;
                _contextMenu.Show(graphMousePosition, hitState, hitGroup);
                e.Use();
            }

            if (_connectionController.IsConnecting)
            {
                HandleConnectingInput(rect);
            }
            else
            {
                HandleLeftClickInteraction(rect);
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (_connectionController.IsConnecting)
                {
                    _connectionController.Cancel();
                    e.Use();
                    Repaint();
                }
            }

            _graphView.Draw(rect, _panOffset, _zoom);
            DrawGroups();
            DrawConnections();
            DrawStates();
            DrawSelectionOverlays();
            _connectionController.DrawPending(_zoom, _panOffset);
            _selectionBox.DrawScreen(_zoom, _panOffset);

            if (_panController.IsPanning || _dragController.IsActive || _selectionBox.IsActive || _connectionController.IsConnecting)
                Repaint();
        }

        private void HandleKeyboardShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.G && e.control)
            {
                CreateGroupFromSelectedStates();
                e.Use();
                Repaint();
            }

            if (e.keyCode is KeyCode.Delete or KeyCode.Backspace)
            {
                DeleteSelectedGroups();
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
                    if (!_connectionController.TryComplete(graphMousePos, _states))
                    {
                        _connectionController.Cancel();
                    }
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

                _dragController.StartDrag(graphPos, GetDragItems());
            }
            else
            {
                if (!e.shift)
                    _selectionController.Clear();

                _selectionBox.Start(graphPos);
            }

            e.Use();
        }

        private void OnLeftMouseDrag(Vector2 graphPos, Event e)
        {
            if (_dragController.IsActive)
            {
                _dragController.UpdateDrag(graphPos, _zoom);
            }
            else if (_selectionBox.IsActive)
            {
                _selectionBox.Update(graphPos);
            }

            e.Use();
        }

        private void OnLeftMouseUp(Vector2 graphPos, Event e)
        {
            if (_dragController.IsActive)
            {
                _dragController.EndDrag();
            }
            else if (_selectionBox.IsActive)
            {
                if (!e.shift)
                    _selectionController.Clear();

                Rect r = _selectionBox.GetGraphRect();
                for (int i = 0; i < _states.Count; i++)
                    if (r.Overlaps(_states[i].GetGraphBounds()))
                        _selectionController.Select(_states[i]);

                for (int i = 0; i < _groups.Count; i++)
                    if (r.Overlaps(_groups[i].GetGraphBounds()))
                        _selectionController.Select(_groups[i]);

                _selectionBox.End();
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

            if (groupMembers.Count == 0) return selected;

            for (int i = selected.Count - 1; i >= 0; i--)
            {
                if (selected[i] is StateView s && groupMembers.Contains(s))
                    selected.RemoveAt(i);
            }

            return selected;
        }

        private ISelectable HitTest(Vector2 graphPos)
        {
            for (int i = _states.Count - 1; i >= 0; i--)
            {
                if (_states[i].ContainsPoint(graphPos))
                    return _states[i];
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
            _groups.Add(group);

            _selectionController.Clear();
            _selectionController.Select(group);
        }

        private void DeleteSelectedGroups()
        {
            bool removed = false;
            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                if (_groups[i].IsSelected)
                {
                    _selectionController.Deselect(_groups[i]);
                    _groups.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed) Repaint();
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
            for (int i = 0; i < _connections.Count; i++)
            {
                _connections[i].Draw(_zoom, _panOffset);
            }
        }

        private void OnCreateStateRequested(Vector2 graphMousePosition)
        {
            var state = new StateView(graphMousePosition);
            _states.Add(state);
            Repaint();
        }

        private void OnConnectRequested(StateView source)
        {
            _connectionController.StartConnection(source);
            Repaint();
        }

        private void OnConnectionCompleted(StateView from, StateView to)
        {
            _connections.Add(new ConnectionView(from, to));
            Repaint();
        }

        private void OnUngroupRequested(CommentGroupView group)
        {
            _selectionController.Deselect(group);
            _groups.Remove(group);
            Repaint();
        }
    }
}
