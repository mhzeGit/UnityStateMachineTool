using UnityEngine;

namespace CleanStateMachine
{
    public abstract class StateMachineAction : MonoBehaviour
    {
        [SerializeField] private StateMachineComponent _stateMachine;
        [SerializeField] private string _blackboardVariableName;
        [SerializeField] private BlackboardVariableType _blackboardVariableType;

        public StateMachineComponent StateMachine => _stateMachine;
        public string BlackboardVariableName => _blackboardVariableName;
        public virtual BlackboardVariableType RequiredVariableType => BlackboardVariableType.Bool;

        protected void SetBlackboardValue(bool value)
        {
            if (_stateMachine != null && !string.IsNullOrEmpty(_blackboardVariableName))
                _stateMachine.SetBoolParameter(_blackboardVariableName, value);
        }

        protected void SetBlackboardValue(int value)
        {
            if (_stateMachine != null && !string.IsNullOrEmpty(_blackboardVariableName))
                _stateMachine.SetIntParameter(_blackboardVariableName, value);
        }

        protected void SetBlackboardValue(float value)
        {
            if (_stateMachine != null && !string.IsNullOrEmpty(_blackboardVariableName))
                _stateMachine.SetFloatParameter(_blackboardVariableName, value);
        }

        protected void SetBlackboardValue(string value)
        {
            if (_stateMachine != null && !string.IsNullOrEmpty(_blackboardVariableName))
                _stateMachine.SetStringParameter(_blackboardVariableName, value);
        }

        protected void SetBlackboardTrigger()
        {
            if (_stateMachine != null && !string.IsNullOrEmpty(_blackboardVariableName))
                _stateMachine.SetTriggerParameter(_blackboardVariableName);
        }

        protected bool GetBlackboardTrigger()
        {
            if (_stateMachine != null && !string.IsNullOrEmpty(_blackboardVariableName))
                return _stateMachine.GetTriggerParameter(_blackboardVariableName);
            return false;
        }

        protected bool GetBlackboardBool()
        {
            if (_stateMachine != null && !string.IsNullOrEmpty(_blackboardVariableName))
                return _stateMachine.GetBoolParameter(_blackboardVariableName);
            return false;
        }

        protected int GetBlackboardInt()
        {
            if (_stateMachine != null && !string.IsNullOrEmpty(_blackboardVariableName))
                return _stateMachine.GetIntParameter(_blackboardVariableName);
            return 0;
        }

        protected float GetBlackboardFloat()
        {
            if (_stateMachine != null && !string.IsNullOrEmpty(_blackboardVariableName))
                return _stateMachine.GetFloatParameter(_blackboardVariableName);
            return 0f;
        }

        protected string GetBlackboardString()
        {
            if (_stateMachine != null && !string.IsNullOrEmpty(_blackboardVariableName))
                return _stateMachine.GetStringParameter(_blackboardVariableName);
            return "";
        }


    }
}
