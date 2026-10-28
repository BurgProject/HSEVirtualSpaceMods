using GraphProcessor;
using UnityEngine;

namespace HSE.CodeCraft
{
    [System.Serializable, NodeMenuItem("Containers/Static/Texture2DContainer")]
    public class ContainerTexture2DNode : ContainerNode<Texture2D>
    {
        public override string name => "ContainerTexture2D";
    }
    [System.Serializable, NodeMenuItem("Containers/Static/Vector3Container")]
    public class ContainerVector3Node : ContainerNode<Vector3>
    {
        public override string name => "ContainerVector3";
    }
}
