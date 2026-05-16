using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using SM = StateMachineTool.Runtime;
using RuntimeAction = StateMachineTool.Runtime.Action;

namespace StateMachineTool.Editor
{
    public class StateInspectorView : VisualElement
    {
        private SM.StateMachineAsset asset;
        private SM.StateData selectedState;
        private SM.TransitionData selectedTransition;
        private VisualElement inspectorContent;
        private ScrollView actionListScroll;
        private System.Action<RuntimeAction> onActionModified;

        public System.Action OnChanged;

        public StateInspectorView()
        {
            AddToClassList("state-inspector");
            BuildUI();
        }

        public void LoadAsset(SM.StateMachineAsset targetAsset)
        {
            asset = targetAsset;
        }

        public void ShowState(SM.StateData stateData)
        {
            selectedState = stateData;
            selectedTransition = null;
            BuildStateUI();
        }

        public void ShowTransition(SM.TransitionData transitionData)
        {
            selectedTransition = transitionData;
            selectedState = null;
            BuildTransitionUI();
        }

        public void ClearSelection()
        {
            selectedState = null;
            selectedTransition = null;
            BuildEmptyUI();
        }

        private void BuildUI()
        {
            var header = new Label("INSPECTOR");
            header.AddToClassList("inspector-header");
            Add(header);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            Add(scrollView);

            inspectorContent = new VisualElement();
            inspectorContent.AddToClassList("inspector-content");
            scrollView.Add(inspectorContent);

            BuildEmptyUI();
        }

        private void BuildEmptyUI()
        {
            inspectorContent.Clear();
            var label = new Label("Select a state or transition to inspect.");
            label.AddToClassList("inspector-empty-label");
            inspectorContent.Add(label);
        }

        private void BuildStateUI()
        {
            inspectorContent.Clear();
            if (selectedState == null) return;

            var stateLabel = new Label("STATE");
            stateLabel.AddToClassList("edit-panel-title");
            inspectorContent.Add(stateLabel);

            var nameField = new TextField("Name");
            nameField.value = selectedState.displayName;
            nameField.RegisterValueChangedCallback(evt =>
            {
                selectedState.displayName = evt.newValue;
                EditorUtility.SetDirty(asset);
                OnChanged?.Invoke();
            });
            inspectorContent.Add(nameField);

            var typeField = new EnumField("Type", selectedState.stateType);
            typeField.RegisterValueChangedCallback(evt =>
            {
                selectedState.stateType = (SM.StateType)evt.newValue;
                if (selectedState.stateType == SM.StateType.Entry)
                    asset.graphData.entryStateId = selectedState.id;
                EditorUtility.SetDirty(asset);
                OnChanged?.Invoke();
            });
            inspectorContent.Add(typeField);

            var commentField = new TextField("Comment");
            commentField.value = selectedState.comment ?? string.Empty;
            commentField.multiline = true;
            commentField.style.height = 60;
            commentField.RegisterValueChangedCallback(evt =>
            {
                selectedState.comment = evt.newValue;
                EditorUtility.SetDirty(asset);
            });
            inspectorContent.Add(commentField);

            inspectorContent.Add(BuildActionSection("On Enter", selectedState.onEnterActions));
            inspectorContent.Add(BuildActionSection("On Update", selectedState.onUpdateActions));
            inspectorContent.Add(BuildActionSection("On Exit", selectedState.onExitActions));

            var deleteBtn = new Button(() =>
            {
                asset.graphData.transitions.RemoveAll(t => t.fromStateId == selectedState.id || t.toStateId == selectedState.id);
                asset.graphData.states.Remove(selectedState);
                selectedState = null;
                EditorUtility.SetDirty(asset);
                OnChanged?.Invoke();
            });
            deleteBtn.text = "Delete State";
            deleteBtn.AddToClassList("delete-btn");
            inspectorContent.Add(deleteBtn);
        }

