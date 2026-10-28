
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShowIfAttribute : PropertyAttribute
{
    public string ConditionalFieldName { get; private set; }
    public bool ExpectedValue { get; private set; }

    public ShowIfAttribute(string conditionalFieldName, bool expectedValue = true)
    {
        ConditionalFieldName = conditionalFieldName;
        ExpectedValue = expectedValue;
    }
}
