using System.Collections.Generic;
using UnityEngine;

namespace CleanStateMachine
{
    public enum StateValidationStatus
    {
        None,
        Ignored,
        Unreachable,
        DeadEnd,
    }

    public class GraphValidation
    {
        private readonly CleanStateMachineWindow _window;
        private bool _dirty = true;

        public GraphValidation(CleanStateMachineWindow window)
        {
            _window = window;
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void RunAndUpdate()
        {
            if (!_dirty) return;
            _dirty = false;

            var states = _window.States;
            var connections = _window.Connections;

            var incoming = new HashSet<StateView>();
            var outgoing = new HashSet<StateView>();

            for (int i = 0; i < connections.Count; i++)
            {
                var conn = connections[i];
                if (conn.To != null)
                    incoming.Add(conn.To);
                if (conn.From != null)
                    outgoing.Add(conn.From);
            }

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];

                if (state.IsEntry || state.IsAnyState || state.IsSubEntry)
                {
                    state.SetValidationStatus(StateValidationStatus.None);
                    continue;
                }

                bool hasIncoming = incoming.Contains(state);
                bool hasOutgoing = outgoing.Contains(state);

                if (!hasIncoming && !hasOutgoing)
                {
                    state.SetValidationStatus(StateValidationStatus.Ignored);
                }
                else if (hasIncoming && !hasOutgoing && !state.IsSubStateMachine)
                {
                    state.SetValidationStatus(StateValidationStatus.DeadEnd);
                }
                else if (!hasIncoming && hasOutgoing)
                {
                    state.SetValidationStatus(StateValidationStatus.Unreachable);
                }
                else
                {
                    state.SetValidationStatus(StateValidationStatus.None);
                }
            }
        }

        public static List<ValidationMessage> GetStateMessages(StateView state, List<ConnectionView> connections)
        {
            var messages = new List<ValidationMessage>();

            for (int i = 0; i < state.BehaviourEntries.Count; i++)
            {
                var entry = state.BehaviourEntries[i];
                if (entry.GetScript() == null)
                    messages.Add(new ValidationMessage(ValidationMessageType.Error, $"Behaviour {i + 1}: No script assigned"));
            }

            if (state.IsExternalReference)
            {
                if (state.ExternalStateMachine == null)
                {
                    messages.Add(new ValidationMessage(ValidationMessageType.Error, "External Reference: Target GameObject not assigned"));
                }
                else
                {
                    var sm = state.ExternalStateMachine.GetComponent<StateMachineComponent>();
                    if (sm == null)
                        messages.Add(new ValidationMessage(ValidationMessageType.Error, "External Reference: Target has no StateMachineComponent"));
                    else if (sm.Controller == null)
                        messages.Add(new ValidationMessage(ValidationMessageType.Error, "External Reference: Target has no StateMachineController assigned"));
                }
            }

            if (state.ValidationStatus == StateValidationStatus.Ignored)
                messages.Add(new ValidationMessage(ValidationMessageType.Error, "State has no connections at all — completely ignored"));
            else if (state.ValidationStatus == StateValidationStatus.Unreachable)
                messages.Add(new ValidationMessage(ValidationMessageType.Warning, "State has no incoming connections — unreachable from entry"));
            else if (state.ValidationStatus == StateValidationStatus.DeadEnd)
                messages.Add(new ValidationMessage(ValidationMessageType.Warning, "State has no outgoing transitions — can get stuck after entering"));

            return messages;
        }

        public static List<ValidationMessage> GetConnectionMessages(ConnectionView conn)
        {
            var messages = new List<ValidationMessage>();

            for (int i = 0; i < conn.ConditionEntries.Count; i++)
            {
                var entry = conn.ConditionEntries[i];
                if (entry.GetScript() == null)
                    messages.Add(new ValidationMessage(ValidationMessageType.Error, $"Condition {i + 1}: No script assigned"));
            }

            return messages;
        }

        public static List<ValidationMessage> GetGraphMessages(List<StateView> states, List<ConnectionView> connections)
        {
            var messages = new List<ValidationMessage>();

            bool hasEntry = false;
            var nameCount = new Dictionary<string, int>();

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state.IsEntry)
                    hasEntry = true;

                if (!string.IsNullOrEmpty(state.Name))
                {
                    if (!nameCount.TryGetValue(state.Name, out var count))
                        nameCount[state.Name] = 1;
                    else
                        nameCount[state.Name] = count + 1;
                }
            }

            if (!hasEntry)
                messages.Add(new ValidationMessage(ValidationMessageType.Error, "No Entry state in graph"));

            foreach (var kvp in nameCount)
            {
                if (kvp.Value > 1)
                    messages.Add(new ValidationMessage(ValidationMessageType.Error, $"Duplicate state name: \"{kvp.Key}\" ({kvp.Value} states)"));
            }

            return messages;
        }
    }

    public enum ValidationMessageType
    {
        Info,
        Warning,
        Error,
    }

    public struct ValidationMessage
    {
        public ValidationMessageType Type;
        public string Text;

        public ValidationMessage(ValidationMessageType type, string text)
        {
            Type = type;
            Text = text;
        }
    }
}
