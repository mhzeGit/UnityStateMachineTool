using UnityEditor;
using UnityEngine;

namespace CleanStateMachine
{
    [CustomEditor(typeof(StateMachineComponent))]
    public class StateMachineComponentEditor : Editor
    {
        private SerializedProperty _controllerProp;
        private StateMachineComponent _component;
        private bool _variablesFoldout = true;
        private Vector2 _scrollPos;

        private void OnEnable()
        {
            _controllerProp = serializedObject.FindProperty("_controller");
            _component = (StateMachineComponent)target;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_component != null && Application.isPlaying)
            {
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_controllerProp);
            serializedObject.ApplyModifiedProperties();

            if (_component.Controller == null)
            {
                EditorGUILayout.HelpBox("Assign a State Machine Controller to begin.", MessageType.Info);
                return;
            }

            DrawCurrentState();

            EditorGUILayout.Space(4);

            DrawVariablesSection();
        }

        private void DrawCurrentState()
        {
            string stateName = _component.CurrentStateName;
            if (string.IsNullOrEmpty(stateName))
                stateName = "None";

            Color stateColor = stateName != "None" ? Color.green : Color.gray;

            var labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.fontSize = 12;

            var stateStyle = new GUIStyle(EditorStyles.boldLabel);
            stateStyle.fontSize = 12;
            stateStyle.normal.textColor = stateColor;

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current State", labelStyle, GUILayout.Width(EditorGUIUtility.labelWidth - 4));
            EditorGUILayout.LabelField(stateName, stateStyle);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private void DrawVariablesSection()
        {
            bool isPlaying = Application.isPlaying;
            var variables = isPlaying
                ? _component.RuntimeVariables
                : _component.Controller.Data.BlackboardVariables;

            int count = variables?.Count ?? 0;

            if (count == 0)
                _variablesFoldout = false;

            _variablesFoldout = EditorGUILayout.Foldout(_variablesFoldout, $"Blackboard Variables ({count})", true);
            if (!_variablesFoldout) return;

            if (count == 0)
            {
                EditorGUILayout.LabelField("No blackboard variables defined.", EditorStyles.miniLabel);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MaxHeight(250));

            for (int i = 0; i < count; i++)
            {
                DrawVariableRow(variables[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawVariableRow(BlackboardVariable variable)
        {
            EditorGUILayout.BeginHorizontal();

            string typeLabel = GetTypeShortName(variable.Type);
            var badgeStyle = new GUIStyle(EditorStyles.miniLabel);
            badgeStyle.normal.textColor = new Color(0.48f, 0.56f, 0.70f);
            badgeStyle.fontStyle = FontStyle.Bold;
            GUILayout.Label(typeLabel, badgeStyle, GUILayout.Width(34));

            var nameStyle = new GUIStyle(EditorStyles.label);
            nameStyle.fontSize = 11;
            nameStyle.clipping = TextClipping.Clip;
            EditorGUILayout.LabelField(variable.Name, nameStyle, GUILayout.Width(EditorGUIUtility.labelWidth - 50));

            DrawValueField(variable);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawValueField(BlackboardVariable variable)
        {
            bool changed = false;

            switch (variable.Type)
            {
                case BlackboardVariableType.Bool:
                {
                    bool val = variable.BoolValue;
                    bool result = EditorGUILayout.Toggle(val);
                    if (result != val)
                    {
                        variable.BoolValue = result;
                        changed = true;
                    }
                    break;
                }
                case BlackboardVariableType.Int:
                {
                    int val = variable.IntValue;
                    int result = EditorGUILayout.IntField(val);
                    if (result != val)
                    {
                        variable.IntValue = result;
                        changed = true;
                    }
                    break;
                }
                case BlackboardVariableType.Float:
                {
                    float val = variable.FloatValue;
                    float result = EditorGUILayout.FloatField(val);
                    if (!Mathf.Approximately(result, val))
                    {
                        variable.FloatValue = result;
                        changed = true;
                    }
                    break;
                }
                case BlackboardVariableType.String:
                {
                    string val = variable.StringValue;
                    string result = EditorGUILayout.TextField(val);
                    if (result != val)
                    {
                        variable.StringValue = result;
                        changed = true;
                    }
                    break;
                }
                case BlackboardVariableType.Vector2:
                {
                    Vector2 val = variable.Vector2Value;
                    Vector2 result = EditorGUILayout.Vector2Field(GUIContent.none, val);
                    if (result != val)
                    {
                        variable.Vector2Value = result;
                        changed = true;
                    }
                    break;
                }
                case BlackboardVariableType.Vector3:
                {
                    Vector3 val = variable.Vector3Value;
                    Vector3 result = EditorGUILayout.Vector3Field(GUIContent.none, val);
                    if (result != val)
                    {
                        variable.Vector3Value = result;
                        changed = true;
                    }
                    break;
                }
            }

            if (changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(_component.Controller);
                EditorUtility.SetDirty(_component);
            }
        }

        private static string GetTypeShortName(BlackboardVariableType type)
        {
            return type switch
            {
                BlackboardVariableType.Bool => "bool",
                BlackboardVariableType.Int => "int",
                BlackboardVariableType.Float => "float",
                BlackboardVariableType.String => "string",
                BlackboardVariableType.Vector2 => "V2",
                BlackboardVariableType.Vector3 => "V3",
                _ => type.ToString()
            };
        }
    }
}
