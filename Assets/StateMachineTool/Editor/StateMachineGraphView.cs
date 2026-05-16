using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using SM = StateMachineTool.Runtime;

namespace StateMachineTool.Editor
{
    public class StateMachineGraphView : GraphView
    {
        public SM.StateMachineAsset Asset { get; private set; }
        public System.Action<SM.StateData> OnStateSelected;
        public System.Action<SM.TransitionData> OnTransitionSelected;
        public System.Action OnGraphChanged;

        private Vector2 lastMousePosition;
        private bool isBuilding;
        private Label emptyGraphLabel;

        public StateMachineGraphView()
        {
            this.StretchToParentSize();

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ClickSelector());

            var minimap = new MiniMap
            {
                anchored = true,
                windowed = false
            };
            minimap.SetPosition(new Rect(10, 30, 180, 130));
            minimap.AddToClassList("graph-minimap");
            Add(minimap);

            RegisterCallback<MouseMoveEvent>(e =>
            {
                lastMousePosition = e.localMousePosition;
            });

            graphViewChanged += OnGraphViewChanged;
            serializeGraphElements += OnSerializeGraphElements;
            unserializeAndPaste += OnUnserializeAndPaste;
            canPasteSerializedData += OnCanPasteSerializedData;

            nodeCreationRequest = ctx =>
            {
                if (Asset == null) return;
                var mousePos = contentViewContainer.WorldToLocal(ctx.screenMousePosition);
                var stateData = new SM.StateData("New State", mousePos);
                CreateStateNode(stateData);
            };

            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
                {
                    DeleteSelection();
                    evt.StopPropagation();
                }
            });

            style.flexGrow = 1;

            var grid = new GridBackground();
            grid.name = "grid-background";
            grid.StretchToParentSize();
            Insert(0, grid);

            emptyGraphLabel = new Label("Right-click here or use the Toolbar buttons to add states.\nDrag from Out ports ( ) to connect states.");
            emptyGraphLabel.name = "empty-graph-label";
            emptyGraphLabel.AddToClassList("empty-graph-label");
            Add(emptyGraphLabel);
        }

        public void LoadAsset(SM.StateMachineAsset asset)
        {
            Asset = asset;
            if (Asset == null) return;

            isBuilding = true;
            ClearGraph();
            BuildGraph();
            isBuilding = false;
        }

        public void RefreshGraph()
        {
            if (Asset == null) return;

            isBuilding = true;
            ClearGraph();
            BuildGraph();
            isBuilding = false;
        }

        private void ClearGraph()
        {
            DeleteElements(graphElements.ToList());
        }

        private void BuildGraph()
        {
            var graphData = Asset.graphData;

            foreach (var state in graphData.states)
                CreateStateNodeView(state);

            foreach (var transition in graphData.transitions)
                CreateTransitionEdge(transition);

            UpdateEmptyLabel();
        }

        private void UpdateEmptyLabel()
        {
            if (emptyGraphLabel == null) return;
            bool hasStates = Asset != null ? Asset.graphData.states.Count > 0 : false;
            emptyGraphLabel.style.display = hasStates ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void CreateStateAtCenter(string name)
        {
            if (Asset == null) return;
            var center = contentViewContainer.WorldToLocal(
                new Vector2(contentViewContainer.layout.width / 2, contentViewContainer.layout.height / 2));
            var stateData = new SM.StateData(name, center);
            CreateStateNode(stateData);
        }

        public void CreateEntryStateAtCenter()
        {
            if (Asset == null) return;
            var center = contentViewContainer.WorldToLocal(
                new Vector2(contentViewContainer.layout.width / 2, contentViewContainer.layout.height / 2));
            var stateData = new SM.StateData("Entry", center, SM.StateType.Entry);
            Asset.graphData.entryStateId = stateData.id;
            CreateStateNode(stateData);
        }

        public StateNodeView CreateStateNode(SM.StateData stateData)
        {
            Undo.RecordObject(Asset, "Create State");
            Asset.graphData.states.Add(stateData);

            if (stateData.stateType == SM.StateType.Entry)
                Asset.graphData.entryStateId = stateData.id;

            var nodeView = CreateStateNodeView(stateData);
            EditorUtility.SetDirty(Asset);
            OnGraphChanged?.Invoke();
            UpdateEmptyLabel();
            return nodeView;
        }

        private StateNodeView CreateStateNodeView(SM.StateData stateData)
        {
            var nodeView = new StateNodeView(this, stateData);
            nodeView.RegisterCallback<PointerDownEvent>(evt =>
            {
                OnStateSelected?.Invoke(stateData);
            });
            AddElement(nodeView);
            return nodeView;
        }

        public void CreateTransition(SM.StateData fromState, SM.StateData toState)
        {
            var fromNode = GetNodeByStateId(fromState.id);
            var toNode = GetNodeByStateId(toState.id);
            if (fromNode == null || toNode == null) return;

            var transitionData = new SM.TransitionData(fromState.id, toState.id)
            {
                displayName = $"{fromState.displayName} -> {toState.displayName}"
            };
            Asset.graphData.transitions.Add(transitionData);

            ConnectNodes(fromNode, toNode, transitionData.id);
            EditorUtility.SetDirty(Asset);
            OnGraphChanged?.Invoke();
        }

        private void ConnectNodes(StateNodeView fromNode, StateNodeView toNode, string transitionId)
        {
            var edge = new TransitionEdgeView(transitionId);
            edge.output = fromNode.GetOutputPort();
            edge.input = toNode.GetInputPort();
            edge.output.Connect(edge);
            edge.input.Connect(edge);

            edge.RegisterCallback<PointerDownEvent>(evt =>
            {
                var transition = Asset.graphData.transitions.FirstOrDefault(t => t.id == transitionId);
                if (transition != null)
                    OnTransitionSelected?.Invoke(transition);
            });

            AddElement(edge);
        }

        private void CreateTransitionEdge(SM.TransitionData transitionData)
        {
            var fromNode = GetNodeByStateId(transitionData.fromStateId);
            var toNode = GetNodeByStateId(transitionData.toStateId);
            if (fromNode == null || toNode == null) return;

            var edge = new TransitionEdgeView(transitionData.id);
            edge.output = fromNode.GetOutputPort();
            edge.input = toNode.GetInputPort();
            edge.output.Connect(edge);
            edge.input.Connect(edge);

            var capturedId = transitionData.id;
            edge.RegisterCallback<PointerDownEvent>(evt =>
            {
                var transition = Asset.graphData.transitions.FirstOrDefault(t => t.id == capturedId);
                if (transition != null)
                    OnTransitionSelected?.Invoke(transition);
            });

            AddElement(edge);
        }

        public StateNodeView GetNodeByStateId(string stateId)
        {
            return nodes.OfType<StateNodeView>().FirstOrDefault(n => n.StateId == stateId);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(endPort =>
                endPort.direction != startPort.direction &&
                endPort.node != startPort.node
            ).ToList();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (isBuilding) return change;

            if (change.elementsToRemove != null)
            {
                var transitionsToRemove = change.elementsToRemove
                    .OfType<TransitionEdgeView>()
                    .Select(e => e.TransitionId)
                    .ToList();

                var statesToRemove = change.elementsToRemove
                    .OfType<StateNodeView>()
                    .Select(n => n.StateId)
                    .ToList();

                foreach (var tid in transitionsToRemove)
                    RemoveTransitionData(tid);
                foreach (var sid in statesToRemove)
                    RemoveStateData(sid);
            }

            if (change.edgesToCreate != null)
            {
                for (int i = change.edgesToCreate.Count - 1; i >= 0; i--)
                {
                    var edge = change.edgesToCreate[i];
                    if (edge.output?.node is StateNodeView fromNode &&
                        edge.input?.node is StateNodeView toNode &&
                        fromNode.StateId != toNode.StateId)
                    {
                        var fromState = Asset.GetState(fromNode.StateId);
                        var toState = Asset.GetState(toNode.StateId);
                        if (fromState != null && toState != null)
                        {
                            Undo.RecordObject(Asset, "Create Transition");

                            var transitionData = new SM.TransitionData(fromState.id, toState.id)
                            {
                                displayName = $"{fromState.displayName} -> {toState.displayName}"
                            };
                            Asset.graphData.transitions.Add(transitionData);

                            var transitionEdge = new TransitionEdgeView(transitionData.id);
                            transitionEdge.output = edge.output;
                            transitionEdge.input = edge.input;

                            var capturedId = transitionData.id;
                            transitionEdge.RegisterCallback<PointerDownEvent>(evt =>
                            {
                                var t = Asset.graphData.transitions.FirstOrDefault(x => x.id == capturedId);
                                if (t != null) OnTransitionSelected?.Invoke(t);
                            });
                            change.edgesToCreate[i] = transitionEdge;
                        }
                    }
                }
                EditorUtility.SetDirty(Asset);
                OnGraphChanged?.Invoke();
            }

            if (change.movedElements != null)
            {
                foreach (var element in change.movedElements)
                {
                    if (element is StateNodeView stateNode)
                    {
                        var stateData = Asset.GetState(stateNode.StateId);
                        if (stateData != null)
                            stateData.position = stateNode.GetPosition().position;
                    }
                }
                EditorUtility.SetDirty(Asset);
            }

            return change;
        }

        private void RemoveTransitionData(string transitionId)
        {
            Asset.graphData.transitions.RemoveAll(t => t.id == transitionId);
            EditorUtility.SetDirty(Asset);
            OnGraphChanged?.Invoke();
        }

        private void RemoveStateData(string stateId)
        {
            Asset.graphData.transitions.RemoveAll(t => t.fromStateId == stateId || t.toStateId == stateId);
            Asset.graphData.states.RemoveAll(s => s.id == stateId);

            if (Asset.graphData.entryStateId == stateId)
                Asset.graphData.entryStateId = null;

            EditorUtility.SetDirty(Asset);
            OnGraphChanged?.Invoke();
            UpdateEmptyLabel();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (Asset == null)
            {
                base.BuildContextualMenu(evt);
                return;
            }

            evt.menu.AppendAction("Add State", action =>
            {
                var mousePos = viewTransform.matrix.inverse.MultiplyPoint(lastMousePosition);
                var stateData = new SM.StateData("New State", mousePos);
                CreateStateNode(stateData);
            }, DropdownMenuAction.AlwaysEnabled);

            evt.menu.AppendAction("Add Entry State (Start)", action =>
            {
                var mousePos = viewTransform.matrix.inverse.MultiplyPoint(lastMousePosition);
                var stateData = new SM.StateData("Entry", mousePos, SM.StateType.Entry);
                Asset.graphData.entryStateId = stateData.id;
                CreateStateNode(stateData);
            }, DropdownMenuAction.AlwaysEnabled);

            bool hasEntryState = Asset.graphData.states.Any(s => s.stateType == SM.StateType.Entry);
            if (!hasEntryState)
            {
                evt.menu.AppendSeparator();

                evt.menu.AppendAction("(No entry state defined)", null,
                    DropdownMenuAction.AlwaysDisabled);
            }

            evt.menu.AppendSeparator();
            base.BuildContextualMenu(evt);
        }

        private string OnSerializeGraphElements(IEnumerable<GraphElement> elements) => string.Empty;
        private void OnUnserializeAndPaste(string operationName, string data) { }
        private bool OnCanPasteSerializedData(string data) => false;
    }
}
