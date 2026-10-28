using GraphProcessor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows;

namespace HSE.CodeCraft
{
    [Serializable, NodeMenuItem("Containers/ArrayPort/Vector3")]
    public sealed class ArrayVector3PortNode : ArrayPortNode<Vector3>
    {
        [CustomPortInput(nameof(inputs), typeof(Vector3))]
        public override void PullInputs(List<SerializableEdge> edges) => base.PullInputs(edges);
    }

    [Serializable, NodeMenuItem("Containers/ArrayPort/GameObject")]
    public sealed class ArrayGameObjectPortNode : ArrayPortNode<GameObject>
    {
        [CustomPortInput(nameof(inputs), typeof(GameObject))]
        public override void PullInputs(List<SerializableEdge> edges) => base.PullInputs(edges);
    }


    [System.Serializable, NodeMenuItem("Containers/ArrayPort/Texture2DPortArray")]
    public class ArrayTexture2DNode : ArrayPortNode<Texture2D>
    {
        public override string name => "ArrayPortTexture2D";
        protected override string displayPortName => "Texture ";


        [CustomPortInput(nameof(inputs), typeof(Texture2D), allowCast = true)]
        public override void PullInputs(List<SerializableEdge> inputEdges)
        {
            base.PullInputs(inputEdges);
        }

        //[CustomPortOutput(nameof(outputs), typeof(Texture2D))]
        //public override void PushOutputs(List<SerializableEdge> connectedEdges)
        //{
        //    base.PushOutputs(connectedEdges);
        //}
    }
}
