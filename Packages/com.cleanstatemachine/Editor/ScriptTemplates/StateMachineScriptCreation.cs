using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    public static class StateMachineScriptCreation
    {
        [MenuItem("Assets/Create/Clean State Machine/State Behaviour", false, 80)]
        private static void CreateStateBehaviour()
        {
            string templatePath = ScriptReferenceUtility.FindAssetPath("StateBehaviourTemplate.txt");
            if (!string.IsNullOrEmpty(templatePath))
                ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "NewStateBehaviour.cs");
        }

        [MenuItem("Assets/Create/Clean State Machine/Condition Script", false, 81)]
        private static void CreateConditionScript()
        {
            string templatePath = ScriptReferenceUtility.FindAssetPath("ConditionScriptTemplate.txt");
            if (!string.IsNullOrEmpty(templatePath))
                ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "NewConditionScript.cs");
        }
    }
}
