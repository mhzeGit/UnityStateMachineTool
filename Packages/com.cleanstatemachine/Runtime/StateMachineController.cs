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
                        if (cd.Conditions == null) continue;
                        for (int j = 0; j < cd.Conditions.Count; j++)
                        {
                            var ce = cd.Conditions[j];
                            if (ce.Instance != null && ce.Instance is ConditionScript)
                                continue;
                            if (!string.IsNullOrEmpty(ce.TypeName))
                            {
                                needsRebuild = true;
                                break;
                            }
                        }
                        if (needsRebuild) break;
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
                if (cd.Conditions == null) continue;
                for (int j = 0; j < cd.Conditions.Count; j++)
                {
                    var ce = cd.Conditions[j];
                    if (!string.IsNullOrEmpty(ce.TypeName) && ce.Instance == null)
                    {
                        var type = System.Type.GetType(ce.TypeName);
                        if (type == null)
                        {
                            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                            {
                                type = asm.GetType(ce.TypeName);
                                if (type != null) break;
                            }
                        }
                        if (type != null && type.IsSubclassOf(typeof(ConditionScript)))
                        {
                            ce.Instance = (ConditionScript)ScriptableObject.CreateInstance(type);
                            ce.Instance.name = $"Condition_{cd.FromIndex}_{cd.ToIndex}_{j}";
                            ce.Instance.hideFlags = HideFlags.HideInHierarchy;
                        }
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
                    var cd = _data.Connections[i];
                    if (cd.Conditions == null) continue;
                    for (int j = 0; j < cd.Conditions.Count; j++)
                    {
                        var inst = cd.Conditions[j].Instance;
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
