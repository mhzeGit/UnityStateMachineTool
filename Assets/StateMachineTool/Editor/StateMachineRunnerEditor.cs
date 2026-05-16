using StateMachineTool.Runtime;
using UnityEditor;
using UnityEngine;

namespace StateMachineTool.Editor
{
    [CustomEditor(typeof(StateMachineRunner))]
    public class StateMachineRunnerEditor : UnityEditor.Editor
    {
        private StateMachineRunner runner;
        private bool showRuntimeInfo = true;

        private void OnEnable()
        {
            runner = target as StateMachineRunner;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("stateMachineAsset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("startOnAwake"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debugLogTransitions"));

            serializedObject.ApplyModifiedProperties();

            if (runner.Asset == null)
            {
                EditorGUILayout.HelpBox("Assign a State Machine Asset to begin.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(10);

            if (Application.isPlaying)
            {
                showRuntimeInfo = EditorGUILayout.Foldout(showRuntimeInfo, "Runtime Info", true);
                if (showRuntimeInfo)
                {
                    DrawRuntimeInfo();
                }
            }

            if (GUILayout.Button("Open in State Machine Editor"))
            {
                StateMachineEditorWindow.OpenAsset(runner.Asset);
            }

            if (GUILayout.Button("Open Editor Window"))
            {
                StateMachineEditorWindow.ShowWindow();
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Restart"))
                    runner.Restart();
                if (GUILayout.Button("Stop"))
                    runner.Stop();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRuntimeInfo()
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Is Running", runner.IsRunning.ToString());

            if (runner.CurrentState != null)
            {
                EditorGUILayout.LabelField("Current State", runner.CurrentState.displayName);
                EditorGUILayout.LabelField("State Type", runner.CurrentState.stateType.ToString());

                if (!string.IsNullOrEmpty(runner.CurrentState.comment))
                    EditorGUILayout.HelpBox(runner.CurrentState.comment, MessageType.None);

                EditorGUILayout.LabelField("On Enter Actions", runner.CurrentState.onEnterActions?.Count.ToString() ?? "0");
                EditorGUILayout.LabelField("On Update Actions", runner.CurrentState.onUpdateActions?.Count.ToString() ?? "0");
                EditorGUILayout.LabelField("On Exit Actions", runner.CurrentState.onExitActions?.Count.ToString() ?? "0");
            }

            if (runner.Asset != null)
            {
                var transitions = runner.Asset.GetTransitionsFrom(runner.CurrentState?.id);
                EditorGUILayout.LabelField("Available Transitions", transitions.Count.ToString());

                if (transitions.Count > 0)
                {
                    EditorGUILayout.Space(2);
                    foreach (var t in transitions)
                    {
                        var toState = runner.Asset.GetState(t.toStateId);
                        EditorGUILayout.LabelField($"  -> {toState?.displayName ?? "?"}",
                            $"Priority: {t.priority} | Conditions: {t.conditions.Count}");
                    }
                }
            }

            // Blackboard runtime values
            if (runner.Blackboard != null)
            {
                EditorGUILayout.Space(5);
                var bbFoldout = EditorGUILayout.Foldout(true, "Blackboard Variables");
                if (bbFoldout)
                {
                    EditorGUI.indentLevel++;
                    foreach (var variable in runner.Asset.graphData.blackboard.variables)
                    {
                        var key = variable.key;
                        switch (variable.type)
                        {
                            case BlackboardValueType.Int:
                                EditorGUILayout.LabelField(key, runner.Blackboard.GetInt(key).ToString());
                                break;
                            case BlackboardValueType.Float:
                                EditorGUILayout.LabelField(key, runner.Blackboard.GetFloat(key).ToString("F2"));
                                break;
                            case BlackboardValueType.Bool:
                                EditorGUILayout.LabelField(key, runner.Blackboard.GetBool(key).ToString());
                                break;
                            case BlackboardValueType.String:
                                EditorGUILayout.LabelField(key, runner.Blackboard.GetString(key));
                                break;
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
        }
    }
}
