using UnityEngine;
using CleanStateMachine;

public class Timer_ConditionBehaviours : ConditionScript
{
    public BlackboardVariableReference duration = new BlackboardVariableReference
    {
        ValueType = BlackboardVariableType.Float,
        DefaultValue = "1"
    };

    public override string DisplayName => "Timer";

    public override bool Evaluate(StateMachineComponent stateMachine)
    {
        float d = duration.GetFloatValue(stateMachine);
        return Time.time - stateMachine.StateEnterTime >= d;
    }
}
