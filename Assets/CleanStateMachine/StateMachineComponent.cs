using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CleanStateMachine
{
    public class StateMachineComponent : MonoBehaviour
    {
        [SerializeField] private StateMachineController _controller;

        private List<BlackboardVariable> _runtimeVariables = new List<BlackboardVariable>();
        private int _currentStateIndex = -1;
        private string _currentStateName = "None";
        private bool _initialized = false;
        private float _waitTimer = 0f;
        private List<TransitionRecord> _recentTransitions = new List<TransitionRecord>();

        public StateMachineController Controller
        {
            get => _controller;
            set => _controller = value;
        }

        public SerializableData Data => _controller != null ? _controller.Data : null;

        public string CurrentStateName => _currentStateName;
        public int CurrentStateIndex => _currentStateIndex;
        public List<BlackboardVariable> RuntimeVariables => _runtimeVariables;
        public List<TransitionRecord> RecentTransitions => _recentTransitions;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized || _controller == null) return;

            CopyVariablesFromController();
            FindEntryState();
            _initialized = true;

            if (_currentStateIndex >= 0)
            {
                ExecuteSection("OnStateEnter");
            }
        }

        private void CopyVariablesFromController()
        {
            _runtimeVariables.Clear();
            if (Data != null)
            {
                for (int i = 0; i < Data.BlackboardVariables.Count; i++)
                {
                    _runtimeVariables.Add(Data.BlackboardVariables[i].Clone());
                }
            }
        }

        private void FindEntryState()
        {
            _currentStateIndex = -1;
            _currentStateName = "None";
            if (Data == null) return;
            for (int i = 0; i < Data.States.Count; i++)
            {
                if (Data.States[i].IsEntry)
                {
                    _currentStateIndex = i;
                    _currentStateName = Data.States[i].Name;
                    return;
                }
            }
        }

        private void Start()
        {
            if (!_initialized) Initialize();
        }

        private void Update()
        {
            if (!_initialized || _currentStateIndex < 0) return;

            if (_waitTimer > 0f)
            {
                _waitTimer -= Time.deltaTime;
                return;
            }

            ExecuteSection("OnStateUpdate");
            CheckTransitions();
        }

        private void ExecuteSection(string sectionName)
        {
            if (_currentStateIndex < 0 || _currentStateIndex >= Data.States.Count) return;

            var state = Data.States[_currentStateIndex];
            if (state.StateClass == null) return;

            for (int s = 0; s < state.StateClass.Sections.Count; s++)
            {
                var section = state.StateClass.Sections[s];
                if (section.SectionName != sectionName) continue;

                for (int e = 0; e < section.Events.Count; e++)
                {
                    var evt = section.Events[e];
                    switch (evt.Type)
                    {
                        case StateMachineEventType.DebugLog:
                            Debug.Log(evt.DebugMessage);
                            break;

                        case StateMachineEventType.Wait:
                            _waitTimer = evt.WaitDuration;
                            return;

                        case StateMachineEventType.UnityEvent:
                            for (int c = 0; c < evt.UnityEventCallbacks.Count; c++)
                            {
                                var callback = evt.UnityEventCallbacks[c];
                                if (callback.Target != null && !string.IsNullOrEmpty(callback.MethodName))
                                {
                                    var targetType = callback.Target.GetType();
                                    var method = targetType.GetMethod(callback.MethodName,
                                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (method != null)
                                    {
                                        method.Invoke(callback.Target, null);
                                    }
                                }
                            }
                            break;
                    }
                }
            }
        }

        private void CheckTransitions()
        {
            if (Data == null || _currentStateIndex < 0) return;

            for (int c = 0; c < Data.Connections.Count; c++)
            {
                var connection = Data.Connections[c];
                if (connection.FromIndex != _currentStateIndex) continue;
                if (connection.Conditions.Count == 0) continue;

                bool allMet = true;
                for (int d = 0; d < connection.Conditions.Count; d++)
                {
                    if (!EvaluateCondition(connection.Conditions[d]))
                    {
                        allMet = false;
                        break;
                    }
                }

                if (allMet)
                {
                    TransitionToState(connection.ToIndex);
                    break;
                }
            }
        }

        private void TransitionToState(int toIndex)
        {
            if (toIndex < 0 || toIndex >= Data.States.Count) return;

            int fromIndex = _currentStateIndex;

            ExecuteSection("OnStateExit");

            _currentStateIndex = toIndex;
            _currentStateName = Data.States[toIndex].Name;
            _waitTimer = 0f;

            _recentTransitions.Add(new TransitionRecord
            {
                FromIndex = fromIndex,
                ToIndex = toIndex
            });

            ExecuteSection("OnStateEnter");
        }

        private bool EvaluateCondition(TransitionCondition condition)
        {
            var variable = _runtimeVariables.Find(v => v.Name == condition.BlackboardVariableName);
            if (variable == null) return false;

            switch (variable.Type)
            {
                case BlackboardVariableType.Bool:
                {
                    bool val = variable.BoolValue;
                    bool.TryParse(condition.CompareValue, out bool compareVal);
                    return condition.Comparison switch
                    {
                        ConditionComparison.EqualTo => val == compareVal,
                        ConditionComparison.NotEqualTo => val != compareVal,
                        _ => false
                    };
                }
                case BlackboardVariableType.Int:
                {
                    int val = variable.IntValue;
                    int.TryParse(condition.CompareValue, out int compareVal);
                    return condition.Comparison switch
                    {
                        ConditionComparison.EqualTo => val == compareVal,
                        ConditionComparison.NotEqualTo => val != compareVal,
                        ConditionComparison.GreaterThan => val > compareVal,
                        ConditionComparison.LessThan => val < compareVal,
                        ConditionComparison.GreaterOrEqual => val >= compareVal,
                        ConditionComparison.LessOrEqual => val <= compareVal,
                        _ => false
                    };
                }
                case BlackboardVariableType.Float:
                {
                    float val = variable.FloatValue;
                    float.TryParse(condition.CompareValue, out float compareVal);
                    return condition.Comparison switch
                    {
                        ConditionComparison.EqualTo => Mathf.Approximately(val, compareVal),
                        ConditionComparison.NotEqualTo => !Mathf.Approximately(val, compareVal),
                        ConditionComparison.GreaterThan => val > compareVal,
                        ConditionComparison.LessThan => val < compareVal,
                        ConditionComparison.GreaterOrEqual => val >= compareVal,
                        ConditionComparison.LessOrEqual => val <= compareVal,
                        _ => false
                    };
                }
                case BlackboardVariableType.String:
                {
                    string val = variable.StringValue;
                    return condition.Comparison switch
                    {
                        ConditionComparison.EqualTo => val == condition.CompareValue,
                        ConditionComparison.NotEqualTo => val != condition.CompareValue,
                        _ => false
                    };
                }
            }

            return false;
        }

        public void SetBoolParameter(string name, bool value)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.Bool)
                {
                    v.BoolValue = value;
                    return;
                }
            }
        }

        public void SetIntParameter(string name, int value)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.Int)
                {
                    v.IntValue = value;
                    return;
                }
            }
        }

        public void SetFloatParameter(string name, float value)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.Float)
                {
                    v.FloatValue = value;
                    return;
                }
            }
        }

        public void SetStringParameter(string name, string value)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.String)
                {
                    v.StringValue = value;
                    return;
                }
            }
        }

        public void SetVector2Parameter(string name, Vector2 value)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.Vector2)
                {
                    v.Vector2Value = value;
                    return;
                }
            }
        }

        public void SetVector3Parameter(string name, Vector3 value)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.Vector3)
                {
                    v.Vector3Value = value;
                    return;
                }
            }
        }

        public bool GetBoolParameter(string name)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.Bool)
                    return v.BoolValue;
            }
            return false;
        }

        public int GetIntParameter(string name)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.Int)
                    return v.IntValue;
            }
            return 0;
        }

        public float GetFloatParameter(string name)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.Float)
                    return v.FloatValue;
            }
            return 0f;
        }

        public string GetStringParameter(string name)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.String)
                    return v.StringValue;
            }
            return "";
        }

        public Vector2 GetVector2Parameter(string name)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.Vector2)
                    return v.Vector2Value;
            }
            return Vector2.zero;
        }

        public Vector3 GetVector3Parameter(string name)
        {
            for (int i = 0; i < _runtimeVariables.Count; i++)
            {
                var v = _runtimeVariables[i];
                if (v.Name == name && v.Type == BlackboardVariableType.Vector3)
                    return v.Vector3Value;
            }
            return Vector3.zero;
        }

        public void ResetStateMachine()
        {
            _initialized = false;
            _waitTimer = 0f;
            _recentTransitions.Clear();
            Initialize();
        }

        [System.Serializable]
        public class TransitionRecord
        {
            public int FromIndex;
            public int ToIndex;
        }
    }
}
