using UnityEngine;
using CleanStateMachine;

public class SetVariable_StateBehaviour : StateBehaviour
{
    public override string DisplayName => "Set Variable";

    public BlackboardVariableSelector target = new BlackboardVariableSelector
    {
        ValueType = BlackboardVariableType.Bool,
    };
    public BlackboardVariableReference value = new BlackboardVariableReference
    {
        ValueType = BlackboardVariableType.Bool,
        DefaultValue = "True"
    };

    public override void OnStateEnter(StateMachineComponent stateMachine)
    {
        if (string.IsNullOrEmpty(target.VariableName))
            return;

        switch (target.ValueType)
        {
            case BlackboardVariableType.Bool:
                stateMachine.SetBoolParameter(target.VariableName, value.GetBoolValue(stateMachine));
                break;
            case BlackboardVariableType.Int:
                stateMachine.SetIntParameter(target.VariableName, value.GetIntValue(stateMachine));
                break;
            case BlackboardVariableType.Float:
                stateMachine.SetFloatParameter(target.VariableName, value.GetFloatValue(stateMachine));
                break;
            case BlackboardVariableType.String:
                stateMachine.SetStringParameter(target.VariableName, value.GetStringValue(stateMachine));
                break;
            case BlackboardVariableType.Trigger:
                if (value.GetTriggerValue(stateMachine))
                    stateMachine.SetTriggerParameter(target.VariableName);
                break;
        }
    }
}
