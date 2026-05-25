using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CleanStateMachine
{
    public class StateMachineComponent : MonoBehaviour
    {
        [SerializeField] private StateMachineController _controller;

        private List<BlackboardVariable> _runtimeVariables = new List<BlackboardVariable>();
        private float _stateEnterTime;
        private bool _initialized = false;
        private int _activeStateIndex = -1;
        private List<TransitionRecord> _recentTransitions = new List<TransitionRecord>();

        private readonly Dictionary<StateData, StateBehaviour> _behaviourInstances = new Dictionary<StateData, StateBehaviour>();
        private readonly List<ConditionScript> _runtimeConditionInstances = new List<ConditionScript>();

        public StateMachineController Controller
        {
            get => _controller;
            set => _controller = value;
        }

        public SerializableData Data => _controller != null ? _controller.Data : null;

        public string CurrentStateName
        {
            get
            {
                if (Data == null || _activeStateIndex < 0 || _activeStateIndex >= Data.States.Count)
                    return "None";
                return Data.States[_activeStateIndex].Name;
            }
        }

        public int CurrentStateIndex => _activeStateIndex;

        public float StateEnterTime => _stateEnterTime;
        public List<BlackboardVariable> RuntimeVariables => _runtimeVariables;
        public List<TransitionRecord> RecentTransitions => _recentTransitions;

        public event Action<int, int> OnStateChanged;
        public event Action<string> OnStateEntered;
        public event Action<string> OnStateExited;

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

            if (_activeStateIndex >= 0)
            {
                _stateEnterTime = Time.time;
                string initialStateName = CurrentStateName;
                OnStateChanged?.Invoke(-1, _activeStateIndex);
                OnStateEntered?.Invoke(initialStateName);
                var behaviour = GetBehaviour(_activeStateIndex);
                if (behaviour != null)
                    behaviour.OnStateEnter(this);
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
            _activeStateIndex = -1;
            if (Data == null) return;

            for (int i = 0; i < Data.States.Count; i++)
            {
                if (Data.States[i].IsEntry)
                {
                    _activeStateIndex = i;
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
            if (!_initialized || _activeStateIndex < 0) return;

            var behaviour = GetBehaviour(_activeStateIndex);
            if (behaviour != null)
                behaviour.OnStateUpdate(this);

            CheckTransitions();
        }

        private StateBehaviour GetBehaviour(int stateIndex)
        {
            if (Data == null || stateIndex < 0 || stateIndex >= Data.States.Count)
                return null;

            var stateData = Data.States[stateIndex];
            return GetOrCreateBehaviour(stateData);
        }

        private StateBehaviour GetOrCreateBehaviour(StateData state)
        {
            if (state.Behaviour != null)
                return state.Behaviour;

            if (_behaviourInstances.TryGetValue(state, out var existing))
                return existing;

            if (string.IsNullOrEmpty(state.BehaviourType))
                return null;

            var type = ResolveType(state.BehaviourType);
            if (type == null || !type.IsSubclassOf(typeof(StateBehaviour)))
                return null;

            var instance = (StateBehaviour)ScriptableObject.CreateInstance(type);
            instance.name = $"{state.Name}_Behaviour";
            instance.hideFlags = HideFlags.HideAndDontSave;
            _behaviourInstances[state] = instance;
            return instance;
        }

        private void CheckTransitions()
        {
            if (Data == null || _activeStateIndex < 0) return;

            for (int c = 0; c < Data.Connections.Count; c++)
            {
                var connection = Data.Connections[c];
                if (connection.FromIndex != _activeStateIndex) continue;

                if (EvaluateConditions(connection))
                {
                    TransitionToState(c);
                    return;
                }
            }
        }

        private bool EvaluateConditions(ConnectionData connection)
        {
            if (connection.Conditions == null || connection.Conditions.Count == 0)
                return true;

            for (int i = 0; i < connection.Conditions.Count; i++)
            {
                var entry = connection.Conditions[i];
                if (entry.Instance == null && string.IsNullOrEmpty(entry.TypeName))
                    continue;

                ConditionScript condition = entry.Instance;
                if (condition == null)
                {
                    var type = ResolveType(entry.TypeName);
                    if (type == null || !type.IsSubclassOf(typeof(ConditionScript)))
                        continue;

                    condition = (ConditionScript)ScriptableObject.CreateInstance(type);
                    condition.name = $"{entry.TypeName}_Condition";
                    condition.hideFlags = HideFlags.HideAndDontSave;
                    _runtimeConditionInstances.Add(condition);
                }

                if (!condition.Evaluate(this))
                    return false;
            }
            return true;
        }

        private void TransitionToState(int connectionIndex)
        {
            var connection = Data.Connections[connectionIndex];
            int toIndex = connection.ToIndex;

            if (toIndex < 0 || toIndex >= Data.States.Count) return;

            int fromIndex = _activeStateIndex;

            string previousLeafName = CurrentStateName;

            if (fromIndex >= 0 && fromIndex < Data.States.Count)
            {
                var fromState = Data.States[fromIndex];
                if (!fromState.IsSubStateMachine)
                {
                    var behaviour = GetOrCreateBehaviour(fromState);
                    if (behaviour != null)
                        behaviour.OnStateExit(this);
                }
            }

            OnStateExited?.Invoke(previousLeafName);

            _activeStateIndex = toIndex;
            _stateEnterTime = Time.time;

            string newLeafName = CurrentStateName;
            _recentTransitions.Add(new TransitionRecord
            {
                FromIndex = fromIndex,
                ToIndex = toIndex,
                ConnectionIndex = connectionIndex
            });

            OnStateChanged?.Invoke(fromIndex, toIndex);
            OnStateEntered?.Invoke(newLeafName);

            var newBehaviour = GetBehaviour(_activeStateIndex);
            if (newBehaviour != null)
                newBehaviour.OnStateEnter(this);
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            var type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }

        private void OnDestroy()
        {
            DestroyRuntimeInstances();
        }

        private void DestroyRuntimeInstances()
        {
            foreach (var instance in _behaviourInstances.Values)
            {
                if (instance != null && instance.hideFlags == HideFlags.HideAndDontSave)
                    Destroy(instance);
            }
            _behaviourInstances.Clear();

            foreach (var instance in _runtimeConditionInstances)
            {
                if (instance != null)
                    Destroy(instance);
            }
            _runtimeConditionInstances.Clear();
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
            DestroyRuntimeInstances();
            _initialized = false;
            _recentTransitions.Clear();
            Initialize();
        }

        [System.Serializable]
        public class TransitionRecord
        {
            public int FromIndex;
            public int ToIndex;
            public int ConnectionIndex = -1;
        }
    }
}
