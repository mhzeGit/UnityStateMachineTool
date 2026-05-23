using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace CleanStateMachine
{
    public static class StateMachineAssetHandler
    {
        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
#pragma warning disable CS0618
            var controller = EditorUtility.InstanceIDToObject(instanceID) as StateMachineController;
#pragma warning restore CS0618
            if (controller != null)
            {
                CleanStateMachineWindow.OpenWithController(controller);
                return true;
            }
            return false;
        }
    }
}
