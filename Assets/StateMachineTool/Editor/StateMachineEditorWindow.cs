using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using SM = StateMachineTool.Runtime;

namespace StateMachineTool.Editor
{
    public class StateMachineEditorWindow : EditorWindow
    {
        private SM.StateMachineAsset currentAsset;
        private StateMachineGraphView graphView;
        private BlackboardEditorView blackboardView;
        private StateInspectorView inspectorView;
        private TwoPaneSplitView horizontalSplit;
        private TwoPaneSplitView leftVerticalSplit;
        private VisualElement noAssetOverlay;

        public static void ShowWindow()
        {
            var window = GetWindow<StateMachineEditorWindow>();
            window.titleContent = new GUIContent("State Machine");
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        public static void OpenAsset(SM.StateMachineAsset asset)
        {
            var window = GetWindow<StateMachineEditorWindow>();
            window.titleContent = new GUIContent("State Machine");
            window.LoadAsset(asset);
            window.Show();
        }

        private void OnEnable()
        {
            BuildLayout();
            LoadStyleSheet();
            UpdateOverlayVisibility();
        }

        private void OnDisable()
        {
            if (graphView != null)
                graphView.OnStateSelected -= OnStateSelected;
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is SM.StateMachineAsset asset)
            {
                LoadAsset(asset);
            }
        }

        private void BuildLayout()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;

            BuildToolbar(root);
            BuildNoAssetOverlay(root);
            BuildMainContent(root);
        }

        private void BuildToolbar(VisualElement root)
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("editor-toolbar");
            root.Add(toolbar);

            var assetLabel = new Label("No asset loaded");
            assetLabel.name = "asset-label";
            assetLabel.AddToClassList("toolbar-label");
            toolbar.Add(assetLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            var addStateBtn = new Button(() =>
            {
                if (graphView != null)
                    graphView.CreateStateAtCenter("New State");
            }) { text = "+ State", tooltip = "Create a new state at graph center (or right-click the graph)" };
            addStateBtn.AddToClassList("toolbar-button");
            addStateBtn.style.display = DisplayStyle.None;
            addStateBtn.name = "add-state-btn";
            toolbar.Add(addStateBtn);

            var addEntryBtn = new Button(() =>
            {
                if (graphView != null)
                    graphView.CreateEntryStateAtCenter();
            }) { text = "+ Entry", tooltip = "Create the entry state" };
            addEntryBtn.AddToClassList("toolbar-button");
            addEntryBtn.style.display = DisplayStyle.None;
            addEntryBtn.name = "add-entry-btn";
            toolbar.Add(addEntryBtn);

            var saveBtn = new Button(() =>
            {
                if (currentAsset != null)
                {
                    EditorUtility.SetDirty(currentAsset);
                    AssetDatabase.SaveAssets();
                }
            }) { text = "Save", tooltip = "Save the state machine asset" };
            saveBtn.AddToClassList("toolbar-button");
            saveBtn.style.display = DisplayStyle.None;
            saveBtn.name = "save-btn";
            toolbar.Add(saveBtn);

            var createBtn = new Button(StateMachineMenuItems.CreateStateMachineAsset)
            { text = "New Asset", tooltip = "Create a new state machine asset" };
            createBtn.AddToClassList("toolbar-button");
            createBtn.name = "create-btn";
            toolbar.Add(createBtn);
        }

        private void BuildNoAssetOverlay(VisualElement root)
        {
            noAssetOverlay = new VisualElement();
            noAssetOverlay.AddToClassList("no-asset-overlay");
            noAssetOverlay.name = "no-asset-overlay";

            var overlayContent = new VisualElement();
            overlayContent.AddToClassList("overlay-content");

            var icon = new Label("( )");
            icon.AddToClassList("overlay-icon");

            var heading = new Label("State Machine Editor");
            heading.AddToClassList("overlay-heading");

            var subtext = new Label("Create a new State Machine Asset or select an existing one to begin.");
            subtext.AddToClassList("overlay-subtext");

            var createAssetBtn = new Button(StateMachineMenuItems.CreateStateMachineAsset)
            { text = "New State Machine Asset" };
            createAssetBtn.AddToClassList("overlay-button");

            overlayContent.Add(icon);
            overlayContent.Add(heading);
            overlayContent.Add(subtext);
            overlayContent.Add(createAssetBtn);

            noAssetOverlay.Add(overlayContent);
            root.Add(noAssetOverlay);
        }

