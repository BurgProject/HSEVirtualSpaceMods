using System;
using JetBrains.Annotations;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public sealed class ButtonAttribute : PropertyAttribute
{
    public readonly string MethodName;
    public ButtonAttribute(string name) {
        MethodName = name;
    }
}
