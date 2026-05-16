using System;
using UnityEngine;
using UnityEngine.Events;

namespace StateMachineTool.Runtime
{
    [Serializable]
    public abstract class Action
    {
        public bool enabled = true;

        public void Execute(Blackboard blackboard, GameObject owner)
        {
            if (enabled)
                OnExecute(blackboard, owner);
        }

        protected abstract void OnExecute(Blackboard blackboard, GameObject owner);
    }

    [Serializable]
    public class DebugLogAction : Action
    {
        public string message = "State Action";

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
            Debug.Log($"[StateMachine] {message}", owner);
        }
    }

    [Serializable]
    public class DebugLogWarningAction : Action
    {
        public string message = "Warning";

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
            Debug.LogWarning($"[StateMachine] {message}", owner);
        }
    }

    [Serializable]
    public class DebugLogErrorAction : Action
    {
        public string message = "Error";

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
            Debug.LogError($"[StateMachine] {message}", owner);
        }
    }

    [Serializable]
    public class SetVariableAction : Action
    {
        public string variableKey;

        public int intValue;
        public float floatValue;
        public bool boolValue;
        public string stringValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public UnityEngine.Object objectValue;

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
            if (!blackboard.TryGetVariableType(variableKey, out BlackboardValueType type))
                return;

            switch (type)
            {
                case BlackboardValueType.Int:
                    blackboard.SetInt(variableKey, intValue);
                    break;
                case BlackboardValueType.Float:
                    blackboard.SetFloat(variableKey, floatValue);
                    break;
                case BlackboardValueType.Bool:
                    blackboard.SetBool(variableKey, boolValue);
                    break;
                case BlackboardValueType.String:
                    blackboard.SetString(variableKey, stringValue);
                    break;
                case BlackboardValueType.Vector2:
                    blackboard.SetVector2(variableKey, vector2Value);
                    break;
                case BlackboardValueType.Vector3:
                    blackboard.SetVector3(variableKey, vector3Value);
                    break;
                case BlackboardValueType.Object:
                    blackboard.SetObject(variableKey, objectValue);
                    break;
            }
        }
    }

    [Serializable]
    public class TriggerEventAction : Action
    {
        public string eventKey;

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
            blackboard.TriggerEvent(eventKey);
        }
    }

    [Serializable]
    public class SetActiveAction : Action
    {
        public string gameObjectName;
        public bool setActive = true;

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
            var go = GameObject.Find(gameObjectName);
            if (go != null)
                go.SetActive(setActive);
        }
    }

    [Serializable]
    public class UnityEventAction : Action
    {
        public UnityEvent unityEvent = new UnityEvent();

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
            unityEvent.Invoke();
        }
    }

    [Serializable]
    public class WaitAction : Action
    {
        public float duration = 1f;

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
        }
    }

    [Serializable]
    public class SetAnimatorBoolAction : Action
    {
        public string parameterName;
        public bool value;

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
            var animator = owner.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.SetBool(parameterName, value);
        }
    }

    [Serializable]
    public class SetAnimatorFloatAction : Action
    {
        public string parameterName;
        public float value;

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
            var animator = owner.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.SetFloat(parameterName, value);
        }
    }

    [Serializable]
    public class SetAnimatorTriggerAction : Action
    {
        public string parameterName;

        protected override void OnExecute(Blackboard blackboard, GameObject owner)
        {
            var animator = owner.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.SetTrigger(parameterName);
        }
    }
}
