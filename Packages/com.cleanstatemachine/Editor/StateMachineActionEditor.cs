using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    [CustomEditor(typeof(StateMachineAction), true)]
    public class StateMachineActionEditor : Editor
    {
        private StateMachineAction _action;
        private StateMachineComponent _lastComponent;
        private StateMachineController _lastController;
        private VisualElement _root;
        private PropertyField _stateMachineField;
        private VisualElement _pickerArea;
        private VisualElement _selectionDisplay;
        private Label _helpLabel;
        private Button _dropdownBtn;
        private double _nextUpdateTime;
        private int _lastVariableCount = -1;
        private string _lastVariableHash = "";
        private SerializedProperty _variableNameProp;
        private SerializedProperty _variableTypeProp;

        public override VisualElement CreateInspectorGUI()
        {
            _action = (StateMachineAction)target;
            _root = new VisualElement();
            _root.AddToClassList("sm-action-inspector");

            var styleSheet = ScriptReferenceUtility.LoadStyleSheet("StateMachineActionInspector");
            if (styleSheet != null)
                _root.styleSheets.Add(styleSheet);

            var header = new Label("State Machine Action");
            header.AddToClassList("sm-action-header");
            _root.Add(header);

            _stateMachineField = new PropertyField(serializedObject.FindProperty("_stateMachine"));
            _stateMachineField.AddToClassList("sm-action-sm-field");
            _root.Add(_stateMachineField);

            _pickerArea = new VisualElement();
            _pickerArea.AddToClassList("sm-action-picker-area");
            _root.Add(_pickerArea);

            _selectionDisplay = new VisualElement();
            _selectionDisplay.AddToClassList("sm-action-selection-display");
            _root.Add(_selectionDisplay);

            _helpLabel = new Label("Assign a State Machine Component to select a blackboard variable.");
            _helpLabel.AddToClassList("sm-action-help");
            _root.Add(_helpLabel);

            _variableNameProp = serializedObject.FindProperty("_blackboardVariableName");
            _variableTypeProp = serializedObject.FindProperty("_blackboardVariableType");

            _dropdownBtn = new Button();
            _dropdownBtn.AddToClassList("sm-action-dropdown-btn");

            var component = _action.StateMachine;
            if (component != null && component.Controller != null)
            {
                _lastComponent = component;
                _lastController = component.Controller;
            }

            UpdatePickerVisibility();
            UpdateSelectionDisplay();

            _root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EditorApplication.update += OnEditorUpdate;
            });
            _root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                EditorApplication.update -= OnEditorUpdate;
            });

            return _root;
        }

        private void OnEditorUpdate()
        {
            if (_action == null) return;

            serializedObject.Update();

            var component = _action.StateMachine;
            var controller = component != null ? component.Controller : null;

            if (component != _lastComponent)
            {
                _lastComponent = component;
                _lastController = controller;
                OnComponentChanged();
                return;
            }

            if (controller != _lastController)
            {
                _lastController = controller;
                OnControllerChanged();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextUpdateTime) return;
            _nextUpdateTime = now + 0.5;

            if (controller != null && controller.Data != null)
            {
                var variables = controller.Data.BlackboardVariables;
                if (HaveVariablesChanged(variables))
                {
                    UpdateDropdownLabel();
                }
            }
        }

        private void OnComponentChanged()
        {
            var component = _action.StateMachine;
            var controller = component != null ? component.Controller : null;
            _lastController = controller;
            OnControllerChanged();
        }

        private void OnControllerChanged()
        {
            string oldName = _variableNameProp.stringValue;
            if (_lastController != null && !string.IsNullOrEmpty(oldName))
            {
                var variables = _lastController.Data.BlackboardVariables;
                bool stillExists = false;
                if (variables != null)
                {
                    for (int i = 0; i < variables.Count; i++)
                    {
                        if (variables[i].Name == oldName)
                        {
                            stillExists = true;
                            break;
                        }
                    }
                }
                if (!stillExists)
                {
                    _variableNameProp.stringValue = "";
                    serializedObject.ApplyModifiedProperties();
                }
            }

            UpdatePickerVisibility();
            UpdateSelectionDisplay();
            TrackCurrentVariables();
        }

        private void UpdatePickerVisibility()
        {
            _pickerArea.Clear();
            _helpLabel.style.display = _lastController == null ? DisplayStyle.Flex : DisplayStyle.None;

            if (_lastController == null) return;

            var requiredType = _action.RequiredVariableType;

            _dropdownBtn = new Button();
            _dropdownBtn.AddToClassList("sm-action-dropdown-btn");
            UpdateDropdownLabel();
            _dropdownBtn.clicked += ShowDropdown;

            var label = new Label("Blackboard Variable");
            label.AddToClassList("sm-action-dropdown-label");
            _pickerArea.Add(label);

            var row = new VisualElement();
            row.AddToClassList("sm-action-dropdown-row");
            var typeBadge = new Label(GetTypeShortName(requiredType));
            typeBadge.AddToClassList("sm-action-type-badge");
            row.Add(typeBadge);
            row.Add(_dropdownBtn);
            _pickerArea.Add(row);
        }

        private void ShowDropdown()
        {
            if (_lastController == null) return;

            var variables = _lastController.Data.BlackboardVariables;
            var requiredType = _action.RequiredVariableType;

            var pos = _root.WorldToLocal(
                new Vector2(_dropdownBtn.worldBound.x, _dropdownBtn.worldBound.y + _dropdownBtn.worldBound.height));

            MenuDropdown.Show(_root, pos, menu =>
            {
                menu.AddItem("None", () =>
                {
                    _variableNameProp.stringValue = "";
                    serializedObject.ApplyModifiedProperties();
                    UpdateDropdownLabel();
                    UpdateSelectionDisplay();
                });

                menu.AddSeparator();

                bool hasMatch = false;
                if (variables != null)
                {
                    for (int i = 0; i < variables.Count; i++)
                    {
                        if (variables[i].Type != requiredType) continue;
                        hasMatch = true;
                        string capturedName = variables[i].Name;
                        BlackboardVariableType capturedType = variables[i].Type;
                        menu.AddItem(capturedName, () =>
                        {
                            _variableNameProp.stringValue = capturedName;
                            _variableTypeProp.enumValueIndex = (int)capturedType;
                            serializedObject.ApplyModifiedProperties();
                            UpdateDropdownLabel();
                            UpdateSelectionDisplay();
                        });
                    }
                }

                if (!hasMatch)
                    menu.AddDisabledItem($"No {GetTypeShortName(requiredType)} variables");
            });
        }

        private void UpdateDropdownLabel()
        {
            string name = _variableNameProp.stringValue;
            _dropdownBtn.text = string.IsNullOrEmpty(name)
                ? "Select variable..."
                : name;
        }

        private void UpdateSelectionDisplay()
        {
            _selectionDisplay.Clear();

            string name = _variableNameProp.stringValue;
            if (string.IsNullOrEmpty(name))
            {
                _selectionDisplay.style.display = DisplayStyle.None;
                return;
            }

            _selectionDisplay.style.display = DisplayStyle.Flex;

            var card = new VisualElement();
            card.AddToClassList("sm-action-selected-card");

            var title = new Label("Selected Variable");
            title.AddToClassList("sm-action-selected-title");
            card.Add(title);

            var infoRow = new VisualElement();
            infoRow.AddToClassList("sm-action-selected-row");

            var type = (BlackboardVariableType)_variableTypeProp.enumValueIndex;
            var badge = new Label(GetTypeShortName(type));
            badge.AddToClassList("sm-action-var-badge");
            infoRow.Add(badge);

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("sm-action-selected-name");
            infoRow.Add(nameLabel);

            card.Add(infoRow);

            var clearBtn = new Button(() =>
            {
                _variableNameProp.stringValue = "";
                serializedObject.ApplyModifiedProperties();
                UpdateDropdownLabel();
                UpdateSelectionDisplay();
            });
            clearBtn.text = "Clear Selection";
            clearBtn.AddToClassList("sm-action-clear-btn");
            card.Add(clearBtn);

            _selectionDisplay.Add(card);
        }

        private void TrackCurrentVariables()
        {
            if (_lastController == null || _lastController.Data == null)
            {
                _lastVariableCount = -1;
                _lastVariableHash = "";
                return;
            }

            var variables = _lastController.Data.BlackboardVariables;
            _lastVariableCount = variables?.Count ?? 0;
            var sb = new System.Text.StringBuilder();
            if (variables != null)
            {
                for (int i = 0; i < variables.Count; i++)
                {
                    sb.Append(variables[i].Name);
                    sb.Append((int)variables[i].Type);
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
            }
            return sb.ToString() != _lastVariableHash;
        }

        private static string GetTypeShortName(BlackboardVariableType type)
        {
            return type switch
            {
                BlackboardVariableType.Bool => "bool",
                BlackboardVariableType.Int => "int",
                BlackboardVariableType.Float => "float",
                BlackboardVariableType.String => "string",
                BlackboardVariableType.Trigger => "trigger",
                _ => type.ToString()
            };
        }
    }
}
