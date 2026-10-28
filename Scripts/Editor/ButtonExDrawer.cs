using System;

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using Unity.EditorCoroutines.Editor;
using System.Reflection;

namespace HSE.Editor.Attributes
{
    [CustomPropertyDrawer(typeof(NamedButtonAttribute))]
    public sealed class ButtonExDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (NamedButtonAttribute)attribute;

            string buttonText = ResolveLabel(property, label, attr);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(attr.MethodName)))
            {
                if (GUI.Button(position, buttonText))
                {
                    InvokeOnTargets(property, attr.MethodName);
                }
            }
        }

        private static string ResolveLabel(SerializedProperty property, GUIContent fallback, NamedButtonAttribute attr)
        {
            if (!string.IsNullOrWhiteSpace(attr.LabelMemberName))
            {
                var p = property.serializedObject.FindProperty(attr.LabelMemberName);
                if (p != null && p.propertyType == SerializedPropertyType.String)
                {
                    var s = p.stringValue ?? "";
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }

            if (!string.IsNullOrWhiteSpace(attr.Label))
                return attr.Label;

            if (!string.IsNullOrWhiteSpace(attr.MethodName))
                return ObjectNames.NicifyVariableName(attr.MethodName);

            return fallback != null ? fallback.text : "Button";
        }

        private static void InvokeOnTargets(SerializedProperty property, string methodName)
        {
            var targets = property.serializedObject.targetObjects;
            if (targets == null || targets.Length == 0) return;

            for (int i = 0; i < targets.Length; i++)
            {
                var t = targets[i] as UnityEngine.Object;
                if (t == null) continue;

                var type = t.GetType();
                var mi = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi == null)
                {
                    Debug.LogError($"[ButtonEx] Method '{methodName}' not found on {type.Name}", t);
                    continue;
                }

                if (mi.GetParameters().Length != 0)
                {
                    Debug.LogError($"[ButtonEx] Method '{methodName}' on {type.Name} must have 0 parameters.", t);
                    continue;
                }

                try
                {
                    Undo.RecordObject(t, $"Invoke {methodName}");

                    var ret = mi.Invoke(t, null);

                    if (ret is System.Collections.IEnumerator e)
                        EditorCoroutineUtility.StartCoroutineOwnerless(e);

                    EditorUtility.SetDirty(t);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ButtonEx] Invoke '{methodName}' failed: {ex}", t);
                }
            }

            AssetDatabase.SaveAssets();
        }
    }
}
#endif