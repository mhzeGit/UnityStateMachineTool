using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CleanStateMachine
{
    public enum ExternalStateMachineAction
    {
        StartStateMachine,
        SetStateByName,
        SetBlackboardParameter
    }

    [Serializable]
    public class BehaviourEntry
    {
        public string TypeName;
        public StateBehaviour Instance;
    }

    [Serializable]
    public class StateData : ISerializationCallbackReceiver
    {
        public string Name = "New State";
        public Vector2 Position;
        public Vector2 Size = new Vector2(160f, 40f);
        public bool IsEntry;
        public bool IsSubEntry;
        public bool IsSubStateMachine;
        public bool IsExternalReference;
        public bool AutoRun = true;
        public List<int> ChildIndices = new List<int>();
        public List<BehaviourEntry> Behaviours = new List<BehaviourEntry>();
        public ExternalStateMachineAction ExternalAction;
        public GameObject ExternalStateMachine;
        public string ExternalTargetStateName;
        public string ExternalBlackboardParmName;
        public BlackboardVariableType ExternalBlackboardParmType;
        public string ExternalBlackboardParmValue;

        [SerializeField] [FormerlySerializedAs("BehaviourType")] private string _legacyBehaviourType;
        [SerializeField] [FormerlySerializedAs("Behaviour")] private StateBehaviour _legacyBehaviour;

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (!string.IsNullOrEmpty(_legacyBehaviourType) || _legacyBehaviour != null)
            {
                if (Behaviours.Count == 0)
                {
                    Behaviours.Add(new BehaviourEntry
                    {
                        TypeName = _legacyBehaviourType,
                        Instance = _legacyBehaviour
                    });
                }
                _legacyBehaviourType = null;
                _legacyBehaviour = null;
            }
        }
    }

    [Serializable]
    public class ConditionEntry
    {
        public string TypeName;
        public ConditionScript Instance;
    }

    [Serializable]
    public class ConnectionData
    {
        public int FromIndex;
        public int ToIndex;
        public List<ConditionEntry> Conditions = new List<ConditionEntry>();
    }

    [Serializable]
    public class GroupData
    {
        public string Label = "Group";
        public List<int> MemberIndices = new List<int>();
        public Color Color = new Color(0.18f, 0.18f, 0.18f, 0.35f);
    }

    [Serializable]
    public class SerializableData
    {
        public List<StateData> States = new List<StateData>();
        public List<ConnectionData> Connections = new List<ConnectionData>();
        public List<GroupData> Groups = new List<GroupData>();
        public List<BlackboardVariable> BlackboardVariables = new List<BlackboardVariable>();
        public List<BreakpointData> Breakpoints = new List<BreakpointData>();
        public Vector2 PanOffset;
        public float Zoom = 1f;
        public List<int> ExpandedSubStateIndices = new List<int>();
    }
}
