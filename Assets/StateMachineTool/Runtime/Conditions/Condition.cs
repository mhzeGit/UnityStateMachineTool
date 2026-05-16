using System;
using System.Collections.Generic;
using UnityEngine;

namespace StateMachineTool.Runtime
{
    [Serializable]
    public abstract class Condition
    {
        public bool negate;

        public bool Evaluate(Blackboard blackboard)
        {
            bool result = OnEvaluate(blackboard);
            return negate ? !result : result;
        }

        protected abstract bool OnEvaluate(Blackboard blackboard);
    }

    [Serializable]
    public class AlwaysCondition : Condition
    {
        protected override bool OnEvaluate(Blackboard blackboard) => true;
    }

    [Serializable]
    public class BoolCondition : Condition
    {
        public string variableKey;
        public bool expectedValue = true;

        protected override bool OnEvaluate(Blackboard blackboard)
        {
            if (blackboard.TryGetBool(variableKey, out bool value))
                return value == expectedValue;
            return false;
        }
    }

    [Serializable]
    public class FloatCondition : Condition
    {
        public string variableKey;
        public FloatComparison comparison = FloatComparison.GreaterThan;
        public float compareValue;

        protected override bool OnEvaluate(Blackboard blackboard)
        {
            if (blackboard.TryGetFloat(variableKey, out float value))
            {
                switch (comparison)
                {
                    case FloatComparison.GreaterThan: return value > compareValue;
                    case FloatComparison.GreaterOrEqual: return value >= compareValue;
                    case FloatComparison.LessThan: return value < compareValue;
                    case FloatComparison.LessOrEqual: return value <= compareValue;
                    case FloatComparison.Equal: return Mathf.Approximately(value, compareValue);
                    case FloatComparison.NotEqual: return !Mathf.Approximately(value, compareValue);
                }
            }
            return false;
        }
    }

    public enum FloatComparison
    {
        GreaterThan,
        GreaterOrEqual,
        LessThan,
        LessOrEqual,
        Equal,
        NotEqual
    }

    [Serializable]
    public class IntCondition : Condition
    {
        public string variableKey;
        public IntComparison comparison = IntComparison.GreaterThan;
        public int compareValue;

        protected override bool OnEvaluate(Blackboard blackboard)
        {
            if (blackboard.TryGetInt(variableKey, out int value))
            {
                switch (comparison)
                {
                    case IntComparison.GreaterThan: return value > compareValue;
                    case IntComparison.GreaterOrEqual: return value >= compareValue;
                    case IntComparison.LessThan: return value < compareValue;
                    case IntComparison.LessOrEqual: return value <= compareValue;
                    case IntComparison.Equal: return value == compareValue;
                    case IntComparison.NotEqual: return value != compareValue;
                }
            }
            return false;
        }
    }

    public enum IntComparison
    {
        GreaterThan,
        GreaterOrEqual,
        LessThan,
        LessOrEqual,
        Equal,
        NotEqual
    }

    [Serializable]
    public class StringCondition : Condition
    {
        public string variableKey;
        public StringComparison comparison = StringComparison.Equals;
        public string compareValue;

        protected override bool OnEvaluate(Blackboard blackboard)
        {
            if (blackboard.TryGetString(variableKey, out string value))
            {
                switch (comparison)
                {
                    case StringComparison.Equals: return value == compareValue;
                    case StringComparison.NotEquals: return value != compareValue;
                    case StringComparison.Contains: return value != null && value.Contains(compareValue);
                    case StringComparison.StartsWith: return value != null && value.StartsWith(compareValue);
                    case StringComparison.EndsWith: return value != null && value.EndsWith(compareValue);
                }
            }
            return false;
        }
    }

    public enum StringComparison
    {
        Equals,
        NotEquals,
        Contains,
        StartsWith,
        EndsWith
    }

    [Serializable]
    public class EventCondition : Condition
    {
        public string eventKey;
        [NonSerialized]
        private bool triggered;

        public void OnEventTriggered(string key)
        {
            if (key == eventKey)
                triggered = true;
        }

        public void ResetTrigger()
        {
            triggered = false;
        }

        protected override bool OnEvaluate(Blackboard blackboard)
        {
            return triggered;
        }
    }

    [Serializable]
    public class CooldownCondition : Condition
    {
        public float cooldownTime = 1f;
        [NonSerialized]
        private float elapsed;

        public void ResetCooldown()
        {
            elapsed = 0f;
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;
        }

        protected override bool OnEvaluate(Blackboard blackboard)
        {
            return elapsed >= cooldownTime;
        }
    }
}
