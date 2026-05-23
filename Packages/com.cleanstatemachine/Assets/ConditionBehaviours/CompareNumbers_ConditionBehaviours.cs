using UnityEngine;
using CleanStateMachine;

public class CompareNumbers_ConditionBehaviours : ConditionScript
{
    public enum ComparisonType
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual
    }

    public CleanStateMachine.BlackboardVariableReference number1 = new CleanStateMachine.BlackboardVariableReference
    {
        ValueType = CleanStateMachine.BlackboardVariableType.Int,
        DefaultValue = "0"
    };
    public CleanStateMachine.BlackboardVariableReference number2 = new CleanStateMachine.BlackboardVariableReference
    {
        ValueType = CleanStateMachine.BlackboardVariableType.Int,
        DefaultValue = "0"
    };
    public ComparisonType comparison = ComparisonType.Equal;

    public override string DisplayName => "Compare Numbers";

    private static float GetValue(CleanStateMachine.BlackboardVariableReference variable, StateMachineComponent stateMachine)
    {
        return variable.ValueType == CleanStateMachine.BlackboardVariableType.Int
            ? variable.GetIntValue(stateMachine)
            : variable.GetFloatValue(stateMachine);
    }

    public override bool Evaluate(StateMachineComponent stateMachine)
    {
        float a = GetValue(number1, stateMachine);
        float b = GetValue(number2, stateMachine);

        return comparison switch
        {
            ComparisonType.Equal => a == b,
            ComparisonType.NotEqual => a != b,
            ComparisonType.GreaterThan => a > b,
            ComparisonType.GreaterThanOrEqual => a >= b,
            ComparisonType.LessThan => a < b,
            ComparisonType.LessThanOrEqual => a <= b,
            _ => false
        };
    }
}
