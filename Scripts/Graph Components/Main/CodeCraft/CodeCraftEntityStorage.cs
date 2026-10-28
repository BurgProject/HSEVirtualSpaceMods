using HSE.Collections;
using Unity.Entities;

namespace HSE.Mechanics.Components
{
    [BakingType]
    public struct EntityOverride : IBufferElementData
    {
        public Entity Value;
    }
    public struct CodeCraftEntityStorage
    {
        public BlobMultiHashMap<Entity, OverrideStateElement> OverrideStatesComponents;
        public BlobMultiHashMap<Entity, OverrideKeyElement> OverridesComponents;
    }

    [BakingType]
    public struct CodeCraftAsset : IComponentData
    {
        public BlobAssetReference<CodeCraftEntityStorage> Storage;
    }
}
