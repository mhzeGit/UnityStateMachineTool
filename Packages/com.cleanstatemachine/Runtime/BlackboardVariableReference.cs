using System.Globalization;
using UnityEngine;

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

        public Vector2 GetVector2Value(StateMachineComponent sm)
        {
            if (UseBlackboard && !string.IsNullOrEmpty(BlackboardVariableName))
                return sm.GetVector2Parameter(BlackboardVariableName);
            return TryParseVector2(DefaultValue, out var v) ? v : Vector2.zero;
        }

        public Vector3 GetVector3Value(StateMachineComponent sm)
        {
            if (UseBlackboard && !string.IsNullOrEmpty(BlackboardVariableName))
                return sm.GetVector3Parameter(BlackboardVariableName);
            return TryParseVector3(DefaultValue, out var v) ? v : Vector3.zero;
        }

        private static bool TryParseVector2(string s, out Vector2 result)
        {
            result = Vector2.zero;
            if (string.IsNullOrEmpty(s))
                return false;
            string[] parts = s.Split(',');
            if (parts.Length != 2)
                return false;
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                result = new Vector2(x, y);
                return true;
            }
            return false;
        }

        private static bool TryParseVector3(string s, out Vector3 result)
        {
            result = Vector3.zero;
            if (string.IsNullOrEmpty(s))
                return false;
            string[] parts = s.Split(',');
            if (parts.Length != 3)
                return false;
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                result = new Vector3(x, y, z);
                return true;
            }
            return false;
        }
    }
}
