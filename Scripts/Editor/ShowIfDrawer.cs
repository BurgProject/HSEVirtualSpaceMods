#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfDrawer: PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute att = (ShowIfAttribute)attribute;

        SerializedProperty conditionField = property.serializedObject.FindProperty(att.ConditionalFieldName);

        if (conditionField == null)
        {
            EditorGUI.LabelField(position, label.text, "Condition field not found!");
            return;
        }

        bool conditionMet = conditionField.boolValue == att.ExpectedValue;
        if (conditionMet)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute att = (ShowIfAttribute)attribute;
        SerializedProperty conditionField = property.serializedObject.FindProperty(att.ConditionalFieldName);

        if (conditionField == null)
            return EditorGUIUtility.singleLineHeight;

        bool conditionMet = conditionField.boolValue == att.ExpectedValue;

        if (!conditionMet)
            return 0f;

        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif