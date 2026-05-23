using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public class SidePanel : VisualElement
    {
        private readonly CleanStateMachineWindow _window;
        private readonly DetailsPanel _detailsPanel;
        private readonly BlackboardPanel _blackboardPanel;

        private readonly VisualElement _collapsedView;
        private readonly VisualElement _expandedView;
        private readonly VisualElement _internalSplitter;
        private readonly VisualElement _expandedContent;
        private readonly VisualElement _panelEdgeSplitter;

        private bool _isDraggingInternalSplitter;
        private bool _isDraggingPanelSplitter;
        private float _dragStartMouseX;
        private float _dragStartWidth;
        private float _dragStartMouseY;
        private float _dragStartDetailsHeight;
        private float _detailsHeightRatio = 0.5f;

        public DetailsPanel DetailsPanel => _detailsPanel;
        public BlackboardPanel BlackboardPanel => _blackboardPanel;

        public SidePanel(CleanStateMachineWindow window)
        {
            _window = window;

            var styleSheet = ScriptReferenceUtility.LoadStyleSheet("SidePanel");
            if (styleSheet != null)
                styleSheets.Add(styleSheet);

            AddToClassList("side-panel");

            // Collapsed view (thin bar)
            _collapsedView = new VisualElement();
            _collapsedView.AddToClassList("side-panel-collapsed");
            _collapsedView.RegisterCallback<ClickEvent>(OnCollapsedClicked);

            var collapsedArrow = new Label("\u25B6");
            collapsedArrow.AddToClassList("collapsed-arrow");
            _collapsedView.Add(collapsedArrow);
            Add(_collapsedView);

            // Expanded view
            _expandedView = new VisualElement();
            _expandedView.AddToClassList("side-panel-expanded");

            // Toggle button (top-right arrow)
            var toggleBtn = new VisualElement();
            toggleBtn.AddToClassList("toggle-button");
            toggleBtn.RegisterCallback<ClickEvent>(OnToggleClicked);
            var toggleArrow = new Label("\u25B6");
            toggleArrow.style.fontSize = 10;
            toggleArrow.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            toggleBtn.Add(toggleArrow);
            _expandedView.Add(toggleBtn);

            // Expanded content (details + splitter + blackboard, no toggle)
            _expandedContent = new VisualElement();
            _expandedContent.AddToClassList("side-panel-expanded");

            // Details panel
            _detailsPanel = new DetailsPanel(window);
            _detailsPanel.AddToClassList("details-panel");
            _expandedContent.Add(_detailsPanel);

            // Internal splitter
            _internalSplitter = new VisualElement();
            _internalSplitter.AddToClassList("internal-splitter");
            var splitterVisual = new VisualElement();
            splitterVisual.AddToClassList("internal-splitter-visual");
            _internalSplitter.Add(splitterVisual);

            _internalSplitter.RegisterCallback<MouseDownEvent>(OnInternalSplitterDown);
            _internalSplitter.RegisterCallback<MouseMoveEvent>(OnInternalSplitterMove);
            _internalSplitter.RegisterCallback<MouseUpEvent>(OnInternalSplitterUp);
            _expandedContent.Add(_internalSplitter);

            // Blackboard panel
            _blackboardPanel = new BlackboardPanel(window);
            _blackboardPanel.AddToClassList("blackboard-panel");
            _expandedContent.Add(_blackboardPanel);

            _expandedView.Add(_expandedContent);
            Add(_expandedView);

            _panelEdgeSplitter = new VisualElement();
            _panelEdgeSplitter.AddToClassList("panel-edge-splitter");
            Add(_panelEdgeSplitter);

            var panelShadow = new VisualElement();
            panelShadow.AddToClassList("panel-shadow");
            panelShadow.pickingMode = PickingMode.Ignore;
            var shadowTex = new Texture2D(10, 1, TextureFormat.RGBA32, false);
            shadowTex.wrapMode = TextureWrapMode.Clamp;
            shadowTex.filterMode = FilterMode.Bilinear;
            for (int i = 0; i < 10; i++)
            {
                float t = i / 9f;
                shadowTex.SetPixel(i, 0, new Color(0, 0, 0, t * 0.45f));
            }
            shadowTex.Apply();
            panelShadow.style.backgroundImage = new StyleBackground(shadowTex);
            Add(panelShadow);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _panelEdgeSplitter.RegisterCallback<MouseDownEvent>(OnPanelSplitterDown);
            _panelEdgeSplitter.RegisterCallback<MouseMoveEvent>(OnPanelSplitterMove);
            _panelEdgeSplitter.RegisterCallback<MouseUpEvent>(OnPanelSplitterUp);

            UpdateVisibility();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (parent != null && _detailsPanel != null && _blackboardPanel != null)
            {
                UpdateInternalLayout();
            }
        }

        private void UpdateInternalLayout()
        {
            float parentHeight = _expandedContent.resolvedStyle.height;
            if (parentHeight < 1f) return;

            float splitterH = _internalSplitter.resolvedStyle.height;
            if (splitterH < 1f) splitterH = 8f;

            float available = parentHeight - splitterH;
            float detailsHeight = Mathf.Clamp(available * _detailsHeightRatio, 60f, available - 60f);

            _detailsPanel.style.height = detailsHeight;
            _blackboardPanel.style.flexGrow = 1;
        }

        public void SetExpanded(bool expanded)
        {
            bool currentIsExpanded = _collapsedView.style.display == DisplayStyle.None;

            _collapsedView.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
            _expandedView.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

            if (expanded)
            {
                style.width = _window.GetSidePanelWidth();
                schedule.Execute(UpdateInternalLayout).StartingIn(10);
            }
            else
            {
                style.width = 32f;
            }
        }

        public void UpdateVisibility()
        {
            SetExpanded(_window.GetShowSidePanel());
        }

        public void SyncFromWindow()
        {
            _detailsHeightRatio = _window.GetDetailsHeightRatio();
            schedule.Execute(UpdateInternalLayout).StartingIn(10);
        }

        public void UpdateSelection()
        {
            _detailsPanel.UpdateSelection(
                _window.GetSelection(),
                _window.GetStates(),
                _window.GetConnections(),
                _window.GetBlackboardVariables());
        }

        public void UpdateBlackboard()
        {
            _blackboardPanel.UpdateVariables(_window.GetBlackboardVariables());
        }

        private void OnCollapsedClicked(ClickEvent evt)
        {
            _window.SetShowSidePanel(true);
            SetExpanded(true);
        }

        private void OnToggleClicked(ClickEvent evt)
        {
            _window.SetShowSidePanel(false);
            SetExpanded(false);
        }

        private void OnInternalSplitterDown(MouseDownEvent evt)
        {
            if (evt.button == 0)
            {
                _isDraggingInternalSplitter = true;
                _dragStartMouseY = evt.mousePosition.y;
                _dragStartDetailsHeight = _detailsPanel.resolvedStyle.height;
                _internalSplitter.AddToClassList("internal-splitter-active");
                _internalSplitter.CaptureMouse();
                evt.StopPropagation();
            }
        }

        private void OnInternalSplitterMove(MouseMoveEvent evt)
        {
            if (!_isDraggingInternalSplitter) return;

            float delta = evt.mousePosition.y - _dragStartMouseY;
            float parentHeight = _expandedContent.resolvedStyle.height;
            if (parentHeight < 1f) return;

            float splitterH = _internalSplitter.resolvedStyle.height;
            if (splitterH < 1f) splitterH = 8f;
            float available = parentHeight - splitterH;

            float detailsH = Mathf.Clamp(_dragStartDetailsHeight + delta, 60f, available - 60f);
            _detailsHeightRatio = detailsH / available;

            _detailsPanel.style.height = detailsH;
            evt.StopPropagation();
        }

        private void OnInternalSplitterUp(MouseUpEvent evt)
        {
            if (_isDraggingInternalSplitter)
            {
                _isDraggingInternalSplitter = false;
                _internalSplitter.RemoveFromClassList("internal-splitter-active");
                _internalSplitter.ReleaseMouse();
                _window.SaveSidePanelLayout(_detailsHeightRatio);
                evt.StopPropagation();
            }
        }

        // ─── Graph-Panel Splitter (drag left edge) ────────────────────

        private void OnPanelSplitterDown(MouseDownEvent evt)
        {
            if (evt.button == 0)
            {
                _isDraggingPanelSplitter = true;
                _dragStartMouseX = evt.mousePosition.x;
                _dragStartWidth = resolvedStyle.width;
                _panelEdgeSplitter.CaptureMouse();
                evt.StopPropagation();
            }
        }

        private void OnPanelSplitterMove(MouseMoveEvent evt)
        {
            if (!_isDraggingPanelSplitter) return;

            float delta = evt.mousePosition.x - _dragStartMouseX;
            float newWidth = Mathf.Clamp(_dragStartWidth - delta, 220f, 600f);
            style.width = newWidth;
            _window.SetSidePanelWidth(newWidth);
            _window.Repaint();
            evt.StopPropagation();
        }

        private void OnPanelSplitterUp(MouseUpEvent evt)
        {
            if (_isDraggingPanelSplitter)
            {
                _isDraggingPanelSplitter = false;
                _panelEdgeSplitter.ReleaseMouse();
                evt.StopPropagation();
            }
        }
    }
}