        private VisualElement BuildActionSection(string title, List<RuntimeAction> actions)
        {
            var section = new Foldout();
            section.text = title;
            section.value = false;

            var actionContainer = new VisualElement();
            actionContainer.style.paddingLeft = 8;
            actionContainer.style.paddingTop = 4;
            actionContainer.style.paddingBottom = 4;

            for (int i = 0; i < actions.Count; i++)
            {
                int index = i;
                var action = actions[index];
                var actionRow = new VisualElement();
                actionRow.style.flexDirection = FlexDirection.Row;
                actionRow.style.alignItems = Align.Center;
                actionRow.style.marginBottom = 2;

                var actionLabel = new Label($"#{index}: {action.GetType().Name}");
                actionLabel.style.flexGrow = 1;
                actionLabel.style.fontSize = 11;
                actionRow.Add(actionLabel);

                var removeBtn = new Button(() =>
                {
                    actions.RemoveAt(index);
                    EditorUtility.SetDirty(asset);
                    OnChanged?.Invoke();
                    BuildStateUI();
                });
                removeBtn.text = "X";
                removeBtn.style.width = 22;
                removeBtn.style.height = 20;
                removeBtn.style.fontSize = 10;
                actionRow.Add(removeBtn);

                actionContainer.Add(actionRow);
            }

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.SpaceEvenly;
            buttonRow.style.marginTop = 4;

            buttonRow.Add(CreateAddActionButton("+ Log", () => actions.Add(new SM.DebugLogAction())));
            buttonRow.Add(CreateAddActionButton("+ LogWarn", () => actions.Add(new SM.DebugLogWarningAction())));
            buttonRow.Add(CreateAddActionButton("+ LogError", () => actions.Add(new SM.DebugLogErrorAction())));
            actionContainer.Add(buttonRow);

            var buttonRow2 = new VisualElement();
            buttonRow2.style.flexDirection = FlexDirection.Row;
            buttonRow2.style.justifyContent = Justify.SpaceEvenly;
            buttonRow2.style.marginTop = 2;
            buttonRow2.Add(CreateAddActionButton("+ SetVar", () => actions.Add(new SM.SetVariableAction())));
            buttonRow2.Add(CreateAddActionButton("+ TriggerEvt", () => actions.Add(new SM.TriggerEventAction())));
            buttonRow2.Add(CreateAddActionButton("+ UnityEvent", () => actions.Add(new SM.UnityEventAction())));
            actionContainer.Add(buttonRow2);

            var buttonRow3 = new VisualElement();
            buttonRow3.style.flexDirection = FlexDirection.Row;
            buttonRow3.style.justifyContent = Justify.SpaceEvenly;
            buttonRow3.style.marginTop = 2;
            buttonRow3.Add(CreateAddActionButton("+ SetActive", () => actions.Add(new SM.SetActiveAction())));
            buttonRow3.Add(CreateAddActionButton("+ AnimBool", () => actions.Add(new SM.SetAnimatorBoolAction())));
            buttonRow3.Add(CreateAddActionButton("+ AnimFloat", () => actions.Add(new SM.SetAnimatorFloatAction())));
            actionContainer.Add(buttonRow3);

            var buttonRow4 = new VisualElement();
            buttonRow4.style.flexDirection = FlexDirection.Row;
            buttonRow4.style.justifyContent = Justify.SpaceEvenly;
            buttonRow4.style.marginTop = 2;
            buttonRow4.Add(CreateAddActionButton("+ AnimTrigger", () => actions.Add(new SM.SetAnimatorTriggerAction())));
            buttonRow4.Add(CreateAddActionButton("+ Wait", () => actions.Add(new SM.WaitAction())));
            actionContainer.Add(buttonRow4);

            section.Add(actionContainer);
            return section;
        }

        private Button CreateAddActionButton(string label, System.Action onClick)
        {
            var btn = new Button(() =>
            {
                onClick();
                EditorUtility.SetDirty(asset);
                OnChanged?.Invoke();
                BuildStateUI();
            });
            btn.text = label;
            btn.style.fontSize = 10;
            btn.style.height = 20;
            btn.style.flexGrow = 1;
            return btn;
        }

