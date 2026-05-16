using System.Collections.Generic;
using UnityEngine;

namespace StateMachineTool.Runtime
{
    public class StateMachineAsset : ScriptableObject
    {
        public StateMachineGraphData graphData = new StateMachineGraphData();

        public StateData GetState(string stateId)
        {
            foreach (var state in graphData.states)
            {
                if (state.id == stateId)
                    return state;
            }
            return null;
        }

        public StateData GetEntryState()
        {
            if (!string.IsNullOrEmpty(graphData.entryStateId))
                return GetState(graphData.entryStateId);

            foreach (var state in graphData.states)
            {
                if (state.stateType == StateType.Entry)
                    return state;
            }
            return null;
        }

        public List<TransitionData> GetTransitionsFrom(string stateId)
        {
            var result = new System.Collections.Generic.List<TransitionData>();
            foreach (var t in graphData.transitions)
            {
                if (t.fromStateId == stateId)
                    result.Add(t);
            }
            result.Sort((a, b) => a.priority.CompareTo(b.priority));
            return result;
        }

        public List<TransitionData> GetTransitionsFromAny()
        {
            var result = new System.Collections.Generic.List<TransitionData>();
            foreach (var t in graphData.transitions)
            {
                var fromState = GetState(t.fromStateId);
                if (fromState != null && fromState.stateType == StateType.Any)
                    result.Add(t);
            }
            return result;
        }
    }
}
