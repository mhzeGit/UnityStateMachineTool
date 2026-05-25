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
        private List<TransitionRecord> _recentTransitions = new List<TransitionRecord>();

        private readonly Dictionary<StateData, StateBehaviour> _behaviourInstances = new Dictionary<StateData, StateBehaviour>();
        private readonly List<ConditionScript> _runtimeConditionInstances = new List<ConditionScript>();

        private readonly List<StateLevel> _activeStatePath = new List<StateLevel>();

        private struct StateLevel
        {
            public SerializableData Data;
            public int StateIndex;
        }

        public StateMachineController Controller
        {
            get => _controller;
            set => _controller = value;
        }

        public SerializableData Data => _controller != null ? _controller.Data : null;

        public int ActiveStateDepth => _activeStatePath.Count;

        public int GetStateIndexAtDepth(int depth)
        {
            if (depth < 0 || depth >= _activeStatePath.Count) return -1;
            return _activeStatePath[depth].StateIndex;
        }

        public string CurrentStateName
        {
            get
            {
                if (_activeStatePath.Count == 0) return "None";
                var leaf = _activeStatePath[^1];
                return leaf.Data.States[leaf.StateIndex].Name;
            }
        }

        public int CurrentStateIndex
        {
            get
            {
                if (_activeStatePath.Count == 0) return -1;
                return _activeStatePath[^1].StateIndex;
            }
        }

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
            BuildEntryPath();
            _initialized = true;

            if (_activeStatePath.Count > 0)
            {
                _stateEnterTime = Time.time;
                string initialStateName = CurrentStateName;
                OnStateChanged?.Invoke(-1, CurrentStateIndex);
                OnStateEntered?.Invoke(initialStateName);
                var leafBehaviour = GetLeafBehaviour();
                if (leafBehaviour != null)
                    leafBehaviour.OnStateEnter(this);
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

        private void BuildEntryPath()
        {
            _activeStatePath.Clear();
            if (Data == null) return;

            var currentData = Data;
            while (currentData != null)
            {
                int entryIndex = -1;
                for (int i = 0; i < currentData.States.Count; i++)
                {
                    if (currentData.States[i].IsEntry)
                    {
                        entryIndex = i;
                        break;
                    }
                }
                if (entryIndex < 0) break;

                _activeStatePath.Add(new StateLevel { Data = currentData, StateIndex = entryIndex });

                var state = currentData.States[entryIndex];
                if (state.IsSubStateMachine && state.SubMachineData != null)
                    currentData = state.SubMachineData;
                else
                    break;
            }
        }

        private void Start()
        {
            if (!_initialized) Initialize();
        }

        private void Update()
        {
            if (!_initialized || _activeStatePath.Count == 0) return;

            var leafBehaviour = GetLeafBehaviour();
            if (leafBehaviour != null)
                leafBehaviour.OnStateUpdate(this);

            CheckTransitions();
        }

        private StateBehaviour GetLeafBehaviour()
        {
            if (_activeStatePath.Count == 0) return null;
            var leaf = _activeStatePath[^1];
            var stateData = leaf.Data.States[leaf.StateIndex];
            return stateData.IsSubStateMachine ? null : GetOrCreateBehaviour(stateData);
        }

        private StateBehaviour GetOrCreateBehaviour(StateData state)
        {
            if (state.IsSubStateMachine)
                return null;

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
            if (Data == null || _activeStatePath.Count == 0) return;

            for (int level = _activeStatePath.Count - 1; level >= 0; level--)
            {
                if (level + 1 < _activeStatePath.Count)
                {
                    var childState = _activeStatePath[level + 1].Data.States[_activeStatePath[level + 1].StateIndex];
                    if (!childState.IsEntry)
                        continue;
                }

                var levelData = _activeStatePath[level].Data;
                var levelIndex = _activeStatePath[level].StateIndex;

                for (int c = 0; c < levelData.Connections.Count; c++)
                {
                    var connection = levelData.Connections[c];
                    if (connection.FromIndex != levelIndex) continue;

                    if (EvaluateConditions(connection))
                    {
                        TransitionToState(level, c);
                        return;
                    }
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

        private void TransitionToState(int transitionLevel, int connectionIndex)
        {
            var transitionData = _activeStatePath[transitionLevel].Data;
            var connection = transitionData.Connections[connectionIndex];
            int toIndex = connection.ToIndex;

            if (toIndex < 0 || toIndex >= transitionData.States.Count) return;

            int fromIndex = _activeStatePath[transitionLevel].StateIndex;
            string previousLeafName = CurrentStateName;

            for (int i = _activeStatePath.Count - 1; i >= transitionLevel; i--)
            {
                var sd = _activeStatePath[i].Data.States[_activeStatePath[i].StateIndex];
                if (!sd.IsSubStateMachine)
                {
                    var behaviour = GetOrCreateBehaviour(sd);
                    if (behaviour != null)
                        behaviour.OnStateExit(this);
                }
            }

            OnStateExited?.Invoke(previousLeafName);

            while (_activeStatePath.Count > transitionLevel)
                _activeStatePath.RemoveAt(_activeStatePath.Count - 1);

            _activeStatePath.Add(new StateLevel
            {
                Data = transitionData,
                StateIndex = toIndex
            });

            var targetState = transitionData.States[toIndex];
            if (targetState.IsSubStateMachine && targetState.SubMachineData != null)
                DescendIntoSubMachine(targetState.SubMachineData);

            _stateEnterTime = Time.time;

            string newLeafName = CurrentStateName;
            _recentTransitions.Add(new TransitionRecord
            {
                FromIndex = fromIndex,
                ToIndex = toIndex,
                ConnectionIndex = connectionIndex,
                Level = transitionLevel
            });

            OnStateChanged?.Invoke(fromIndex, toIndex);
            OnStateEntered?.Invoke(newLeafName);

            var newLeafBehaviour = GetLeafBehaviour();
            if (newLeafBehaviour != null)
                newLeafBehaviour.OnStateEnter(this);
        }

        private void DescendIntoSubMachine(SerializableData subData)
        {
            int entryIndex = -1;
            for (int i = 0; i < subData.States.Count; i++)
            {
                if (subData.States[i].IsEntry)
                {
                    entryIndex = i;
                    break;
                }
            }
            if (entryIndex < 0) return;

            _activeStatePath.Add(new StateLevel
            {
                Data = subData,
                StateIndex = entryIndex
            });

            var entryState = subData.States[entryIndex];
            if (entryState.IsSubStateMachine && entryState.SubMachineData != null)
                DescendIntoSubMachine(entryState.SubMachineData);
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
            public int Level;
        }
    }
}