        private void BuildTransitionUI()
        {
            inspectorContent.Clear();
            if (selectedTransition == null) return;

            var transitionLabel = new Label("TRANSITION");
            transitionLabel.AddToClassList("edit-panel-title");
            inspectorContent.Add(transitionLabel);

            var fromState = asset.GetState(selectedTransition.fromStateId);
            var toState = asset.GetState(selectedTransition.toStateId);

            var infoLabel = new Label($"From: {fromState?.displayName ?? "?"}  ->  To: {toState?.displayName ?? "?"}");
            infoLabel.AddToClassList("inspector-info-label");
            inspectorContent.Add(infoLabel);

            var nameField = new TextField("Label");
            nameField.value = selectedTransition.displayName ?? string.Empty;
            nameField.RegisterValueChangedCallback(evt =>
            {
                selectedTransition.displayName = evt.newValue;
                EditorUtility.SetDirty(asset);
            });
            inspectorContent.Add(nameField);

            var priorityField = new EnumField("Priority", selectedTransition.priority);
            priorityField.RegisterValueChangedCallback(evt =>
            {
                selectedTransition.priority = (SM.TransitionPriority)evt.newValue;
                EditorUtility.SetDirty(asset);
            });
            inspectorContent.Add(priorityField);

            var conditionsFoldout = new Foldout();
            conditionsFoldout.text = $"Conditions ({selectedTransition.conditions.Count})";
            conditionsFoldout.value = true;

            var conditionContainer = new VisualElement();
            conditionContainer.style.paddingLeft = 8;
            conditionContainer.style.paddingTop = 4;
            conditionContainer.style.paddingBottom = 4;

            for (int i = 0; i < selectedTransition.conditions.Count; i++)
            {
                int index = i;
                var condition = selectedTransition.conditions[i];
                var condRow = BuildConditionRow(condition, index);
                conditionContainer.Add(condRow);
            }

            var addCondRow = new VisualElement();
            addCondRow.style.flexDirection = FlexDirection.Row;
            addCondRow.style.flexWrap = Wrap.Wrap;
            addCondRow.style.justifyContent = Justify.SpaceEvenly;
            addCondRow.style.marginTop = 4;

            addCondRow.Add(CreateAddConditionButton("+ Always", () => selectedTransition.conditions.Add(new SM.AlwaysCondition())));
            addCondRow.Add(CreateAddConditionButton("+ Bool", () => selectedTransition.conditions.Add(new SM.BoolCondition())));
            addCondRow.Add(CreateAddConditionButton("+ Int", () => selectedTransition.conditions.Add(new SM.IntCondition())));
            addCondRow.Add(CreateAddConditionButton("+ Float", () => selectedTransition.conditions.Add(new SM.FloatCondition())));
            addCondRow.Add(CreateAddConditionButton("+ String", () => selectedTransition.conditions.Add(new SM.StringCondition())));
            addCondRow.Add(CreateAddConditionButton("+ Event", () => selectedTransition.conditions.Add(new SM.EventCondition())));
            addCondRow.Add(CreateAddConditionButton("+ Cooldown", () => selectedTransition.conditions.Add(new SM.CooldownCondition())));

            conditionContainer.Add(addCondRow);
            conditionsFoldout.Add(conditionContainer);
            inspectorContent.Add(conditionsFoldout);

            var deleteBtn = new Button(() =>
            {
                asset.graphData.transitions.Remove(selectedTransition);
                selectedTransition = null;
                EditorUtility.SetDirty(asset);
                OnChanged?.Invoke();
            });
            deleteBtn.text = "Delete Transition";
            deleteBtn.AddToClassList("delete-btn");
            inspectorContent.Add(deleteBtn);
        }

        private VisualElement BuildConditionRow(SM.Condition condition, int index)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;

            var typeLabel = new Label($"#{index}: {condition.GetType().Name}");
            typeLabel.style.flexGrow = 1;
            typeLabel.style.fontSize = 11;
            row.Add(typeLabel);

            if (condition is SM.BoolCondition boolCond)
            {
                var varField = new TextField();
                varField.value = boolCond.variableKey ?? "";
                varField.style.width = 80;
                varField.RegisterValueChangedCallback(evt =>
                {
                    boolCond.variableKey = evt.newValue;
                    EditorUtility.SetDirty(asset);
                });
                row.Add(varField);

                var toggle = new Toggle();
                toggle.value = boolCond.expectedValue;
                toggle.style.marginLeft = 4;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    boolCond.expectedValue = evt.newValue;
                    EditorUtility.SetDirty(asset);
                });
                row.Add(toggle);
            }
            else if (condition is SM.IntCondition intCond)
            {
                var varField = new TextField();
                varField.value = intCond.variableKey ?? "";
                varField.style.width = 80;
                varField.RegisterValueChangedCallback(evt =>
                {
                    intCond.variableKey = evt.newValue;
                    EditorUtility.SetDirty(asset);
                });
                row.Add(varField);
            }
            else if (condition is SM.FloatCondition floatCond)
            {
                var varField = new TextField();
                varField.value = floatCond.variableKey ?? "";
                varField.style.width = 80;
                varField.RegisterValueChangedCallback(evt =>
                {
                    floatCond.variableKey = evt.newValue;
                    EditorUtility.SetDirty(asset);
                });
                row.Add(varField);
            }
            else if (condition is SM.EventCondition evtCond)
            {
                var eventField = new TextField();
                eventField.value = evtCond.eventKey ?? "";
                eventField.style.width = 100;
                eventField.RegisterValueChangedCallback(evt =>
                {
                    evtCond.eventKey = evt.newValue;
                    EditorUtility.SetDirty(asset);
                });
                row.Add(eventField);
            }

            var removeBtn = new Button(() =>
            {
                selectedTransition.conditions.RemoveAt(index);
                EditorUtility.SetDirty(asset);
                OnChanged?.Invoke();
                BuildTransitionUI();
            });
            removeBtn.text = "X";
            removeBtn.style.width = 22;
            removeBtn.style.height = 20;
            removeBtn.style.fontSize = 10;
            row.Add(removeBtn);

            return row;
        }

        private Button CreateAddConditionButton(string label, System.Action onClick)
        {
            var btn = new Button(() =>
            {
                onClick();
                EditorUtility.SetDirty(asset);
                OnChanged?.Invoke();
                BuildTransitionUI();
            });
            btn.text = label;
            btn.style.fontSize = 10;
            btn.style.height = 20;
            btn.style.flexGrow = 1;
            btn.style.marginLeft = 2;
            btn.style.marginRight = 2;
            return btn;
        }
    }
}
