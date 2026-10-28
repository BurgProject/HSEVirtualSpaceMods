using GraphProcessor;
using UnityEngine;

namespace HSE.CodeCraft
{
    [System.Serializable, NodeMenuItem("Utility/Extractors/Transform")]
    public class GameObjectTransformExtractorNode: BaseHSENode
    {
        [Input, ShowAsDrawer]
        public GameObject GameObject;

        [Output]
        public Vector3 Position;
        [Output]
        public Quaternion Rotation;
        [Output]
        public Vector3 Scale;

        public override string name => "GameObjectTransformExtractor";

        protected override bool Process()
        {
            bool goIsNull = GameObject == null;
            AddStateMessage("Назначьте GameObject!", goIsNull, NodeMessageType.Error);

            if(!goIsNull){
          
                Position = GameObject.transform.position;
                Rotation = GameObject.transform.rotation;
                Scale = GameObject.transform.localScale;
                return true;
            }
            return false;
        }
    }
}
