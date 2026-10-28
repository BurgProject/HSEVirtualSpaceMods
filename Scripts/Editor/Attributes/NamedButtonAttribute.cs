using System;
using UnityEngine;

namespace HSE.Editor.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NamedButtonAttribute : PropertyAttribute
    {
        public string MethodName { get; }
        public string Label { get; set; }
        public string LabelMemberName { get; set; } // name of serialized string field on same object

        public NamedButtonAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}