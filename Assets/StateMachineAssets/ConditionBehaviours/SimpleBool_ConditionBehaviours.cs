using UnityEngine;
using CleanStateMachine;

public class SimpleBool_ConditionBehaviours : ConditionScript
{
    public bool Bool = true;
    public override bool Evaluate(StateMachineComponent stateMachine)
    {
        return Bool;
    }
}
