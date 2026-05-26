using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    [System.Flags]
    internal enum ResizeEdge
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
    }

    [System.Serializable]
    internal class CopiedStateData
    {
        public int sourceDataIndex;
        public Vector2 position;
        public string name;
        public Vector2 size;
        public List<BehaviourEntry> behaviourEntries;
        public List<int> childIndices;
        public bool isSubStateMachine;
        public bool isExternalReference;
        public bool isAnyState;
        public ExternalStateMachineAction externalAction;
        public GameObject externalStateMachine;
        public string externalTargetStateName;
        public string externalBlackboardParmName;
        public BlackboardVariableType externalBlackboardParmType;
        public string externalBlackboardParmValue;
    }

    internal class CopiedConnectionData
    {
        public int fromSourceIndex;
        public int toSourceIndex;
        public List<ConditionEntry> conditionEntries;
    }

    public class CleanStateMachineWindow : EditorWindow
    {
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

        // ─── Serialized & Internal State ──────────────────────────────

        [SerializeField] private Vector2 _panOffset;
        [SerializeField] private float _zoom = 1f;
        [SerializeField] private StateMachineController _controller;
        private bool _showSidePanel = true;
        private float _sidePanelWidth = 220f;
        private float _detailsHeightRatio = 0.5f;

        internal Vector2 PanOffset { get => _panOffset; set => _panOffset = value; }
        internal float Zoom { get => _zoom; set => _zoom = value; }
        internal bool ShowSidePanel { get => _showSidePanel; set => _showSidePanel = value; }
        internal float SidePanelWidth { get => _sidePanelWidth; set => _sidePanelWidth = value; }
        internal float DetailsHeightRatio { get => _detailsHeightRatio; set => _detailsHeightRatio = value; }
        internal StateMachineController Controller { get => _controller; set => _controller = value; }

        internal SerializableData CurrentData;

        internal bool IsLoading
        {
            get => _isLoading;
            set => _isLoading = value;
        }

        internal bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                _hasUnsavedChanges = value;
                hasUnsavedChanges = value;
            }
        }

        internal List<CopiedStateData> Clipboard;
        internal List<CopiedConnectionData> CopiedConnections;

        internal readonly List<StateView> States = new();
        internal readonly List<ConnectionView> Connections = new();
        internal readonly List<CommentGroupView> Groups = new();
        internal List<BlackboardVariable> BlackboardVariables = new();

        internal readonly List<int> ExpandedSubStateStack = new();

        internal StateView EntryState;
        internal StateView EditingState;
        internal CommentGroupView EditingGroup;

        internal CommentGroupView ResizingGroup;
        internal ResizeEdge ResizeEdgeFlags;
        internal Vector2 ResizeStartGraphPos;
        internal Rect ResizeStartRect;

        internal int ActiveStateIndex = -1;
        internal StateMachineComponent TrackedComponent;
        internal bool IsAutoNavigating;
        internal bool WasPlaying;

        internal List<int> PendingExpandStack;
        internal double PendingExpandTime;

        internal int LastTransitionFromIndex = -1;
        internal int LastTransitionToIndex = -1;
        internal int LastTransitionConnectionIndex = -1;

        internal HashSet<int> BreakpointStateIndices = new HashSet<int>();
        internal HashSet<int> TriggeredBreakpointIndices = new HashSet<int>();

        internal bool IsAnimatingView;
        internal Vector2 AnimFromPan;
        internal Vector2 AnimToPan;
        internal float AnimFromZoom;
        internal float AnimToZoom;
        internal double AnimStartTime;

        internal long LastClickTimestamp;
        internal StateView LastDoubleClickCandidate;
        internal CommentGroupView LastDoubleClickCandidateGroup;

        internal Vector2 LastMouseGraphPos;

        // ─── Controllers ──────────────────────────────────────────────

        internal SelectionController SelectionController;
        internal UndoRedoSystem UndoRedoSystem;
        internal GraphPanController PanController;
        internal GraphContextMenu ContextMenu;
        internal DragController DragController;
        internal SelectionBox SelectionBox;
        internal ConnectionController ConnectionController;

        // ─── Helper Modules ───────────────────────────────────────────

        internal GraphOperations GraphOperations;
        internal GraphValidation GraphValidation;
        internal GraphInputHandler InputHandler;
        internal ShortcutGuide ShortcutGuide;
        internal GraphSearchPanel SearchPanel;
        internal ExpandedViewManager ExpandedView;
        internal GraphSerializer GraphSerializer;
        internal PlayModeTracker PlayModeTracker;
        internal GraphViewAnimator ViewAnimator;

        // ─── UI Elements ──────────────────────────────────────────────

        internal GridBackground GridBackground;
        internal ConnectionArrowsLayer ConnectionArrowsLayer;
        internal GraphPreview GraphPreview;
        internal VisualElement StateLayer;
        internal VisualElement GroupContainer;
        internal IMGUIContainer GraphCanvas;
        internal SidePanel SidePanelElement;
        internal VisualElement ExpandedModeBar;
        internal Label ExpandedModeLabel;
        internal VisualElement BreadcrumbContainer;
        private VisualElement _searchButton;

        // ─── Private Helpers ──────────────────────────────────────────

        internal StateMachineController _pendingController;
        private bool _pendingFocusOnContent;
        private bool _hasUnsavedChanges;
        private bool _isLoading;

        internal static readonly Stopwatch ClickStopwatch = Stopwatch.StartNew();
        internal static readonly Vector2 EntryStatePosition = new Vector2(50f, 200f);
        internal const float CollapsedPanelWidth = 35f;
        internal const float ResizeHandleScreenSize = 8f;
        internal const int DoubleClickTimeMs = 300;
        internal const float MinGroupWidth = 60f;
        internal const float MinGroupHeight = 50f;
        internal const float AnimDuration = 0.35f;
        internal const float AutoExpandDelay = 0.35f;

        // ─── Window Lifecycle ────────────────────────────────────────

        private static class LayoutPrefs
        {
            public const string ShowSidePanel = "CleanStateMachine.ShowSidePanel";
            public const string SidePanelWidth = "CleanStateMachine.SidePanelWidth";
            public const string DetailsHeightRatio = "CleanStateMachine.DetailsHeightRatio";

            public static void Load(CleanStateMachineWindow w)
            {
                w._showSidePanel = EditorPrefs.GetBool(ShowSidePanel, true);
                w._sidePanelWidth = EditorPrefs.GetFloat(SidePanelWidth, 220f);
                w._detailsHeightRatio = EditorPrefs.GetFloat(DetailsHeightRatio, 0.5f);
            }

            public static void Save(CleanStateMachineWindow w)
            {
                EditorPrefs.SetBool(ShowSidePanel, w._showSidePanel);
                EditorPrefs.SetFloat(SidePanelWidth, w._sidePanelWidth);
                EditorPrefs.SetFloat(DetailsHeightRatio, w._detailsHeightRatio);
            }
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            LayoutPrefs.Load(this);
            UndoRedoSystem = new UndoRedoSystem();
            PanController = new GraphPanController();
            ContextMenu = new GraphContextMenu();
            SelectionController = new SelectionController();
            DragController = new DragController();
            SelectionBox = new SelectionBox();
            ConnectionController = new ConnectionController();

            GraphOperations = new GraphOperations(this);
            GraphValidation = new GraphValidation(this);
            InputHandler = new GraphInputHandler(this, GraphOperations);
            ShortcutGuide = new ShortcutGuide(this);
            SearchPanel = new GraphSearchPanel(this);
            ExpandedView = new ExpandedViewManager(this);
            GraphSerializer = new GraphSerializer(this);
            PlayModeTracker = new PlayModeTracker(this, ExpandedView);
            ViewAnimator = new GraphViewAnimator(this);

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            PlayModeTracker.SubscribeToGlobalEvents();

            if (_controller != null)
            {
                CurrentData = _controller.Data;
                GraphSerializer.LoadFromController();
                _pendingFocusOnContent = true;
            }
            else
            {
                CurrentData = new SerializableData();
                GraphOperations.EnsureEntryStateExists();
            }

            ContextMenu.CreateStateRequested += OnCreateStateRequested;
            ContextMenu.CreateSubStateMachineRequested += OnCreateSubStateMachineRequested;
            ContextMenu.CreateExternalReferenceRequested += OnCreateExternalReferenceRequested;
            ContextMenu.CreateAnyStateRequested += OnCreateAnyStateRequested;
            ContextMenu.ConnectRequested += OnConnectRequested;
            ContextMenu.UngroupRequested += OnUngroupRequested;
            ContextMenu.CopyRequested += CopySelectedStates;
            ContextMenu.PasteRequested += PasteStates;
            ContextMenu.DeleteRequested += DeleteSelected;
            ContextMenu.ToggleBreakpointRequested += OnToggleBreakpointRequested;
            SelectionController.SelectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            LayoutPrefs.Save(this);
            ContextMenu.CreateStateRequested -= OnCreateStateRequested;
            ContextMenu.CreateSubStateMachineRequested -= OnCreateSubStateMachineRequested;
            ContextMenu.CreateExternalReferenceRequested -= OnCreateExternalReferenceRequested;
            ContextMenu.CreateAnyStateRequested -= OnCreateAnyStateRequested;
            ContextMenu.ConnectRequested -= OnConnectRequested;
            ContextMenu.UngroupRequested -= OnUngroupRequested;
            ContextMenu.CopyRequested -= CopySelectedStates;
            ContextMenu.PasteRequested -= PasteStates;
            ContextMenu.DeleteRequested -= DeleteSelected;
            ContextMenu.ToggleBreakpointRequested -= OnToggleBreakpointRequested;
            SelectionController.SelectionChanged -= OnSelectionChanged;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            PlayModeTracker.UnsubscribeFromGlobalEvents();

            if (CurrentData != null && ExpandedSubStateStack.Count > 0)
            {
                var indexToView = new Dictionary<int, int>();
                for (int i = 0; i < States.Count; i++)
                    indexToView[States[i].DataIndex] = i;

                CurrentData.ExpandedSubStateIndices.Clear();
                foreach (int dataIdx in ExpandedSubStateStack)
                {
                    if (indexToView.TryGetValue(dataIdx, out int si))
                        CurrentData.ExpandedSubStateIndices.Add(si);
                }
            }

            if (_controller != null && !_isLoading)
            {
                if (_hasUnsavedChanges || Application.isPlaying)
                {
                    GraphSerializer.SaveCurrentData();
                    _controller.Data = _controller.Data;
                }
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            PlayModeTracker.OnPlayModeStateChanged(change);
        }

        private void OnSelectionChange()
        {
            var active = Selection.activeGameObject;
            if (active == null) return;

            var component = active.GetComponent<StateMachineComponent>();
            if (component == null || component.Controller == null) return;
            if (component.Controller == _controller) return;

            if (GraphSerializer != null)
                GraphSerializer.LoadController(component.Controller);
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            GridBackground = new GridBackground();
            GridBackground.style.position = Position.Absolute;
            GridBackground.style.left = 0f;
            GridBackground.style.top = 0f;
            GridBackground.style.right = 0f;
            GridBackground.style.bottom = 0f;
            GridBackground.style.overflow = Overflow.Hidden;
            GridBackground.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            GridBackground.pickingMode = PickingMode.Ignore;
            rootVisualElement.Add(GridBackground);

            GroupContainer = new VisualElement();
            GroupContainer.style.position = Position.Absolute;
            GroupContainer.style.left = 0f;
            GroupContainer.style.top = 0f;
            GroupContainer.style.right = 0f;
            GroupContainer.style.bottom = 0f;
            GroupContainer.pickingMode = PickingMode.Ignore;
            GroupContainer.style.overflow = Overflow.Hidden;
            rootVisualElement.Add(GroupContainer);

            var groupStyleSheet = ScriptReferenceUtility.LoadStyleSheet("CommentGroupView");
            if (groupStyleSheet != null)
                GroupContainer.styleSheets.Add(groupStyleSheet);

            ConnectionArrowsLayer = new ConnectionArrowsLayer(Connections, ConnectionController);
            ConnectionArrowsLayer.IsConnectionHidden = conn => !ExpandedView.IsConnectionVisible(conn);
            rootVisualElement.Add(ConnectionArrowsLayer);

            StateLayer = new VisualElement();
            StateLayer.style.position = Position.Absolute;
            StateLayer.style.left = 0f;
            StateLayer.style.top = 0f;
            StateLayer.style.right = 0f;
            StateLayer.style.bottom = 0f;
            StateLayer.pickingMode = PickingMode.Ignore;
            rootVisualElement.Add(StateLayer);

            var stateStyleSheet = ScriptReferenceUtility.LoadStyleSheet("StateView");
            if (stateStyleSheet != null)
                rootVisualElement.styleSheets.Add(stateStyleSheet);

            GraphCanvas = new IMGUIContainer(OnGraphCanvasGUI);
            GraphCanvas.style.position = Position.Absolute;
            GraphCanvas.style.left = 0f;
            GraphCanvas.style.top = 0f;
            GraphCanvas.style.right = 0f;
            GraphCanvas.style.bottom = 0f;
            GraphCanvas.pickingMode = PickingMode.Ignore;
            rootVisualElement.Add(GraphCanvas);

            ExpandedModeBar = new VisualElement();
            ExpandedModeBar.style.position = Position.Absolute;
            ExpandedModeBar.style.left = 0f;
            ExpandedModeBar.style.right = 0f;
            ExpandedModeBar.style.top = 0f;
            ExpandedModeBar.style.height = 24f;
            ExpandedModeBar.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            ExpandedModeBar.style.flexDirection = FlexDirection.Row;
            ExpandedModeBar.style.alignItems = Align.Center;
            ExpandedModeBar.style.paddingLeft = 8f;
            ExpandedModeBar.style.paddingRight = 8f;
            ExpandedModeBar.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            ExpandedModeBar.style.borderBottomWidth = 1f;
            ExpandedModeBar.style.justifyContent = Justify.SpaceBetween;
            ExpandedModeBar.style.display = DisplayStyle.Flex;
            ExpandedModeBar.pickingMode = PickingMode.Position;
            rootVisualElement.Add(ExpandedModeBar);

            BreadcrumbContainer = new VisualElement();
            BreadcrumbContainer.style.flexDirection = FlexDirection.Row;
            BreadcrumbContainer.style.alignItems = Align.Center;
            BreadcrumbContainer.style.flexGrow = 1f;
            BreadcrumbContainer.style.overflow = Overflow.Hidden;
            ExpandedModeBar.Add(BreadcrumbContainer);

            rootVisualElement.Add(SelectionBox.Element);

            GraphPreview = new GraphPreview();
            GraphPreview.AddToClassList("graph-preview");
            var previewStyleSheet = ScriptReferenceUtility.LoadStyleSheet("GraphPreview");
            if (previewStyleSheet != null)
                rootVisualElement.styleSheets.Add(previewStyleSheet);
            rootVisualElement.Add(GraphPreview);

            SidePanelElement = new SidePanel(this);
            SidePanelElement.style.position = Position.Absolute;
            SidePanelElement.style.right = 0f;
            SidePanelElement.style.top = 0f;
            SidePanelElement.style.bottom = 0f;
            rootVisualElement.Add(SidePanelElement);

            _searchButton = new Button(() => SearchPanel.Show());
            _searchButton.text = "\U0001F50D";
            _searchButton.style.position = Position.Absolute;
            _searchButton.style.top = 32f;
            _searchButton.style.width = 32f;
            _searchButton.style.height = 32f;
            _searchButton.style.fontSize = 16f;
            _searchButton.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.85f);
            _searchButton.style.color = new Color(0.8f, 0.8f, 0.8f);
            _searchButton.style.borderTopLeftRadius = 6f;
            _searchButton.style.borderTopRightRadius = 6f;
            _searchButton.style.borderBottomLeftRadius = 6f;
            _searchButton.style.borderBottomRightRadius = 6f;
            _searchButton.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            _searchButton.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            _searchButton.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            _searchButton.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            _searchButton.style.borderLeftWidth = 1f;
            _searchButton.style.borderRightWidth = 1f;
            _searchButton.style.borderTopWidth = 1f;
            _searchButton.style.borderBottomWidth = 1f;
            _searchButton.style.paddingLeft = 0f;
            _searchButton.style.paddingRight = 0f;
            _searchButton.style.paddingTop = 0f;
            _searchButton.style.paddingBottom = 0f;
            _searchButton.style.justifyContent = Justify.Center;
            _searchButton.style.alignItems = Align.Center;
            _searchButton.tooltip = "Search (Ctrl+F)";
            _searchButton.RegisterCallback<MouseEnterEvent>(_ =>
                _searchButton.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f, 0.95f));
            _searchButton.RegisterCallback<MouseLeaveEvent>(_ =>
                _searchButton.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.85f));
            rootVisualElement.Add(_searchButton);

            GraphOperations.SyncGroupElements();
            rootVisualElement.schedule.Execute(() =>
            {
                ExpandedView?.UpdateExpandedModeBar();
                SidePanelElement?.SyncFromWindow();
                SidePanelElement?.UpdateBlackboard();
                SidePanelElement?.UpdateSelection();
                GridBackground?.UpdateView(_panOffset, _zoom);
            }).StartingIn(10);

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown);
        }

        private void OnGUI()
        {
            if (position.width < 1f || position.height < 1f)
                return;

            if (_pendingController != null)
            {
                var pending = _pendingController;
                _pendingController = null;
                GraphSerializer.LoadController(pending);
            }

            if (_pendingFocusOnContent)
            {
                _pendingFocusOnContent = false;
                ViewAnimator.StartSmoothFocusOnContent(ExpandedView.ComputeVisibleContentBounds());
            }

            var e = Event.current;

            PanController.ResetFrameState();

            float sideW = _showSidePanel ? _sidePanelWidth : CollapsedPanelWidth;
            const float barH = 24f;
            Rect graphRect = new Rect(0f, barH, position.width - sideW, position.height - barH);

            PanController.HandleInput(graphRect, ref _panOffset, ref _zoom);

            if (ViewAnimator.UpdateAnimation(ref _panOffset, ref _zoom, PanController))
                Repaint();

            UpdateConnectionOffsets();

            // Show shortcut guide (always available, even during connecting/editing)
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Slash && e.control)
            {
                ShortcutGuide.Show();
                e.Use();
                Repaint();
                return;
            }

            // Show search panel (always available)
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F && e.control)
            {
                SearchPanel.Show();
                e.Use();
                Repaint();
                return;
            }

            LastMouseGraphPos = (e.mousePosition - _panOffset) / _zoom;

            if (!ConnectionController.IsConnecting && EditingState == null && EditingGroup == null)
                InputHandler.HandleKeyboardShortcuts(e);

            if (e.type == EventType.ContextClick && graphRect.Contains(e.mousePosition))
            {
                if (EditingState != null)
                    EditingState.CommitEditing();

                ConnectionController.Cancel();
                Vector2 graphMousePosition = (e.mousePosition - _panOffset) / _zoom;
                ISelectable hit = InputHandler.HitTest(graphMousePosition);
                StateView hitState = hit as StateView;
                CommentGroupView hitGroup = hit as CommentGroupView;
                ContextMenu.Show(rootVisualElement, e.mousePosition, graphMousePosition, hitState, hitGroup,
                    SelectionController.Count > 0,
                    Clipboard is { Count: > 0 },
                    hitState != null && BreakpointStateIndices.Contains(hitState.DataIndex));
                e.Use();
            }

            if (ConnectionController.IsConnecting)
                InputHandler.HandleConnectingInput(graphRect);
            else
                InputHandler.HandleLeftClickInteraction(graphRect);

            if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && e.type == EventType.KeyDown && EditingState == null)
            {
                if (ExpandedSubStateStack.Count > 0)
                {
                    int topExpanded = ExpandedSubStateStack[ExpandedSubStateStack.Count - 1];
                    bool canGoBack = false;
                    for (int i = 0; i < SelectionController.Count; i++)
                    {
                        if (SelectionController.Selected[i] is StateView s)
                        {
                            if (s.DataIndex == topExpanded || s.IsSubEntry)
                            {
                                canGoBack = true;
                                break;
                            }
                        }
                    }
                    if (canGoBack)
                    {
                        ExpandedView.ExitExpandedSubState();
                        e.Use();
                        Repaint();
                        return;
                    }
                }

                StateView subStateNode = null;
                for (int i = 0; i < SelectionController.Count; i++)
                {
                    if (SelectionController.Selected[i] is StateView s && s.IsSubStateMachine)
                    {
                        if (subStateNode != null) { subStateNode = null; break; }
                        subStateNode = s;
                    }
                }

                if (subStateNode != null)
                {
                    ExpandedView.EnterExpandSubState(subStateNode);
                    e.Use();
                    Repaint();
                }
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (SearchPanel.IsVisible)
                {
                    SearchPanel.Hide();
                    e.Use();
                    Repaint();
                    return;
                }

                if (ShortcutGuide.IsVisible)
                {
                    ShortcutGuide.Hide();
                    e.Use();
                    Repaint();
                    return;
                }

                if (ConnectionController.IsConnecting)
                {
                    ConnectionController.Cancel();
                    e.Use();
                    Repaint();
                }
                else if (ExpandedSubStateStack.Count > 0 && EditingState == null)
                {
                    ExpandedView.ExitExpandedSubState();
                    e.Use();
                    Repaint();
                }
            }

            if (_searchButton != null)
            {
                float rightPadding = _showSidePanel ? _sidePanelWidth + 8f : 8f;
                _searchButton.style.right = rightPadding;
            }

            GraphValidation.RunAndUpdate();
            GridBackground.UpdateView(_panOffset, _zoom);
            ConnectionArrowsLayer.UpdateView(_zoom, _panOffset);
            SyncStateHierarchy();
            UpdateStateTransforms();
            UpdateGroupPositions();
            UpdateGraphPreview(graphRect);
            GraphCanvas.MarkDirtyRepaint();

            if (PanController.IsPanning || DragController.IsActive || SelectionBox.IsActive || ConnectionController.IsConnecting)
                Repaint();
        }

        private void OnGraphCanvasGUI()
        {
            DrawSelectionOverlays();
            SelectionBox.DrawScreen(_zoom, _panOffset);

            if (Event.current.type != EventType.Repaint)
                return;

            UpdateResizeCursor();
        }

        private void OnRootKeyDown(KeyDownEvent e)
        {
            if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
            if (EditingState != null || EditingGroup != null) return;

            if (ExpandedSubStateStack.Count > 0)
            {
                int topExpanded = ExpandedSubStateStack[ExpandedSubStateStack.Count - 1];
                bool canGoBack = false;
                for (int i = 0; i < SelectionController.Count; i++)
                {
                    if (SelectionController.Selected[i] is StateView s)
                    {
                        if (s.DataIndex == topExpanded || s.IsSubEntry)
                        {
                            canGoBack = true;
                            break;
                        }
                    }
                }
                if (canGoBack)
                {
                    ExpandedView.ExitExpandedSubState();
                    e.StopPropagation();
                    Repaint();
                    return;
                }
            }

            StateView subStateNode = null;
            for (int i = 0; i < SelectionController.Count; i++)
            {
                if (SelectionController.Selected[i] is StateView s && s.IsSubStateMachine)
                {
                    if (subStateNode != null) { subStateNode = null; break; }
                    subStateNode = s;
                }
            }

            if (subStateNode != null)
            {
                ExpandedView.EnterExpandSubState(subStateNode);
                e.StopPropagation();
                Repaint();
            }
        }

        // ─── Context Menu Event Handlers ──────────────────────────────

        private void OnCreateStateRequested(Vector2 pos) => GraphOperations.CreateState(pos);
        private void OnCreateSubStateMachineRequested(Vector2 pos) => GraphOperations.CreateSubStateMachine(pos);
        private void OnCreateExternalReferenceRequested(Vector2 pos) => GraphOperations.CreateExternalReferenceState(pos);
        private void OnCreateAnyStateRequested(Vector2 pos) => GraphOperations.CreateAnyState(pos);
        private void OnConnectRequested(StateView source) => GraphOperations.ConnectRequested(source);
        private void OnUngroupRequested(CommentGroupView group) => GraphOperations.UngroupRequested(group);
        private void CopySelectedStates() => GraphOperations.CopySelectedStates();
        private void PasteStates() => GraphOperations.PasteStates();
        private void DeleteSelected() => GraphOperations.DeleteSelected();
        private void OnToggleBreakpointRequested(StateView state)
        {
            bool hasBp = BreakpointStateIndices.Contains(state.DataIndex);
            var cmd = new ToggleBreakpointCommand(this, state.DataIndex, !hasBp);
            UndoRedoSystem.Execute(cmd);
            MarkChangedInternal();
            Repaint();
        }

        internal void SyncStateBreakpointVisuals()
        {
            for (int i = 0; i < States.Count; i++)
                States[i].HasBreakpoint = BreakpointStateIndices.Contains(States[i].DataIndex);
        }

        // ─── Selection Changed ────────────────────────────────────────

        private void OnSelectionChanged()
        {
            List<StateView> pickedStates = new();
            for (int i = 0; i < States.Count; i++)
                if (States[i].IsSelected)
                    pickedStates.Add(States[i]);

            if (pickedStates.Count > 0)
            {
                for (int i = States.Count - 1; i >= 0; i--)
                    if (States[i].IsSelected)
                        States.RemoveAt(i);
                States.AddRange(pickedStates);
            }

            if (EntryState != null && States.Count > 0 && States[0] != EntryState)
            {
                States.Remove(EntryState);
                States.Insert(0, EntryState);
            }

            List<ConnectionView> pickedConnections = new();
            for (int i = 0; i < Connections.Count; i++)
                if (Connections[i].IsSelected)
                    pickedConnections.Add(Connections[i]);

            if (pickedConnections.Count > 0)
            {
                for (int i = Connections.Count - 1; i >= 0; i--)
                    if (Connections[i].IsSelected)
                        Connections.RemoveAt(i);
                Connections.AddRange(pickedConnections);
            }

            List<CommentGroupView> pickedGroups = new();
            for (int i = 0; i < Groups.Count; i++)
                if (Groups[i].IsSelected)
                    pickedGroups.Add(Groups[i]);

            if (pickedGroups.Count > 0)
            {
                for (int i = Groups.Count - 1; i >= 0; i--)
                    if (Groups[i].IsSelected)
                        Groups.RemoveAt(i);
                Groups.AddRange(pickedGroups);
                GraphOperations.SyncGroupElements();
            }

            if (SidePanelElement != null)
                SidePanelElement.UpdateSelection();
        }

        // ─── Render Helpers ───────────────────────────────────────────

        private void DrawSelectionOverlays()
        {
            var selected = SelectionController.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is StateView) continue;
                selected[i].DrawSelectionOverlay(_zoom, _panOffset);
            }
        }

        internal void SyncStateHierarchy()
        {
            for (int i = 0; i < States.Count; i++)
            {
                var state = States[i];
                if (state.parent == null)
                {
                    StateLayer.Add(state);
                }
            }

            for (int i = StateLayer.childCount - 1; i >= 0; i--)
            {
                var child = StateLayer[i];
                if (child is StateView sv && !States.Contains(sv))
                {
                    sv.RemoveFromHierarchy();
                }
            }
        }

        internal void UpdateStateTransforms()
        {
            for (int i = 0; i < States.Count; i++)
            {
                bool visible = ExpandedView.IsStateVisible(States[i]);
                States[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible)
                    States[i].UpdateTransform(_zoom, _panOffset);
            }
        }

        internal void UpdateGroupPositions()
        {
            for (int i = 0; i < Groups.Count; i++)
                Groups[i].UpdateScreenPosition(_zoom, _panOffset);
        }

        private void UpdateGraphPreview(Rect graphRect)
        {
            if (GraphPreview == null) return;

            float sideW = _showSidePanel ? _sidePanelWidth : CollapsedPanelWidth;

            float previewW = GraphPreview.PreviewWidth;
            float previewH = GraphPreview.PreviewHeight;
            previewW = Mathf.Clamp(previewW, GraphPreview.MinPreviewWidth, position.width);
            previewH = Mathf.Clamp(previewH, GraphPreview.MinPreviewHeight, position.height);
            float defaultRight = sideW + 8f;
            float defaultBottom = 8f;

            float leftPos = position.width - defaultRight - previewW + GraphPreview.DragOffset.x;
            float topPos = position.height - defaultBottom - previewH + GraphPreview.DragOffset.y;

            const float barH = 24f;
            float rightBoundary = _showSidePanel ? position.width - sideW : position.width;
            leftPos = Mathf.Clamp(leftPos, 0f, rightBoundary - previewW);
            topPos = Mathf.Clamp(topPos, barH, position.height - previewH);

            GraphPreview.style.left = leftPos;
            GraphPreview.style.top = topPos;
            GraphPreview.style.width = previewW;
            GraphPreview.style.height = previewH;

            GraphPreview.UpdateView(
                States,
                Connections,
                state => IsStateVisible(state),
                conn => IsConnectionVisible(conn),
                _panOffset,
                _zoom,
                graphRect);
        }

        internal void UpdateConnectionOffsets()
        {
            var groups = new Dictionary<(StateView, StateView), List<ConnectionView>>();
            for (int i = 0; i < Connections.Count; i++)
            {
                var conn = Connections[i];
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

        internal void UpdateResizeCursor()
        {
            float hs = ResizeHandleScreenSize;

            for (int i = 0; i < Groups.Count; i++)
            {
                var group = Groups[i];

                Rect r = group.GetGraphBounds();
                Vector2 sp = r.position * _zoom + _panOffset;
                Vector2 ss = r.size * _zoom;

                float edgeW = hs * 2f;
                float inset = hs;

                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x - hs, sp.y + inset, edgeW, ss.y - inset * 2f),
                    MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x + ss.x - hs, sp.y + inset, edgeW, ss.y - inset * 2f),
                    MouseCursor.ResizeHorizontal);

                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x + inset, sp.y - hs, ss.x - inset * 2f, edgeW),
                    MouseCursor.ResizeVertical);
                EditorGUIUtility.AddCursorRect(
                    new Rect(sp.x + inset, sp.y + ss.y - hs, ss.x - inset * 2f, edgeW),
                    MouseCursor.ResizeVertical);

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

        // ─── Internal Helpers for Modules ────────────────────────────

        internal void MarkChangedInternal()
        {
            if (_isLoading) return;
            GraphValidation?.MarkDirty();
            _hasUnsavedChanges = true;
            hasUnsavedChanges = true;
            UpdateTitleInternal();
        }

        internal void MarkSavedInternal()
        {
            _hasUnsavedChanges = false;
            hasUnsavedChanges = false;
            UpdateTitleInternal();
        }

        internal void UpdateTitleInternal()
        {
            string name = _controller != null ? _controller.name : "CleanStateMachine";
            titleContent = new GUIContent(name);
        }

        internal void OnSaveCommandInternal()
        {
            if (_controller != null)
            {
                GraphSerializer.SaveToController();
                _controller.Save();
                MarkSavedInternal();
            }
            Repaint();
        }

        internal void OnSaveCommand() => OnSaveCommandInternal();

        internal void EnsureEntryStateExistsInternal() => GraphOperations.EnsureEntryStateExists();

        internal void EnterExpandSubStateInternal(StateView s)
        {
            ExpandedView.EnterExpandSubState(s);
            Repaint();
        }

        internal void ExitExpandedSubStateInternal()
        {
            ExpandedView.ExitExpandedSubState();
            Repaint();
        }

        internal void EnterExpandSubState(StateView s) => ExpandedView.EnterExpandSubState(s);
        internal void ExitExpandedSubState() => ExpandedView.ExitExpandedSubState();
        internal bool IsCurrentExpandedSubState(StateView state) => ExpandedView.IsCurrentExpandedSubState(state);

        internal void StartSmoothFocusOnContentInternal()
        {
            ViewAnimator.StartSmoothFocusOnContent(ExpandedView.ComputeVisibleContentBounds());
        }

        internal void BeginGroupResize(CommentGroupView group, ResizeEdge edge, Vector2 graphPos)
        {
            ResizingGroup = group;
            ResizeEdgeFlags = edge;
            ResizeStartGraphPos = graphPos;
            ResizeStartRect = group.GetGraphBounds();
        }

        internal void UndoRedoSystemClear()
        {
            UndoRedoSystem = new UndoRedoSystem();
        }

        // ─── Visibility Delegates ─────────────────────────────────────

        internal bool IsStateVisible(StateView state) => ExpandedView.IsStateVisible(state);
        internal bool IsConnectionVisible(ConnectionView conn) => ExpandedView.IsConnectionVisible(conn);

        // ─── Internal Accessors for SidePanel / DetailsPanel ─────────

        internal bool GetShowSidePanel() => _showSidePanel;
        internal void SetShowSidePanel(bool value)
        {
            _showSidePanel = value;
            EditorPrefs.SetBool(LayoutPrefs.ShowSidePanel, value);
            if (SidePanelElement != null)
                SidePanelElement.SetExpanded(value);
        }

        internal float GetSidePanelWidth() => _sidePanelWidth;
        internal void SetSidePanelWidth(float value)
        {
            _sidePanelWidth = value;
            EditorPrefs.SetFloat(LayoutPrefs.SidePanelWidth, value);
        }

        internal float GetDetailsHeightRatio() => _detailsHeightRatio;
        internal void SetDetailsHeightRatio(float value)
        {
            _detailsHeightRatio = value;
            EditorPrefs.SetFloat(LayoutPrefs.DetailsHeightRatio, value);
        }

        internal IReadOnlyList<ISelectable> GetSelection() => SelectionController.Selected;
        internal List<StateView> GetStates() => States;
        internal List<ConnectionView> GetConnections() => Connections;
        internal List<BlackboardVariable> GetBlackboardVariables() => BlackboardVariables;

        internal void NotifySidePanelChanged()
        {
            MarkChangedInternal();
            Repaint();
        }

        internal void SaveSidePanelLayout(float detailsHeightRatio)
        {
            _detailsHeightRatio = detailsHeightRatio;
            EditorPrefs.SetFloat(LayoutPrefs.DetailsHeightRatio, detailsHeightRatio);
        }

        public override void SaveChanges()
        {
            base.SaveChanges();
            if (_controller != null)
            {
                GraphSerializer.SaveToController();
                _controller.Save();
                MarkSavedInternal();
            }
        }

        // ─── Play Mode Update ─────────────────────────────────────────

        private void OnEditorUpdate()
        {
            if (_controller == null && !EditorApplication.isPlaying) return;
            PlayModeTracker.OnEditorUpdate();
        }
    }
}
