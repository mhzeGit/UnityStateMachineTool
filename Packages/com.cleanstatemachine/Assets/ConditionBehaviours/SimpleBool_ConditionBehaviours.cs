using UnityEngine;
using CleanStateMachine;

public class SimpleBool_ConditionBehaviours : ConditionScript
{
    public CleanStateMachine.BlackboardVariableReference conditionBool = new CleanStateMachine.BlackboardVariableReference
    {
        ValueType = CleanStateMachine.BlackboardVariableType.Bool,
        DefaultValue = "True"
    };
    public CleanStateMachine.BlackboardVariableReference isInverse = new CleanStateMachine.BlackboardVariableReference
    {
        ValueType = CleanStateMachine.BlackboardVariableType.Bool,
        DefaultValue = "False"
    };

    public override string DisplayName => "Simple Bool";

    public override bool Evaluate(StateMachineComponent stateMachine)
    {
        bool result = conditionBool.GetBoolValue(stateMachine);
        return isInverse.GetBoolValue(stateMachine) ? !result : result;
    }
}
