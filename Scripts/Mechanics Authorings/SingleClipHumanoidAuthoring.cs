using HSE.Mechanics.Components;
using Latios.Authoring;
using Latios.Kinemation;
using Latios.Kinemation.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class SingleClipHumanoidAuthoring : MonoBehaviour
{
    public AnimationClip clip;
}
[TemporaryBakingType]
struct SingleClipHumanoidSmartBakeItem : ISmartBakeItem<SingleClipHumanoidAuthoring>
{
    SmartBlobberHandle<SkeletonClipSetBlob> blob;

    public bool Bake(SingleClipHumanoidAuthoring authoring, IBaker baker)
    {
        var ebt = baker.GetEntity(TransformUsageFlags.Dynamic);
        baker.AddComponent<ClipSetStorage>(ebt);
        var clips = new NativeArray<SkeletonClipConfig>(1, Allocator.Temp);
        clips[0] = new SkeletonClipConfig { clip = authoring.clip, settings = SkeletonClipCompressionSettings.kDefaultSettings };
        blob = baker.RequestCreateBlobAsset(baker.GetComponent<Animator>(), clips);
        clips.Dispose();
        return true;
    }

    public void PostProcessBlobRequests(EntityManager entityManager, Entity entity)
    {
        entityManager.SetComponentData(entity, new ClipSetStorage { blob = blob.Resolve(entityManager) });
    }
}

class SingleClipHumanoidBaker : SmartBaker<SingleClipHumanoidAuthoring, SingleClipHumanoidSmartBakeItem>
{
}