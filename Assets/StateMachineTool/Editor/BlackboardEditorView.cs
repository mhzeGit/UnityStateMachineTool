using System;
using System.Collections.Generic;
using System.Linq;
using StateMachineTool.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace StateMachineTool.Editor
{
    public class BlackboardEditorView : VisualElement
    {
        private StateMachineAsset asset;
        private ListView variableListView;
        private ListView eventListView;
        private VisualElement variableEditPanel;
        private VisualElement eventEditPanel;

        private BlackboardVariable selectedVariable;
        private BlackboardEvent selectedEvent;

        public System.Action OnChanged;

        public BlackboardEditorView()
        {
            AddToClassList("blackboard-editor");
            BuildUI();
        }

        public void LoadAsset(StateMachineAsset targetAsset)
        {
            asset = targetAsset;
            if (asset == null) return;
            Refresh();
        }

        private void BuildUI()
        {
            var header = new Label("BLACKBOARD");
            header.AddToClassList("blackboard-header");
            Add(header);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;

            // --- Variables Section ---
            var variablesHeader = new Label("Variables");
            variablesHeader.AddToClassList("blackboard-section-header");
            scrollView.Add(variablesHeader);

            var addVarBtn = new Button(() => AddVariable());
            addVarBtn.text = "+ Add Variable";
            addVarBtn.AddToClassList("blackboard-add-btn");
            scrollView.Add(addVarBtn);

            var varContainer = new VisualElement();
            varContainer.AddToClassList("blackboard-list-container");
            scrollView.Add(varContainer);

            variableListView = new ListView();
            variableListView.AddToClassList("blackboard-list");
            variableListView.fixedItemHeight = 24;
            variableListView.makeItem = () =>
            {
                var label = new Label();
                label.AddToClassList("blackboard-list-item");
                return label;
            };
            variableListView.bindItem = (element, index) =>
            {
                if (index >= asset.graphData.blackboard.variables.Count) return;
                var variable = asset.graphData.blackboard.variables[index];
                var label = element as Label;
                if (label != null)
                    label.text = $"{variable.key} ({variable.type})";
            };
            variableListView.selectionChanged += OnVariableSelectionChanged;
            variableListView.selectionType = SelectionType.Single;
            varContainer.Add(variableListView);

            // Variable Edit Panel
            variableEditPanel = new VisualElement();
            variableEditPanel.AddToClassList("blackboard-edit-panel");
            variableEditPanel.style.display = DisplayStyle.None;
            scrollView.Add(variableEditPanel);

            // --- Events Section ---
            var eventsHeader = new Label("Events");
            eventsHeader.AddToClassList("blackboard-section-header");
            scrollView.Add(eventsHeader);

            var addEventBtn = new Button(() => AddEvent());
            addEventBtn.text = "+ Add Event";
            addEventBtn.AddToClassList("blackboard-add-btn");
            scrollView.Add(addEventBtn);

            var eventContainer = new VisualElement();
            eventContainer.AddToClassList("blackboard-list-container");
            scrollView.Add(eventContainer);

            eventListView = new ListView();
            eventListView.AddToClassList("blackboard-list");
            eventListView.fixedItemHeight = 24;
            eventListView.makeItem = () =>
            {
                var label = new Label();
                label.AddToClassList("blackboard-list-item");
                return label;
            };
            eventListView.bindItem = (element, index) =>
            {
                if (index >= asset.graphData.blackboard.events.Count) return;
                var evt = asset.graphData.blackboard.events[index];
                var label = element as Label;
                if (label != null)
                    label.text = evt.key;
            };
            eventListView.selectionChanged += OnEventSelectionChanged;
            eventListView.selectionType = SelectionType.Single;
            eventContainer.Add(eventListView);

            // Event Edit Panel
            eventEditPanel = new VisualElement();
            eventEditPanel.AddToClassList("blackboard-edit-panel");
            eventEditPanel.style.display = DisplayStyle.None;
            scrollView.Add(eventEditPanel);

            Add(scrollView);
        }

        public void Refresh()
        {
            if (asset == null) return;
            variableListView.itemsSource = asset.graphData.blackboard.variables;
            variableListView.RefreshItems();
            eventListView.itemsSource = asset.graphData.blackboard.events;
            eventListView.RefreshItems();
        }

        private void AddVariable()
        {
            var variable = new BlackboardVariable
            {
                key = $"Variable {asset.graphData.blackboard.variables.Count}",
                type = BlackboardValueType.Bool
            };
            asset.graphData.blackboard.variables.Add(variable);
            EditorUtility.SetDirty(asset);
            Refresh();
            variableListView.selectedIndex = asset.graphData.blackboard.variables.Count - 1;
            OnChanged?.Invoke();
        }

        private void AddEvent()
        {
            var evt = new BlackboardEvent
            {
                key = $"Event {asset.graphData.blackboard.events.Count}",
                displayName = $"Event {asset.graphData.blackboard.events.Count}"
            };
            asset.graphData.blackboard.events.Add(evt);
            EditorUtility.SetDirty(asset);
            Refresh();
            eventListView.selectedIndex = asset.graphData.blackboard.events.Count - 1;
            OnChanged?.Invoke();
        }

        private void OnVariableSelectionChanged(IEnumerable<object> selectedItems)
        {
            var selected = selectedItems?.FirstOrDefault() as BlackboardVariable;
            selectedVariable = selected;
            BuildVariableEditPanel();
        }

        private void OnEventSelectionChanged(IEnumerable<object> selectedItems)
        {
            var selected = selectedItems?.FirstOrDefault() as BlackboardEvent;
            selectedEvent = selected;
            BuildEventEditPanel();
        }

        private void BuildVariableEditPanel()
        {
            variableEditPanel.Clear();
            variableEditPanel.style.display = DisplayStyle.None;

            if (selectedVariable == null) return;

            variableEditPanel.style.display = DisplayStyle.Flex;

            var titleLabel = new Label($"Edit Variable: {selectedVariable.key}");
            titleLabel.AddToClassList("edit-panel-title");
            variableEditPanel.Add(titleLabel);

            var keyField = new TextField("Key");
            keyField.value = selectedVariable.key;
            keyField.RegisterValueChangedCallback(evt =>
            {
                selectedVariable.key = evt.newValue;
                EditorUtility.SetDirty(asset);
                Refresh();
            });
            variableEditPanel.Add(keyField);

            var typeField = new EnumField("Type", selectedVariable.type);
            typeField.RegisterValueChangedCallback(evt =>
            {
                selectedVariable.type = (BlackboardValueType)evt.newValue;
                EditorUtility.SetDirty(asset);
                Refresh();
            });
            variableEditPanel.Add(typeField);

            BuildValueField(variableEditPanel, selectedVariable);

            var deleteBtn = new Button(() =>
            {
                asset.graphData.blackboard.variables.Remove(selectedVariable);
                selectedVariable = null;
                EditorUtility.SetDirty(asset);
                Refresh();
                OnChanged?.Invoke();
            });
            deleteBtn.text = "Delete Variable";
            deleteBtn.AddToClassList("delete-btn");
            variableEditPanel.Add(deleteBtn);
        }

        private void BuildValueField(VisualElement parent, BlackboardVariable variable)
        {
            switch (variable.type)
            {
                case BlackboardValueType.Int:
                    var intField = new IntegerField("Default Value");
                    intField.value = variable.intValue;
                    intField.RegisterValueChangedCallback(evt =>
                    {
                        variable.intValue = evt.newValue;
                        EditorUtility.SetDirty(asset);
                    });
                    parent.Add(intField);
                    break;
                case BlackboardValueType.Float:
                    var floatField = new FloatField("Default Value");
                    floatField.value = variable.floatValue;
                    floatField.RegisterValueChangedCallback(evt =>
                    {
                        variable.floatValue = evt.newValue;
                        EditorUtility.SetDirty(asset);
                    });
                    parent.Add(floatField);
                    break;
                case BlackboardValueType.Bool:
                    var boolField = new Toggle("Default Value");
                    boolField.value = variable.boolValue;
                    boolField.RegisterValueChangedCallback(evt =>
                    {
                        variable.boolValue = evt.newValue;
                        EditorUtility.SetDirty(asset);
                    });
                    parent.Add(boolField);
                    break;
                case BlackboardValueType.String:
                    var stringField = new TextField("Default Value");
                    stringField.value = variable.stringValue;
                    stringField.RegisterValueChangedCallback(evt =>
                    {
                        variable.stringValue = evt.newValue;
                        EditorUtility.SetDirty(asset);
                    });
                    parent.Add(stringField);
                    break;
                case BlackboardValueType.Vector2:
                    var vec2Field = new Vector2Field("Default Value");
                    vec2Field.value = variable.vector2Value;
                    vec2Field.RegisterValueChangedCallback(evt =>
                    {
                        variable.vector2Value = evt.newValue;
                        EditorUtility.SetDirty(asset);
                    });
                    parent.Add(vec2Field);
                    break;
                case BlackboardValueType.Vector3:
                    var vec3Field = new Vector3Field("Default Value");
                    vec3Field.value = variable.vector3Value;
                    vec3Field.RegisterValueChangedCallback(evt =>
                    {
                        variable.vector3Value = evt.newValue;
                        EditorUtility.SetDirty(asset);
                    });
                    parent.Add(vec3Field);
                    break;
                case BlackboardValueType.Object:
                    var objField = new ObjectField("Default Value");
                    objField.objectType = typeof(UnityEngine.Object);
                    objField.value = variable.objectValue;
                    objField.RegisterValueChangedCallback(evt =>
                    {
                        variable.objectValue = evt.newValue;
                        EditorUtility.SetDirty(asset);
                    });
                    parent.Add(objField);
                    break;
            }
        }

        private void BuildEventEditPanel()
        {
            eventEditPanel.Clear();
            eventEditPanel.style.display = DisplayStyle.None;

            if (selectedEvent == null) return;

            eventEditPanel.style.display = DisplayStyle.Flex;

            var titleLabel = new Label($"Edit Event: {selectedEvent.key}");
            titleLabel.AddToClassList("edit-panel-title");
            eventEditPanel.Add(titleLabel);

            var keyField = new TextField("Key");
            keyField.value = selectedEvent.key;
            keyField.RegisterValueChangedCallback(evt =>
            {
                selectedEvent.key = evt.newValue;
                EditorUtility.SetDirty(asset);
                Refresh();
            });
            eventEditPanel.Add(keyField);

            var displayNameField = new TextField("Display Name");
            displayNameField.value = selectedEvent.displayName;
            displayNameField.RegisterValueChangedCallback(evt =>
            {
                selectedEvent.displayName = evt.newValue;
                EditorUtility.SetDirty(asset);
                Refresh();
            });
            eventEditPanel.Add(displayNameField);

            var deleteBtn = new Button(() =>
            {
                asset.graphData.blackboard.events.Remove(selectedEvent);
                selectedEvent = null;
                EditorUtility.SetDirty(asset);
                Refresh();
                OnChanged?.Invoke();
            });
            deleteBtn.text = "Delete Event";
            deleteBtn.AddToClassList("delete-btn");
            eventEditPanel.Add(deleteBtn);
        }
    }
}
