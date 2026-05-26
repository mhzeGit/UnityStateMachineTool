using System.Globalization;

namespace CleanStateMachine
{
    [System.Serializable]
    public class BlackboardVariableReference
    {
        public bool UseBlackboard;
        public string BlackboardVariableName;
        public BlackboardVariableType ValueType = BlackboardVariableType.Float;
        public string DefaultValue = "";

        public string GetStringValue(StateMachineComponent sm)
        {
            if (UseBlackboard && !string.IsNullOrEmpty(BlackboardVariableName))
                return sm.GetStringParameter(BlackboardVariableName);
            return DefaultValue;
        }

        public bool GetBoolValue(StateMachineComponent sm)
        {
            if (UseBlackboard && !string.IsNullOrEmpty(BlackboardVariableName))
                return sm.GetBoolParameter(BlackboardVariableName);
            return bool.TryParse(DefaultValue, out var v) && v;
        }

        public int GetIntValue(StateMachineComponent sm)
        {
            if (UseBlackboard && !string.IsNullOrEmpty(BlackboardVariableName))
                return sm.GetIntParameter(BlackboardVariableName);
            return int.TryParse(DefaultValue, out var v) ? v : 0;
        }

        public float GetFloatValue(StateMachineComponent sm)
        {
            if (UseBlackboard && !string.IsNullOrEmpty(BlackboardVariableName))
                return sm.GetFloatParameter(BlackboardVariableName);
            return float.TryParse(DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }

        public bool GetTriggerValue(StateMachineComponent sm)
        {
            if (UseBlackboard && !string.IsNullOrEmpty(BlackboardVariableName))
                return sm.GetTriggerParameter(BlackboardVariableName);
            return bool.TryParse(DefaultValue, out var v) && v;
        }
    }
}
