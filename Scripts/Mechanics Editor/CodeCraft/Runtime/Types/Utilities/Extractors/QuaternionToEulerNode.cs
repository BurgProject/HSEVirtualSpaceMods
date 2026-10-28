using GraphProcessor;
using UnityEngine;

namespace HSE.CodeCraft
{
    [System.Serializable, NodeMenuItem("Utility/Extractors/Math/QuaternionToEuler")]
    public class QuaternionToEulerNode : BaseHSENode
    {
        [Input]
        public Quaternion Rotation;
        [Output]
        public Vector3 EulerAngles;

        public override string name => "QuaternionToEuler";

        protected override bool Process()
        {
            EulerAngles = Rotation.eulerAngles;
            return true;
        }
    }
}
