using StateMachineTool.Runtime;
using UnityEditor;
using UnityEngine;

namespace StateMachineTool.Editor
{
    public static class StateMachineMenuItems
    {
        [MenuItem("Assets/Create/State Machine Tool/State Machine Asset", false, 100)]
        public static void CreateStateMachineAsset()
        {
            var asset = ScriptableObject.CreateInstance<StateMachineAsset>();
            asset.name = "NewStateMachine";

            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/NewStateMachine.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;

            StateMachineEditorWindow.OpenAsset(asset);
        }

        [MenuItem("GameObject/State Machine/State Machine Runner", false, 10)]
        public static void CreateStateMachineRunner()
        {
            var go = new GameObject("State Machine Runner");
            go.AddComponent<StateMachineRunner>();
            Selection.activeGameObject = go;

            Undo.RegisterCreatedObjectUndo(go, "Create State Machine Runner");
        }

        [MenuItem("Window/State Machine Editor", false, 200)]
        public static void OpenEditorWindow()
        {
            StateMachineEditorWindow.ShowWindow();
        }
    }
}
