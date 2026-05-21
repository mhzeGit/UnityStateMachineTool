using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    [CustomEditor(typeof(StateMachineController))]
    public class StateMachineControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (StateMachineController)target;

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Open in Graph Editor", GUILayout.Height(30)))
            {
                CleanStateMachineWindow.OpenWithController(controller);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Assign StateBehaviour scripts to states and ConditionScript scripts to transitions " +
                "in the Graph Editor. Their public fields can be edited on the " +
                "StateMachineComponent inspector attached to any GameObject using this controller.",
                MessageType.Info);
        }
    }
}
