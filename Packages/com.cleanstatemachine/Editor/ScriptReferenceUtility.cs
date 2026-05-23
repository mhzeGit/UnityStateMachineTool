using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CleanStateMachine
{
    public static class ScriptReferenceUtility
    {
        public static string GetTypeName(MonoScript script)
        {
            if (script == null) return null;
            var type = script.GetClass();
            return type != null ? type.FullName : null;
        }

        public static MonoScript FindScriptByTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            var scripts = Resources.FindObjectsOfTypeAll<MonoScript>();
            for (int i = 0; i < scripts.Length; i++)
            {
                var type = scripts[i].GetClass();
                if (type != null && type.FullName == typeName)
                    return scripts[i];
            }
            return null;
        }

        public static StyleSheet LoadStyleSheet(string name)
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:StyleSheet");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }
            return null;
        }

        public static string FindAssetPath(string fileName)
        {
            string[] guids = AssetDatabase.FindAssets(fileName);
            if (guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }
            return null;
        }
    }
}
