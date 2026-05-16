using System.Collections.Generic;
using UnityEngine;

namespace StateMachineTool.Runtime
{
    public class StateMachineRunner : MonoBehaviour
    {
        [SerializeField]
        private StateMachineAsset stateMachineAsset;

        [SerializeField]
        private bool startOnAwake = true;

        [SerializeField]
        private bool debugLogTransitions;

        private Blackboard blackboard;
        private StateData currentState;
        private Dictionary<string, EventCondition> eventConditions = new Dictionary<string, EventCondition>();
        private Dictionary<string, CooldownCondition> cooldownConditions = new Dictionary<string, CooldownCondition>();
        private bool initialized;

        public StateMachineAsset Asset => stateMachineAsset;
        public StateData CurrentState => currentState;
        public Blackboard Blackboard => blackboard;
        public bool IsRunning => initialized && currentState != null;

        private void Awake()
        {
            if (startOnAwake)
                Initialize();
        }

        public void Initialize()
        {
            if (stateMachineAsset == null)
            {
                Debug.LogError("[StateMachine] No StateMachineAsset assigned.", this);
                return;
            }

            blackboard = new Blackboard();
            blackboard.Initialize(stateMachineAsset.graphData.blackboard);

            eventConditions.Clear();
            cooldownConditions.Clear();

            CacheSpecialConditions();

            var entryState = stateMachineAsset.GetEntryState();
            if (entryState != null)
            {
                TransitionTo(entryState);
            }
            else
            {
                Debug.LogWarning("[StateMachine] No entry state found.", this);
            }

            initialized = true;
        }

        public void Restart()
        {
            initialized = false;

            if (blackboard != null)
                blackboard.Initialize(stateMachineAsset.graphData.blackboard);

            CacheSpecialConditions();

            var entryState = stateMachineAsset.GetEntryState();
            if (entryState != null)
                TransitionTo(entryState);

            initialized = true;
        }

        public void Stop()
        {
            if (currentState != null)
            {
                ExecuteActions(currentState.onExitActions, blackboard, gameObject);
            }
            currentState = null;
            initialized = false;
        }

        private void CacheSpecialConditions()
        {
            eventConditions.Clear();
            cooldownConditions.Clear();

            foreach (var transition in stateMachineAsset.graphData.transitions)
            {
                foreach (var condition in transition.conditions)
                {
                    if (condition is EventCondition ec)
                        eventConditions[transition.id] = ec;
                    if (condition is CooldownCondition cc)
                        cooldownConditions[transition.id] = cc;
                }
            }
        }

        private void Update()
        {
            if (!initialized || currentState == null || stateMachineAsset == null)
                return;

            float deltaTime = Time.deltaTime;

            TickCooldownConditions(deltaTime);

            ExecuteActions(currentState.onUpdateActions, blackboard, gameObject);

            EvaluateTransitions();
        }

        private void TickCooldownConditions(float deltaTime)
        {
            foreach (var kvp in cooldownConditions)
            {
                kvp.Value.Tick(deltaTime);
            }
        }

        private void EvaluateTransitions()
        {
            var transitions = stateMachineAsset.GetTransitionsFrom(currentState.id);

            foreach (var transition in transitions)
            {
                if (EvaluateTransition(transition))
                {
                    var targetState = stateMachineAsset.GetState(transition.toStateId);
                    if (targetState != null)
                    {
                        TransitionTo(targetState);

                        if (cooldownConditions.TryGetValue(transition.id, out var cc))
                            cc.ResetCooldown();

                        break;
                    }
                }
            }
        }

        public void TriggerEvent(string eventKey)
        {
            if (!initialized)
                return;

            blackboard.TriggerEvent(eventKey);

            foreach (var kvp in eventConditions)
            {
                kvp.Value.OnEventTriggered(eventKey);
            }

            EvaluateTransitions();
        }

        private bool EvaluateTransition(TransitionData transition)
        {
            if (transition.conditions.Count == 0)
                return false;

            foreach (var condition in transition.conditions)
            {
                if (!condition.Evaluate(blackboard))
                    return false;
            }

            return true;
        }

        private void TransitionTo(StateData newState)
        {
            if (currentState != null)
            {
                ExecuteActions(currentState.onExitActions, blackboard, gameObject);
                if (debugLogTransitions)
                    Debug.Log($"[StateMachine] Exit: {currentState.displayName}", this);
            }

            foreach (var kvp in eventConditions)
            {
                kvp.Value.ResetTrigger();
            }

            currentState = newState;

            ExecuteActions(newState.onEnterActions, blackboard, gameObject);

            if (debugLogTransitions)
                Debug.Log($"[StateMachine] Enter: {newState.displayName}", this);
        }

        private void ExecuteActions(List<Action> actions, Blackboard bb, GameObject owner)
        {
            if (actions == null) return;
            foreach (var action in actions)
            {
                action.Execute(bb, owner);
            }
        }
    }
}
