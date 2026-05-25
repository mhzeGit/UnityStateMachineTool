using System;
using System.Collections.Generic;
using UnityEngine;

namespace CleanStateMachine
{
    [Serializable]
    public class StateData
    {
        public string Name = "New State";
        public Vector2 Position;
        public Vector2 Size = new Vector2(160f, 40f);
        public bool IsEntry;
        public bool IsSubEntry;
        public bool IsSubStateMachine;
        public List<int> ChildIndices = new List<int>();
        public string BehaviourType;
        public StateBehaviour Behaviour;
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
        public Vector2 PanOffset;
        public float Zoom = 1f;
        public bool ShowSidePanel = true;
        public float SidePanelWidth = 220f;
        public float DetailsHeightRatio = 0.5f;
    }
}
