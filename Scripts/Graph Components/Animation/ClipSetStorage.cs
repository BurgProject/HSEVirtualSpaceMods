using Latios.Kinemation;
using Latios.Transforms;
using Unity.Entities;
using Unity.Transforms;

namespace HSE.Mechanics.Components
{
    public struct ClipSetStorage : IComponentData
    {
        public BlobAssetReference<SkeletonClipSetBlob> blob;
    }

    public struct RelativeAnimationBaseTransform : IComponentData
    {
        public LocalTransform Value;
    }

    public struct RelativeAnimationClipStartPose : IComponentData
    {
        public TransformQvvs Value;
    }
}