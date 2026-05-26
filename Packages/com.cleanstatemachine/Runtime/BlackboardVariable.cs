using System;

namespace CleanStateMachine
{
    public enum BlackboardVariableType
    {
        Bool,
        Int,
        Float,
        String,
        Trigger,
    }

    [Serializable]
    public class BlackboardVariable
    {
        public string Name = "New Variable";
        public BlackboardVariableType Type = BlackboardVariableType.Float;
        public string StringValue = "0";

        public bool BoolValue
        {
            get => bool.TryParse(StringValue, out var v) ? v : false;
            set => StringValue = value.ToString();
        }

        public int IntValue
        {
            get => int.TryParse(StringValue, out var v) ? v : 0;
            set => StringValue = value.ToString();
        }

        public float FloatValue
        {
            get => float.TryParse(StringValue, out var v) ? v : 0f;
            set => StringValue = value.ToString("G");
        }

        public bool TriggerValue
        {
            get => bool.TryParse(StringValue, out var v) && v;
            set => StringValue = value.ToString();
        }

        public BlackboardVariable Clone()
        {
            return new BlackboardVariable
            {
                Name = Name,
                Type = Type,
                StringValue = StringValue
            };
        }
    }
}
