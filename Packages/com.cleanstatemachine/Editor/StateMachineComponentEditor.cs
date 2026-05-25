using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    [CustomEditor(typeof(StateMachineComponent))]
    public class StateMachineComponentEditor : Editor
    {
        private StateMachineComponent _component;
        private StateMachineController _lastController;
        private VisualElement _root;
        private VisualElement _stateContainer;
        private Label _stateLabel;
        private Foldout _variablesFoldout;
        private ScrollView _variablesScroll;
        private Label _helpLabel;
        private readonly List<VisualElement> _variableRows = new();
        private double _nextUpdateTime;
        private int _lastVariableCount = -1;
        private string _lastVariableHash = "";

        public override VisualElement CreateInspectorGUI()
        {
            _component = (StateMachineComponent)target;
            _root = new VisualElement();
            _root.AddToClassList("component-inspector");

            var styleSheet = ScriptReferenceUtility.LoadStyleSheet("ComponentInspector");
            if (styleSheet != null)
                _root.styleSheets.Add(styleSheet);

            var controllerField = new PropertyField(serializedObject.FindProperty("_controller"));
            _root.Add(controllerField);

            var openGraphBtn = new Button(() =>
            {
                var controller = _component.Controller;
                if (controller != null)
                    CleanStateMachineWindow.OpenWithController(controller);
            });
            openGraphBtn.text = "Open Graph";
            openGraphBtn.SetEnabled(_component.Controller != null);
            openGraphBtn.AddToClassList("controller-open-button");
            openGraphBtn.name = "open-graph-btn";
            _root.Add(openGraphBtn);

            controllerField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                openGraphBtn.SetEnabled(_component.Controller != null);
            });

            _stateContainer = new VisualElement();
            _stateContainer.AddToClassList("state-container");

            _stateLabel = new Label("Current State: None");
            _stateLabel.AddToClassList("state-label");
            _stateContainer.Add(_stateLabel);
            _root.Add(_stateContainer);

            _variablesFoldout = new Foldout();
            _variablesFoldout.text = "Blackboard Variables (0)";
            _variablesFoldout.AddToClassList("variables-foldout");
            _variablesFoldout.value = true;

            _variablesScroll = new ScrollView();
            _variablesScroll.AddToClassList("variables-scroll");
            _variablesFoldout.Add(_variablesScroll);
            _root.Add(_variablesFoldout);

            _helpLabel = new Label("Assign a State Machine Controller to begin.");
            _helpLabel.AddToClassList("help-text");
            _root.Add(_helpLabel);

            _lastController = _component.Controller;
            SetVisibility(_lastController);
            if (_lastController != null)
                RebuildVariables(_lastController);
            UpdateStateLabel();

            _root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EditorApplication.update += OnEditorUpdate;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            });
            _root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            });

            return _root;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode &&
                change != PlayModeStateChange.EnteredEditMode)
                return;

            var controller = _component.Controller;
            if (controller != null)
                RebuildVariables(controller);
            UpdateStateLabel();
        }

        private void OnEditorUpdate()
        {
            if (_component == null) return;

            var currentController = _component.Controller;
            if (currentController != _lastController)
            {
                _lastController = currentController;
                serializedObject.Update();
                OnControllerChanged();
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextUpdateTime) return;
            _nextUpdateTime = now + 0.5;

            UpdateStateLabel();
            UpdateRuntimeVariableValues();

            if (!Application.isPlaying && currentController != null)
            {
                var currentVariables = currentController.Data.BlackboardVariables;
                if (HaveVariablesChanged(currentVariables))
                {
                    RebuildVariables(currentController);
                }
            }
        }

        private void OnControllerChanged()
        {
            serializedObject.ApplyModifiedProperties();
            var controller = _component.Controller;
            if (controller != null)
                controller.RebuildBehaviourInstances(addSubAssets: false);
            SetVisibility(controller);
            RebuildVariables(controller);
            UpdateStateLabel();
        }

        private void SetVisibility(StateMachineController controller)
        {
            bool hasController = controller != null;
            _stateContainer.style.display = hasController ? DisplayStyle.Flex : DisplayStyle.None;
            _variablesFoldout.style.display = hasController ? DisplayStyle.Flex : DisplayStyle.None;
            _helpLabel.style.display = hasController ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void UpdateStateLabel()
        {
            var controller = _component.Controller;
            if (controller == null)
            {
                _stateLabel.text = "Current State: None";
                _stateLabel.style.color = Color.gray;
                return;
            }

            string stateName = _component.CurrentStateName;
            if (string.IsNullOrEmpty(stateName))
                stateName = "None";

            _stateLabel.text = $"Current State: {stateName}";
            _stateLabel.style.color = stateName != "None" ? Color.green : Color.gray;
        }

        private void RebuildVariables(StateMachineController controller)
        {
            _variablesScroll.Clear();
            _variableRows.Clear();
            _variablesFoldout.text = "Blackboard Variables (0)";

            if (controller == null) return;

            var variables = Application.isPlaying
                ? _component.RuntimeVariables
                : controller.Data.BlackboardVariables;

            int count = variables?.Count ?? 0;
            _variablesFoldout.text = $"Blackboard Variables ({count})";

            if (count == 0)
            {
                var empty = new Label("No blackboard variables defined.");
                empty.AddToClassList("empty-variables");
                _variablesScroll.Add(empty);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var row = CreateVariableRow(variables[i]);
                _variablesScroll.Add(row);
                _variableRows.Add(row);
            }

            TrackCurrentVariables(variables);
        }

        private void TrackCurrentVariables(List<BlackboardVariable> variables)
        {
            _lastVariableCount = variables?.Count ?? 0;
            var sb = new System.Text.StringBuilder();
            if (variables != null)
            {
                for (int i = 0; i < variables.Count; i++)
                {
                    sb.Append(variables[i].Name);
                    sb.Append((int)variables[i].Type);
                    sb.Append(variables[i].StringValue);
                }
            }
            _lastVariableHash = sb.ToString();
        }

        private bool HaveVariablesChanged(List<BlackboardVariable> current)
        {
            int count = current?.Count ?? 0;
            if (count != _lastVariableCount) return true;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                sb.Append(current[i].Name);
                sb.Append((int)current[i].Type);
                sb.Append(current[i].StringValue);
            }
            return sb.ToString() != _lastVariableHash;
        }

        private VisualElement CreateVariableRow(BlackboardVariable variable)
        {
            var row = new VisualElement();
            row.AddToClassList("variable-row");

            var badge = new Label(GetTypeShortName(variable.Type));
            badge.AddToClassList("variable-badge");
            row.Add(badge);

            var nameLabel = new Label(variable.Name);
            nameLabel.AddToClassList("variable-name-label");
            row.Add(nameLabel);

            var valueContainer = new VisualElement();
            valueContainer.AddToClassList("variable-value-container");

            switch (variable.Type)
            {
                case BlackboardVariableType.Bool:
                {
                    var toggle = new Toggle();
                    toggle.value = variable.BoolValue;
                    toggle.AddToClassList("variable-field");
                    toggle.RegisterValueChangedCallback(e =>
                    {
                        variable.BoolValue = e.newValue;
                        MarkDirty();
                    });
                    valueContainer.Add(toggle);
                    break;
                }
                case BlackboardVariableType.Int:
                {
                    var field = new IntegerField();
                    field.value = variable.IntValue;
                    field.AddToClassList("variable-field");
                    field.RegisterValueChangedCallback(e =>
                    {
                        variable.IntValue = e.newValue;
                        MarkDirty();
                    });
                    valueContainer.Add(field);
                    break;
                }
                case BlackboardVariableType.Float:
                {
                    var field = new FloatField();
                    field.value = variable.FloatValue;
                    field.AddToClassList("variable-field");
                    field.RegisterValueChangedCallback(e =>
                    {
                        variable.FloatValue = e.newValue;
                        MarkDirty();
                    });
                    valueContainer.Add(field);
                    break;
                }
                case BlackboardVariableType.String:
                {
                    var field = new TextField();
                    field.value = variable.StringValue;
                    field.AddToClassList("variable-field");
                    field.RegisterValueChangedCallback(e =>
                    {
                        variable.StringValue = e.newValue;
                        MarkDirty();
                    });
                    valueContainer.Add(field);
                    break;
                }
                case BlackboardVariableType.Vector2:
                {
                    var field = new Vector2Field();
                    field.value = variable.Vector2Value;
                    field.AddToClassList("variable-field");
                    field.RegisterValueChangedCallback(e =>
                    {
                        variable.Vector2Value = e.newValue;
                        MarkDirty();
                    });
                    valueContainer.Add(field);
                    break;
                }
                case BlackboardVariableType.Vector3:
                {
                    var field = new Vector3Field();
                    field.value = variable.Vector3Value;
                    field.AddToClassList("variable-field");
                    field.RegisterValueChangedCallback(e =>
                    {
                        variable.Vector3Value = e.newValue;
                        MarkDirty();
                    });
                    valueContainer.Add(field);
                    break;
                }
            }

            row.Add(valueContainer);
            return row;
        }

        private void UpdateRuntimeVariableValues()
        {
            if (!Application.isPlaying) return;

            var controller = _component.Controller;
            if (controller == null) return;

            var variables = _component.RuntimeVariables;
            if (variables == null) return;

            int count = Mathf.Min(variables.Count, _variableRows.Count);
            for (int i = 0; i < count; i++)
            {
                var variable = variables[i];
                var row = _variableRows[i];
                var valueContainer = row.Q<VisualElement>(className: "variable-value-container");
                if (valueContainer == null || valueContainer.childCount == 0) continue;

                var field = valueContainer[0];
                switch (variable.Type)
                {
                    case BlackboardVariableType.Bool:
                        if (field is Toggle tb && tb.value != variable.BoolValue)
                            tb.SetValueWithoutNotify(variable.BoolValue);
                        break;
                    case BlackboardVariableType.Int:
                        if (field is IntegerField intF && intF.value != variable.IntValue)
                            intF.SetValueWithoutNotify(variable.IntValue);
                        break;
                    case BlackboardVariableType.Float:
                        if (field is FloatField ff && !Mathf.Approximately(ff.value, variable.FloatValue))
                            ff.SetValueWithoutNotify(variable.FloatValue);
                        break;
                    case BlackboardVariableType.String:
                        if (field is TextField tf && tf.value != variable.StringValue)
                            tf.SetValueWithoutNotify(variable.StringValue);
                        break;
                    case BlackboardVariableType.Vector2:
                        if (field is Vector2Field v2f && v2f.value != variable.Vector2Value)
                            v2f.SetValueWithoutNotify(variable.Vector2Value);
                        break;
                    case BlackboardVariableType.Vector3:
                        if (field is Vector3Field v3f && v3f.value != variable.Vector3Value)
                            v3f.SetValueWithoutNotify(variable.Vector3Value);
                        break;
                }
            }
        }

        private void MarkDirty()
        {
            if (!Application.isPlaying && _component.Controller != null)
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
