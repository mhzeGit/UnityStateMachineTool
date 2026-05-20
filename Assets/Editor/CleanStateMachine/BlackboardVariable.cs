using System;
using System.Globalization;
using UnityEngine;

namespace CleanStateMachine
{
    public enum BlackboardVariableType
    {
        Bool,
        Int,
        Float,
        String,
        Vector2,
        Vector3,
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

        public Vector2 Vector2Value
        {
            get => TryParseVector2(StringValue, out var v) ? v : Vector2.zero;
            set => StringValue = $"{value.x:G},{value.y:G}";
        }

        public Vector3 Vector3Value
        {
            get => TryParseVector3(StringValue, out var v) ? v : Vector3.zero;
            set => StringValue = $"{value.x:G},{value.y:G},{value.z:G}";
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
