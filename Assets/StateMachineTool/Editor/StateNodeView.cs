using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace StateMachineTool.Editor
{
    public class StateNodeView : Node
    {
        public string StateId { get; private set; }
        public StateMachineGraphView GraphView { get; private set; }

        private Label typeLabel;

        public StateNodeView(StateMachineGraphView graphView, Runtime.StateData stateData)
        {
            GraphView = graphView;
            StateId = stateData.id;

            viewDataKey = stateData.id;
            title = stateData.displayName;

            SetPosition(new Rect(stateData.position, new Vector2(200, 100)));
            capabilities |= Capabilities.Movable | Capabilities.Selectable | Capabilities.Deletable;

            AddToClassList("state-node");

            SetupPorts(stateData.stateType);
            SetupTypeBadge(stateData.stateType);
            RefreshStyle(stateData.stateType);
        }

        private void SetupPorts(Runtime.StateType stateType)
        {
            if (stateType != Runtime.StateType.Entry)
            {
                var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                inputPort.portName = "In";
                inputPort.name = "in-port";
                inputContainer.Add(inputPort);
            }

            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            outputPort.portName = "Out";
            outputPort.name = "out-port";
            outputContainer.Add(outputPort);
        }

        private void SetupTypeBadge(Runtime.StateType stateType)
        {
            typeLabel = new Label(stateType.ToString());
            typeLabel.AddToClassList("state-type-badge");
            titleButtonContainer.Add(typeLabel);
        }

        public void RefreshStyle(Runtime.StateType stateType)
        {
            RemoveFromClassList("state-entry");
            RemoveFromClassList("state-normal");
            RemoveFromClassList("state-any");

            switch (stateType)
            {
                case Runtime.StateType.Entry:
                    AddToClassList("state-entry");
                    break;
                case Runtime.StateType.Any:
                    AddToClassList("state-any");
                    break;
                default:
                    AddToClassList("state-normal");
                    break;
            }

            if (typeLabel != null)
                typeLabel.text = stateType.ToString();
        }

        public void UpdateFromData(Runtime.StateData data)
        {
            title = data.displayName;
            RefreshStyle(data.stateType);
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);

            var stateData = GraphView.Asset.GetState(StateId);
            if (stateData != null)
            {
                stateData.position = newPos.position;
                EditorUtility.SetDirty(GraphView.Asset);
            }
        }

        public Port GetInputPort()
        {
            return inputContainer.Q<Port>("in-port");
        }

        public Port GetOutputPort()
        {
            return outputContainer.Q<Port>("out-port");
        }
    }
}
