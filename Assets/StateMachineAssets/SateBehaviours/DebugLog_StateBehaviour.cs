using UnityEngine;
using CleanStateMachine;

public class NewStateBehaviour : StateBehaviour
{
    public string message = "Hello World";
    public override void OnStateEnter(StateMachineComponent stateMachine)
    {
        Debug.Log(message);
        base.OnStateEnter(stateMachine);
    }

    public override void OnStateUpdate(StateMachineComponent stateMachine)
    {
        base.OnStateUpdate(stateMachine);
    }

    public override void OnStateExit(StateMachineComponent stateMachine)
    {
        base.OnStateExit(stateMachine);
    }
}
