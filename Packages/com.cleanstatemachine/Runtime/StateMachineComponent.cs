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
        private readonly List<int> _activeStatePath = new List<int>();
        private List<TransitionRecord> _recentTransitions = new List<TransitionRecord>();
        private bool _isTransitioning;

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
                int idx = CurrentStateIndex;
                if (Data == null || idx < 0 || idx >= Data.States.Count)
                    return "None";
                return Data.States[idx].Name;
            }
        }

        public int CurrentStateIndex
        {
            get
            {
                if (_activeStatePath.Count == 0) return -1;
                return _activeStatePath[^1];
            }
        }

        public IReadOnlyList<int> CurrentStatePath => _activeStatePath;

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
                int leafIndex = CurrentStateIndex;
                string initialStateName = CurrentStateName;
                OnStateChanged?.Invoke(-1, leafIndex);
                OnStateEntered?.Invoke(initialStateName);
                EnterPathBehaviours();
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

            for (int i = 0; i < Data.States.Count; i++)
            {
                if (Data.States[i].IsEntry)
                {
                    BuildFullPath(i);
                    return;
                }
            }
        }

        private void BuildFullPath(int targetIndex)
        {
            _activeStatePath.Clear();

            var chain = new List<int>();
            int current = targetIndex;
            chain.Add(current);

            while (true)
            {
                int parent = FindParentContaining(current);
                if (parent < 0) break;
                chain.Add(parent);
                current = parent;
            }

            for (int i = chain.Count - 1; i >= 0; i--)
                _activeStatePath.Add(chain[i]);

            ResolveSubEntriesDownward(targetIndex);
        }

        private int FindParentContaining(int childIndex)
        {
            if (Data == null) return -1;
            for (int i = 0; i < Data.States.Count; i++)
            {
                if (Data.States[i].IsSubStateMachine &&
                    Data.States[i].ChildIndices != null &&
                    Data.States[i].ChildIndices.Contains(childIndex))
                    return i;
            }
            return -1;
        }

        private void ResolveSubEntriesDownward(int stateIndex)
        {
            if (Data == null || stateIndex < 0 || stateIndex >= Data.States.Count) return;
            var state = Data.States[stateIndex];
            if (!state.IsSubStateMachine) return;

            int subEntry = FindSubEntry(state);
            if (subEntry >= 0)
            {
                _activeStatePath.Add(subEntry);
                ResolveSubEntriesDownward(subEntry);
            }
        }

        private int FindSubEntry(StateData parentState)
        {
            if (!parentState.IsSubStateMachine || parentState.ChildIndices == null || parentState.ChildIndices.Count == 0)
                return -1;

            for (int i = 0; i < parentState.ChildIndices.Count; i++)
            {
                int childIdx = parentState.ChildIndices[i];
                if (childIdx >= 0 && childIdx < Data.States.Count && Data.States[childIdx].IsSubEntry)
                    return childIdx;
            }

            return parentState.ChildIndices[0];
        }

        private void Start()
        {
            if (!_initialized) Initialize();
        }

        private void Update()
        {
            if (!_initialized || _activeStatePath.Count == 0) return;

            int leafIndex = CurrentStateIndex;
            var leafBehaviour = GetBehaviour(leafIndex);
            if (leafBehaviour != null)
                leafBehaviour.OnStateUpdate(this);

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
            if (_isTransitioning) return;
            if (Data == null || _activeStatePath.Count == 0) return;

            for (int depth = _activeStatePath.Count - 1; depth >= 0; depth--)
            {
                int fromIndex = _activeStatePath[depth];
                bool isLeaf = depth == _activeStatePath.Count - 1;

                for (int c = 0; c < Data.Connections.Count; c++)
                {
                    var connection = Data.Connections[c];
                    if (connection.FromIndex != fromIndex) continue;

                    if (EvaluateConditions(connection))
                    {
                        if (!isLeaf && IsDirectChildOf(connection.ToIndex, fromIndex))
                            continue;

                        _isTransitioning = true;
                        try
                        {
                            TransitionToState(c);
                        }
                        finally
                        {
                            _isTransitioning = false;
                        }
                        return;
                    }
                }
            }
        }

        private bool IsDirectChildOf(int childIndex, int parentIndex)
        {
            if (Data == null) return false;
            if (parentIndex < 0 || parentIndex >= Data.States.Count) return false;

            var parent = Data.States[parentIndex];
            if (!parent.IsSubStateMachine || parent.ChildIndices == null)
                return false;

            return parent.ChildIndices.Contains(childIndex);
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

            int fromLeaf = CurrentStateIndex;
            string previousLeafName = CurrentStateName;

            var oldPath = new List<int>(_activeStatePath);

            BuildFullPath(toIndex);

            int commonDepth = 0;
            while (commonDepth < oldPath.Count &&
                   commonDepth < _activeStatePath.Count &&
                   oldPath[commonDepth] == _activeStatePath[commonDepth])
                commonDepth++;

            if (commonDepth == oldPath.Count && commonDepth == _activeStatePath.Count)
            {
                if (connection.FromIndex != connection.ToIndex)
                    return;
                commonDepth = _activeStatePath.Count - 1;
            }

            for (int i = oldPath.Count - 1; i >= commonDepth; i--)
            {
                int idx = oldPath[i];
                if (idx >= 0 && idx < Data.States.Count)
                {
                    var stateData = Data.States[idx];
                    var behaviour = GetOrCreateBehaviour(stateData);
                    if (behaviour != null)
                        behaviour.OnStateExit(this);
                }
            }

            OnStateExited?.Invoke(previousLeafName);

            _stateEnterTime = Time.time;

            int newLeaf = CurrentStateIndex;
            string newLeafName = CurrentStateName;
            _recentTransitions.Add(new TransitionRecord
            {
                FromIndex = fromLeaf,
                ToIndex = toIndex,
                ConnectionIndex = connectionIndex
            });

            OnStateChanged?.Invoke(fromLeaf, newLeaf);
            OnStateEntered?.Invoke(newLeafName);

            for (int i = commonDepth; i < _activeStatePath.Count; i++)
            {
                int idx = _activeStatePath[i];
                if (idx >= 0 && idx < Data.States.Count)
                {
                    var stateData = Data.States[idx];
                    var behaviour = GetOrCreateBehaviour(stateData);
                    if (behaviour != null)
                        behaviour.OnStateEnter(this);
                }
            }
        }

        private void EnterPathBehaviours()
        {
            for (int i = 0; i < _activeStatePath.Count; i++)
            {
                int idx = _activeStatePath[i];
                if (idx >= 0 && idx < Data.States.Count)
                {
                    var stateData = Data.States[idx];
                    var behaviour = GetOrCreateBehaviour(stateData);
                    if (behaviour != null)
                        behaviour.OnStateEnter(this);
                }
            }
        }

        private void ExitPathBehaviours()
        {
            for (int i = _activeStatePath.Count - 1; i >= 0; i--)
            {
                int idx = _activeStatePath[i];
                if (idx >= 0 && idx < Data.States.Count)
                {
                    var stateData = Data.States[idx];
                    var behaviour = GetOrCreateBehaviour(stateData);
                    if (behaviour != null)
                        behaviour.OnStateExit(this);
                }
            }
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