        private void BuildMainContent(VisualElement root)
        {
            horizontalSplit = new TwoPaneSplitView(1, 280, TwoPaneSplitViewOrientation.Horizontal);
            horizontalSplit.name = "horizontal-split";
            horizontalSplit.style.flexGrow = 1;

            leftVerticalSplit = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Vertical);
            leftVerticalSplit.name = "left-vertical-split";

            blackboardView = new BlackboardEditorView();
            blackboardView.name = "blackboard-view";
            blackboardView.OnChanged += OnGraphDataChanged;
            leftVerticalSplit.Add(blackboardView);

            graphView = new StateMachineGraphView();
            graphView.name = "graph-view";
            graphView.OnStateSelected += OnStateSelected;
            graphView.OnTransitionSelected += OnTransitionSelected;
            graphView.OnGraphChanged += OnGraphDataChanged;
            leftVerticalSplit.Add(graphView);

            horizontalSplit.Add(leftVerticalSplit);

            inspectorView = new StateInspectorView();
            inspectorView.name = "inspector-view";
            inspectorView.OnChanged += OnGraphDataChanged;
            horizontalSplit.Add(inspectorView);

            root.Add(horizontalSplit);
        }

        public void LoadAsset(SM.StateMachineAsset asset)
        {
            currentAsset = asset;
            graphView?.LoadAsset(asset);
            blackboardView?.LoadAsset(asset);
            inspectorView?.LoadAsset(asset);
            UpdateOverlayVisibility();
        }

        private void UpdateOverlayVisibility()
        {
            bool hasAsset = currentAsset != null;

            var overlay = rootVisualElement.Q<VisualElement>("no-asset-overlay");
            if (overlay != null)
                overlay.style.display = hasAsset ? DisplayStyle.None : DisplayStyle.Flex;

            var split = rootVisualElement.Q<TwoPaneSplitView>("horizontal-split");
            if (split != null)
                split.style.display = hasAsset ? DisplayStyle.Flex : DisplayStyle.None;

            var assetLabel = rootVisualElement.Q<Label>("asset-label");
            if (assetLabel != null)
                assetLabel.text = hasAsset ? currentAsset.name : "No asset loaded";

            var addStateBtn = rootVisualElement.Q<Button>("add-state-btn");
            if (addStateBtn != null)
                addStateBtn.style.display = hasAsset ? DisplayStyle.Flex : DisplayStyle.None;

            var addEntryBtn = rootVisualElement.Q<Button>("add-entry-btn");
            if (addEntryBtn != null)
                addEntryBtn.style.display = hasAsset ? DisplayStyle.Flex : DisplayStyle.None;

            var saveBtn = rootVisualElement.Q<Button>("save-btn");
            if (saveBtn != null)
                saveBtn.style.display = hasAsset ? DisplayStyle.Flex : DisplayStyle.None;

            var createBtn = rootVisualElement.Q<Button>("create-btn");
            if (createBtn != null)
                createBtn.style.display = hasAsset ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnStateSelected(SM.StateData stateData)
        {
            inspectorView?.ShowState(stateData);
        }

        private void OnTransitionSelected(SM.TransitionData transitionData)
        {
            inspectorView?.ShowTransition(transitionData);
        }

        private void OnGraphDataChanged()
        {
            if (currentAsset != null)
            {
                EditorUtility.SetDirty(currentAsset);
                blackboardView?.Refresh();
                SyncGraphViewNodes();
            }
        }

        private void SyncGraphViewNodes()
        {
            if (graphView == null || currentAsset == null) return;

            foreach (var stateData in currentAsset.graphData.states)
            {
                var nodeView = graphView.GetNodeByStateId(stateData.id);
                if (nodeView != null)
                {
                    nodeView.UpdateFromData(stateData);
                }
            }
        }

        private void LoadStyleSheet()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/StateMachineTool/Editor/StateMachineStyles.uss");

            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);
        }
    }
}
