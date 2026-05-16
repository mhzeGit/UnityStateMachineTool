using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace StateMachineTool.Editor
{
    public class TransitionEdgeView : Edge
    {
        public string TransitionId { get; private set; }

        public TransitionEdgeView(string transitionId)
        {
            TransitionId = transitionId;
            AddToClassList("transition-edge");
        }
    }
}
