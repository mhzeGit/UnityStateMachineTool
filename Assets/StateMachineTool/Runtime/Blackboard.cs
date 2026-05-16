using System;
using System.Collections.Generic;
using UnityEngine;

namespace StateMachineTool.Runtime
{
    public class Blackboard
    {
        private Dictionary<string, int> intVariables = new Dictionary<string, int>();
        private Dictionary<string, float> floatVariables = new Dictionary<string, float>();
        private Dictionary<string, bool> boolVariables = new Dictionary<string, bool>();
        private Dictionary<string, string> stringVariables = new Dictionary<string, string>();
        private Dictionary<string, Vector2> vector2Variables = new Dictionary<string, Vector2>();
        private Dictionary<string, Vector3> vector3Variables = new Dictionary<string, Vector3>();
        private Dictionary<string, UnityEngine.Object> objectVariables = new Dictionary<string, UnityEngine.Object>();
        private Dictionary<string, BlackboardValueType> variableTypes = new Dictionary<string, BlackboardValueType>();

        private HashSet<string> triggeredEvents = new HashSet<string>();

        public void Initialize(BlackboardData data)
        {
            Clear();

            foreach (var variable in data.variables)
            {
                variableTypes[variable.key] = variable.type;
                switch (variable.type)
                {
                    case BlackboardValueType.Int: intVariables[variable.key] = variable.intValue; break;
                    case BlackboardValueType.Float: floatVariables[variable.key] = variable.floatValue; break;
                    case BlackboardValueType.Bool: boolVariables[variable.key] = variable.boolValue; break;
                    case BlackboardValueType.String: stringVariables[variable.key] = variable.stringValue ?? string.Empty; break;
                    case BlackboardValueType.Vector2: vector2Variables[variable.key] = variable.vector2Value; break;
                    case BlackboardValueType.Vector3: vector3Variables[variable.key] = variable.vector3Value; break;
                    case BlackboardValueType.Object: objectVariables[variable.key] = variable.objectValue; break;
                }
            }
        }

        public void Clear()
        {
            intVariables.Clear();
            floatVariables.Clear();
            boolVariables.Clear();
            stringVariables.Clear();
            vector2Variables.Clear();
            vector3Variables.Clear();
            objectVariables.Clear();
            variableTypes.Clear();
            triggeredEvents.Clear();
        }

        public bool TryGetVariableType(string key, out BlackboardValueType type)
        {
            return variableTypes.TryGetValue(key, out type);
        }

        public bool HasVariable(string key)
        {
            return variableTypes.ContainsKey(key);
        }

        public bool HasEvent(string key)
        {
            // Events are tracked through the triggered set
            return true; // Event existence is validated at data level
        }

        // --- Int ---
        public int GetInt(string key, int defaultValue = 0)
        {
            if (intVariables.TryGetValue(key, out int value)) return value;
            return defaultValue;
        }
        public void SetInt(string key, int value) { intVariables[key] = value; }
        public bool TryGetInt(string key, out int value) { return intVariables.TryGetValue(key, out value); }

        // --- Float ---
        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (floatVariables.TryGetValue(key, out float value)) return value;
            return defaultValue;
        }
        public void SetFloat(string key, float value) { floatVariables[key] = value; }
        public bool TryGetFloat(string key, out float value) { return floatVariables.TryGetValue(key, out value); }

        // --- Bool ---
        public bool GetBool(string key, bool defaultValue = false)
        {
            if (boolVariables.TryGetValue(key, out bool value)) return value;
            return defaultValue;
        }
        public void SetBool(string key, bool value) { boolVariables[key] = value; }
        public bool TryGetBool(string key, out bool value) { return boolVariables.TryGetValue(key, out value); }

        // --- String ---
        public string GetString(string key, string defaultValue = "")
        {
            if (stringVariables.TryGetValue(key, out string value)) return value;
            return defaultValue;
        }
        public void SetString(string key, string value) { stringVariables[key] = value; }
        public bool TryGetString(string key, out string value) { return stringVariables.TryGetValue(key, out value); }

        // --- Vector2 ---
        public Vector2 GetVector2(string key)
        {
            if (vector2Variables.TryGetValue(key, out Vector2 value)) return value;
            return Vector2.zero;
        }
        public void SetVector2(string key, Vector2 value) { vector2Variables[key] = value; }
        public bool TryGetVector2(string key, out Vector2 value) { return vector2Variables.TryGetValue(key, out value); }

        // --- Vector3 ---
        public Vector3 GetVector3(string key)
        {
            if (vector3Variables.TryGetValue(key, out Vector3 value)) return value;
            return Vector3.zero;
        }
        public void SetVector3(string key, Vector3 value) { vector3Variables[key] = value; }
        public bool TryGetVector3(string key, out Vector3 value) { return vector3Variables.TryGetValue(key, out value); }

        // --- Object ---
        public UnityEngine.Object GetObject(string key)
        {
            if (objectVariables.TryGetValue(key, out UnityEngine.Object value)) return value;
            return null;
        }
        public void SetObject(string key, UnityEngine.Object value) { objectVariables[key] = value; }
        public bool TryGetObject(string key, out UnityEngine.Object value) { return objectVariables.TryGetValue(key, out value); }

        // --- Generic ---
        public object GetValue(string key)
        {
            if (!variableTypes.TryGetValue(key, out var type)) return null;
            switch (type)
            {
                case BlackboardValueType.Int: return GetInt(key);
                case BlackboardValueType.Float: return GetFloat(key);
                case BlackboardValueType.Bool: return GetBool(key);
                case BlackboardValueType.String: return GetString(key);
                case BlackboardValueType.Vector2: return GetVector2(key);
                case BlackboardValueType.Vector3: return GetVector3(key);
                case BlackboardValueType.Object: return GetObject(key);
                default: return null;
            }
        }

        public void SetValue(string key, object value)
        {
            if (!variableTypes.TryGetValue(key, out var type)) return;
            switch (type)
            {
                case BlackboardValueType.Int: SetInt(key, Convert.ToInt32(value)); break;
                case BlackboardValueType.Float: SetFloat(key, Convert.ToSingle(value)); break;
                case BlackboardValueType.Bool: SetBool(key, Convert.ToBoolean(value)); break;
                case BlackboardValueType.String: SetString(key, Convert.ToString(value)); break;
                case BlackboardValueType.Vector2: if (value is Vector2 v2) SetVector2(key, v2); break;
                case BlackboardValueType.Vector3: if (value is Vector3 v3) SetVector3(key, v3); break;
                case BlackboardValueType.Object: if (value is UnityEngine.Object obj) SetObject(key, obj); break;
            }
        }

        // --- Events ---
        public void TriggerEvent(string eventKey)
        {
            triggeredEvents.Add(eventKey);
        }

        public bool IsEventTriggered(string eventKey)
        {
            return triggeredEvents.Contains(eventKey);
        }

        public void ResetEvent(string eventKey)
        {
            triggeredEvents.Remove(eventKey);
        }

        public void ClearAllEvents()
        {
            triggeredEvents.Clear();
        }
    }
}
