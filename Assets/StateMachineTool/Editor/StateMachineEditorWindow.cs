using System.Linq;
using StateMachineTool.Runtime;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateMachineTool.Editor
{
    public class StateMachineEditorWindow : EditorWindow
    {
        private StateMachineAsset currentAsset;
        private StateMachineGraphView graphView;
        private BlackboardEditorView blackboardView;
        private StateInspectorView inspectorView;
        private TwoPaneSplitView horizontalSplit;
        private TwoPaneSplitView leftVerticalSplit;

        [MenuItem("Window/State Machine Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<StateMachineEditorWindow>();
            window.titleContent = new GUIContent("State Machine");
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        public static void OpenAsset(StateMachineAsset asset)
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
        }

        private void OnDisable()
        {
            if (graphView != null)
                graphView.OnStateSelected -= OnStateSelected;
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is StateMachineAsset asset)
            {
                LoadAsset(asset);
            }
        }

        private void BuildLayout()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;

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

            var saveBtn = new Button(() =>
            {
                if (currentAsset != null)
                {
                    EditorUtility.SetDirty(currentAsset);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[StateMachine] Saved: {currentAsset.name}");
                }
            }) { text = "Save" };
            saveBtn.AddToClassList("toolbar-button");
            toolbar.Add(saveBtn);

            var refreshBtn = new Button(() =>
            {
                if (graphView != null && currentAsset != null)
                    graphView.RefreshGraph();
                if (blackboardView != null)
                    blackboardView.Refresh();
            }) { text = "Refresh" };
            refreshBtn.AddToClassList("toolbar-button");
            toolbar.Add(refreshBtn);

            // --- Graph + Side Panels ---
            horizontalSplit = new TwoPaneSplitView(0, 260, TwoPaneSplitViewOrientation.Horizontal);
            horizontalSplit.style.flexGrow = 1;

            leftVerticalSplit = new TwoPaneSplitView(1, 200, TwoPaneSplitViewOrientation.Vertical);

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

        public void LoadAsset(StateMachineAsset asset)
        {
            currentAsset = asset;
            graphView?.LoadAsset(asset);
            blackboardView?.LoadAsset(asset);
            inspectorView?.LoadAsset(asset);

            var assetLabel = rootVisualElement.Q<Label>("asset-label");
            if (assetLabel != null)
                assetLabel.text = asset != null ? asset.name : "No asset loaded";
        }

        private void OnStateSelected(StateData stateData)
        {
            inspectorView?.ShowState(stateData);
        }

        private void OnTransitionSelected(TransitionData transitionData)
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
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
        }
    }
}
