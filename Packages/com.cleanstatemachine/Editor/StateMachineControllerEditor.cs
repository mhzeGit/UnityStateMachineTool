using UnityEditor;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    [CustomEditor(typeof(StateMachineController))]
    public class StateMachineControllerEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("controller-inspector");

            var styleSheet = ScriptReferenceUtility.LoadStyleSheet("ControllerInspector");
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            var openBtn = new Button(() =>
            {
                var controller = (StateMachineController)target;
                CleanStateMachineWindow.OpenWithController(controller);
            });
            openBtn.text = "Open in Graph Editor";
            openBtn.AddToClassList("controller-open-button");
            root.Add(openBtn);

            var helpText = new Label(
                "Assign StateBehaviour scripts to states and ConditionScript scripts to transitions " +
                "in the Graph Editor. Their public fields can be edited on the " +
                "StateMachineComponent inspector attached to any GameObject using this controller.");
            helpText.AddToClassList("controller-help-text");
            root.Add(helpText);

            return root;
        }
    }
}
