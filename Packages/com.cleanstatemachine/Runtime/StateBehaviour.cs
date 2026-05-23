using UnityEngine;

namespace CleanStateMachine
{
    public abstract class StateBehaviour : ScriptableObject
    {
        public virtual void OnStateEnter(StateMachineComponent stateMachine) { }
        public virtual void OnStateUpdate(StateMachineComponent stateMachine) { }
        public virtual void OnStateExit(StateMachineComponent stateMachine) { }
    }
}
