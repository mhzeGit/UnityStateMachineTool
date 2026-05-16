using System;
using System.Collections.Generic;
using UnityEngine;

namespace StateMachineTool.Runtime
{
    [Serializable]
    public class StateMachineGraphData
    {
        public List<StateData> states = new List<StateData>();
        public List<TransitionData> transitions = new List<TransitionData>();
        public BlackboardData blackboard = new BlackboardData();
        public string entryStateId;
    }

    [Serializable]
    public class StateData
    {
        public string id;
        public string displayName;
        public Vector2 position;
        public StateType stateType = StateType.Normal;
        public string comment;

        [SerializeReference]
        public List<Action> onEnterActions = new List<Action>();

        [SerializeReference]
        public List<Action> onUpdateActions = new List<Action>();

        [SerializeReference]
        public List<Action> onExitActions = new List<Action>();

        public StateData()
        {
            id = Guid.NewGuid().ToString();
            displayName = "New State";
        }

        public StateData(string name, Vector2 pos, StateType type = StateType.Normal)
        {
            id = Guid.NewGuid().ToString();
            displayName = name;
            position = pos;
            stateType = type;
        }
    }

    public enum StateType
    {
        Normal,
        Entry,
        Any
    }

    [Serializable]
    public class TransitionData
    {
        public string id;
        public string fromStateId;
        public string toStateId;
        public string displayName;
        public TransitionPriority priority = TransitionPriority.Normal;

        [SerializeReference]
        public List<Condition> conditions = new List<Condition>();

        public TransitionData()
        {
            id = Guid.NewGuid().ToString();
        }

        public TransitionData(string fromId, string toId)
        {
            id = Guid.NewGuid().ToString();
            fromStateId = fromId;
            toStateId = toId;
        }
    }

    public enum TransitionPriority
    {
        Highest = 0,
        High = 1,
        Normal = 2,
        Low = 3,
        Lowest = 4
    }

    [Serializable]
    public class BlackboardData
    {
        public List<BlackboardVariable> variables = new List<BlackboardVariable>();
        public List<BlackboardEvent> events = new List<BlackboardEvent>();
    }

    [Serializable]
    public class BlackboardVariable
    {
        public string key;
        public BlackboardValueType type;

        public int intValue;
        public float floatValue;
        public bool boolValue;
        public string stringValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public UnityEngine.Object objectValue;

        public object GetValue()
        {
            switch (type)
            {
                case BlackboardValueType.Int: return intValue;
                case BlackboardValueType.Float: return floatValue;
                case BlackboardValueType.Bool: return boolValue;
                case BlackboardValueType.String: return stringValue;
                case BlackboardValueType.Vector2: return vector2Value;
                case BlackboardValueType.Vector3: return vector3Value;
                case BlackboardValueType.Object: return objectValue;
                default: return null;
            }
        }

        public void SetValue(object value)
        {
            switch (type)
            {
                case BlackboardValueType.Int: intValue = (int)value; break;
                case BlackboardValueType.Float: floatValue = (float)value; break;
                case BlackboardValueType.Bool: boolValue = (bool)value; break;
                case BlackboardValueType.String: stringValue = (string)value; break;
                case BlackboardValueType.Vector2: vector2Value = (Vector2)value; break;
                case BlackboardValueType.Vector3: vector3Value = (Vector3)value; break;
                case BlackboardValueType.Object: objectValue = (UnityEngine.Object)value; break;
            }
        }
    }

    public enum BlackboardValueType
    {
        Int,
        Float,
        Bool,
        String,
        Vector2,
        Vector3,
        Object
    }

    [Serializable]
    public class BlackboardEvent
    {
        public string key;
        public string displayName;
    }
}
