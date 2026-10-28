using GraphProcessor;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace HSE.CodeCraft
{
    public class ContainerNode<T> : BaseHSENode
    {
        [Output(name = "Out Values", allowMultiple = true)]
        [NonSerialized] public T[] outputs = Array.Empty<T>();

        public List<T> values = new List<T>();

        public override Color color => Color.antiqueWhite;

        protected override bool Process()
        {
            outputs = values?.ToArray() ?? Array.Empty<T>();
            return true;
        }
    }
}
