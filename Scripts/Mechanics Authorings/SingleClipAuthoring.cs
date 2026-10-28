using Latios.Authoring;
using Latios.Kinemation.Authoring;
using Latios.Kinemation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using HSE.Components;
using HSE.Mechanics.Components;

[DisallowMultipleComponent]
public class SingleClipAuthoring : MonoBehaviour
{
    public AnimationClip clip;
}

[TemporaryBakingType]
struct SingleClipSmartBakeItem : ISmartBakeItem<SingleClipAuthoring>
{
    SmartBlobberHandle<SkeletonClipSetBlob> blob;

    public bool Bake(SingleClipAuthoring authoring, IBaker baker)
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

class SingleClipBaker : SmartBaker<SingleClipAuthoring, SingleClipSmartBakeItem>
{
}
