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

    public CleanStateMachine.BlackboardVariableType number1Type = CleanStateMachine.BlackboardVariableType.Float;
    public CleanStateMachine.BlackboardVariableReference number1 = new CleanStateMachine.BlackboardVariableReference
    {
        ValueType = CleanStateMachine.BlackboardVariableType.Float,
        DefaultValue = "0"
    };
    public CleanStateMachine.BlackboardVariableType number2Type = CleanStateMachine.BlackboardVariableType.Float;
    public CleanStateMachine.BlackboardVariableReference number2 = new CleanStateMachine.BlackboardVariableReference
    {
        ValueType = CleanStateMachine.BlackboardVariableType.Float,
        DefaultValue = "0"
    };
    public ComparisonType comparison = ComparisonType.Equal;

    private static float GetValue(CleanStateMachine.BlackboardVariableReference variable, CleanStateMachine.BlackboardVariableType type, StateMachineComponent stateMachine)
    {
        return type == CleanStateMachine.BlackboardVariableType.Int
            ? variable.GetIntValue(stateMachine)
            : variable.GetFloatValue(stateMachine);
    }

    public override bool Evaluate(StateMachineComponent stateMachine)
    {
        float a = GetValue(number1, number1Type, stateMachine);
        float b = GetValue(number2, number2Type, stateMachine);

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
