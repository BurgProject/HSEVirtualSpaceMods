using Unity.Entities;

namespace HSE.Mechanics.Components
{
    public struct CodeCraftDataStorage{
        public BlobArray<byte> AllDatas;
    }

    public struct CodeCraftDataAsset : IComponentData{
        public BlobAssetReference<CodeCraftDataStorage> Storage;
    }
}
