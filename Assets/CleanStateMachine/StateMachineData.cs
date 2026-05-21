using System;
using System.Collections.Generic;
using UnityEngine;

namespace CleanStateMachine
{
    public enum StateMachineEventType
    {
        DebugLog,
        Wait,
        UnityEvent,
        Custom
    }

    [Serializable]
    public class UnityEventCallbackData
    {
        public UnityEngine.Object Target;
        public string MethodName = "";
    }

    [Serializable]
    public class StateMachineEventData
    {
        [System.NonSerialized] public int EditorId;
        public StateMachineEventType Type;
        public string DebugMessage = "";
        public float WaitDuration;
        public List<UnityEventCallbackData> UnityEventCallbacks = new List<UnityEventCallbackData>();
        public string CustomText = "";
    }

    [Serializable]
    public class StateSectionData
    {
        public string SectionName = "";
        public List<StateMachineEventData> Events = new List<StateMachineEventData>();
    }

    [Serializable]
    public class StateClassData
    {
        public List<StateSectionData> Sections;

        public StateClassData()
        {
            Sections = new List<StateSectionData>
            {
                new StateSectionData { SectionName = "OnStateEnter" },
                new StateSectionData { SectionName = "OnStateUpdate" },
                new StateSectionData { SectionName = "OnStateExit" },
            };
        }
    }

    public enum ConditionComparison
    {
        IsTrue,
        IsFalse,
        GreaterThan,
        LessThan,
        EqualTo,
        NotEqualTo,
        GreaterOrEqual,
        LessOrEqual
    }

    [Serializable]
    public class TransitionCondition
    {
        public string BlackboardVariableName = "";
        public ConditionComparison Comparison;
        public string CompareValue = "";
    }

    [Serializable]
    public class StateData
    {
        public string Name = "New State";
        public Vector2 Position;
        public Vector2 Size = new Vector2(160f, 40f);
        public bool IsEntry;
        public StateClassData StateClass;
    }

    [Serializable]
    public class ConnectionData
    {
        public int FromIndex;
        public int ToIndex;
        public List<TransitionCondition> Conditions = new List<TransitionCondition>();
    }

    [Serializable]
    public class GroupData
    {
        public string Label = "Group";
        public List<int> MemberIndices = new List<int>();
    }

    [Serializable]
    public class SerializableData
    {
        public List<StateData> States = new List<StateData>();
        public List<ConnectionData> Connections = new List<ConnectionData>();
        public List<GroupData> Groups = new List<GroupData>();
        public List<BlackboardVariable> BlackboardVariables = new List<BlackboardVariable>();
        public Vector2 PanOffset;
        public float Zoom = 1f;
        public bool ShowBlackboard = true;
        public float BlackboardWidth = 220f;
        public float DetailsWidth = 220f;
    }
}
