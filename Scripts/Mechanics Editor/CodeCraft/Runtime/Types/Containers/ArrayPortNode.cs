using GraphProcessor;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows;

namespace HSE.CodeCraft
{
    public class ArrayPortNode<T> : BaseHSENode, IPlusMinusButtonNode
    {
        [Input(name = "In Values")]
        public List<T> inputs = null;

        [Output(name = "Out Values", allowMultiple = true)]
        [NonSerialized] public T[] outputs = Array.Empty<T>();

        protected virtual string displayPortName => "value ";
        public static Color ColorArrayPortNode = Color.aliceBlue;
        public override Color color => ColorArrayPortNode;

        // We keep the max port count so it doesn't cause binding issues
        [SerializeField, HideInInspector]
        int defaultPortCount = 1;

        [CustomPortBehavior(nameof(inputs))]
        public IEnumerable<PortData> GetPortsForInputs(List<SerializableEdge> edges)
        {
            for (int i = 0; i < defaultPortCount; i++)
            {
                var identifier = i.ToString();
                yield return new PortData { displayName = displayPortName+identifier, displayType = typeof(T), identifier = identifier, acceptMultipleEdges = false };
            }
        }

        public virtual void PullInputs(List<SerializableEdge> inputEdges)
        {
            var values = new T[defaultPortCount];
            foreach (var edge in inputEdges)
            {
                if (int.TryParse(edge.inputPortIdentifier, out int index) && index >= 0 && index < values.Length &&
                    edge.passThroughBuffer is T value)
                    values[index] = value;
            }
            outputs = values;
        }

        protected override bool Process()
        {
            ClearMessages();
            var edges = new List<SerializableEdge>(defaultPortCount);
            for (int i = 0; i < defaultPortCount; i++)
            {
                var portEdges = GetPort(nameof(inputs), i.ToString())?.GetEdges();
                if (portEdges == null || portEdges.Count != 1 || !(portEdges[0].passThroughBuffer is T))
                {
                    outputs = Array.Empty<T>();
                    AddMessage("Connect a value to each array input.", NodeMessageType.Error);
                    return false;
                }
                edges.Add(portEdges[0]);
            }
            PullInputs(edges);
            return true;
        }
        //public virtual void PushOutputs(List<SerializableEdge> connectedEdges)
        //{
        //    // Values length is supposed to match connected edges length
        //    for (int i = 0; i < connectedEdges.Count; i++)
        //        connectedEdges[i].passThroughBuffer = values[Mathf.Min(i, values.Count - 1)];

        //    // once the outputs are pushed, we don't need the inputs data anymore
        //    values.Clear();
        //}

        public void OnClickPlusButton(){
            if(defaultPortCount >= 0)
                defaultPortCount++;
        }
        public void OnClickMinusButton() {
            if (defaultPortCount > 1)
                defaultPortCount--;
        }
    }
}
