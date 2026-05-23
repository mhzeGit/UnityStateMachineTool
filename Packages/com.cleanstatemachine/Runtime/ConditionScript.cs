using UnityEngine;

namespace CleanStateMachine
{
    public abstract class ConditionScript : ScriptableObject
    {
        public abstract bool Evaluate(StateMachineComponent stateMachine);
    }
}
