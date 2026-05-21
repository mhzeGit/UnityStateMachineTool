using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CleanStateMachine
{
    [CreateAssetMenu(menuName = "Clean State Machine/Controller", fileName = "NewStateMachineController")]
    public class StateMachineController : ScriptableObject
    {
        [SerializeField] private SerializableData _data = new SerializableData();

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (_data != null)
            {
                bool needsRebuild = false;
                for (int i = 0; i < _data.States.Count; i++)
                {
                    var sd = _data.States[i];
                    if (sd.Behaviour != null && sd.Behaviour is StateBehaviour)
                        continue;
                    if (!string.IsNullOrEmpty(sd.BehaviourType))
                    {
                        needsRebuild = true;
                        break;
                    }
                }
                if (!needsRebuild)
                {
                    for (int i = 0; i < _data.Connections.Count; i++)
                    {
                        var cd = _data.Connections[i];
                        if (cd.Condition != null && cd.Condition is ConditionScript)
                            continue;
                        if (!string.IsNullOrEmpty(cd.ConditionType))
                        {
                            needsRebuild = true;
                            break;
                        }
                    }
                }
                if (needsRebuild)
                    RebuildBehaviourInstances(addSubAssets: false);
            }
#endif
        }

        public SerializableData Data
        {
            get => _data;
            set
            {
                _data = value;
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(this)))
                    EnsureSubAssets();
                EditorUtility.SetDirty(this);
#endif
            }
        }

        public void Save()
        {
#if UNITY_EDITOR
            EnsureSubAssets();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
#endif
        }

        public void RebuildBehaviourInstances(bool addSubAssets = true)
        {
#if UNITY_EDITOR
            if (_data == null) return;

            for (int i = 0; i < _data.States.Count; i++)
            {
                var sd = _data.States[i];
                if (!string.IsNullOrEmpty(sd.BehaviourType) && sd.Behaviour == null)
                {
                    var type = System.Type.GetType(sd.BehaviourType);
                    if (type == null)
                    {
                        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                        {
                            type = asm.GetType(sd.BehaviourType);
                            if (type != null) break;
                        }
                    }
                    if (type != null && type.IsSubclassOf(typeof(StateBehaviour)))
                    {
                        sd.Behaviour = (StateBehaviour)ScriptableObject.CreateInstance(type);
                        sd.Behaviour.name = $"{sd.Name}_Behaviour";
                        sd.Behaviour.hideFlags = HideFlags.HideInHierarchy;
                    }
                }
            }

            for (int i = 0; i < _data.Connections.Count; i++)
            {
                var cd = _data.Connections[i];
                if (!string.IsNullOrEmpty(cd.ConditionType) && cd.Condition == null)
                {
                    var type = System.Type.GetType(cd.ConditionType);
                    if (type == null)
                    {
                        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                        {
                            type = asm.GetType(cd.ConditionType);
                            if (type != null) break;
                        }
                    }
                    if (type != null && type.IsSubclassOf(typeof(ConditionScript)))
                    {
                        cd.Condition = (ConditionScript)ScriptableObject.CreateInstance(type);
                        cd.Condition.name = $"Condition_{cd.FromIndex}_{cd.ToIndex}";
                        cd.Condition.hideFlags = HideFlags.HideInHierarchy;
                    }
                }
            }

            if (addSubAssets)
            {
                EnsureSubAssets();
                EditorUtility.SetDirty(this);
            }
#endif
        }

#if UNITY_EDITOR
        public void EnsureSubAssets()
        {
            var path = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(path)) return;

            var referenced = new HashSet<Object>();
            if (_data != null)
            {
                for (int i = 0; i < _data.States.Count; i++)
                {
                    var inst = _data.States[i].Behaviour;
                    if (inst != null)
                    {
                        referenced.Add(inst);
                        if (!AssetDatabase.Contains(inst))
                        {
                            inst.hideFlags = HideFlags.HideInHierarchy;
                            AssetDatabase.AddObjectToAsset(inst, this);
                        }
                    }
                }

                for (int i = 0; i < _data.Connections.Count; i++)
                {
                    var inst = _data.Connections[i].Condition;
                    if (inst != null)
                    {
                        referenced.Add(inst);
                        if (!AssetDatabase.Contains(inst))
                        {
                            inst.hideFlags = HideFlags.HideInHierarchy;
                            AssetDatabase.AddObjectToAsset(inst, this);
                        }
                    }
                }
            }

            var subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            for (int i = subAssets.Length - 1; i >= 0; i--)
            {
                var sub = subAssets[i];
                if (sub != null && !referenced.Contains(sub) &&
                    (sub is StateBehaviour || sub is ConditionScript))
                {
                    Object.DestroyImmediate(sub, true);
                }
            }
        }
#endif
    }
}
