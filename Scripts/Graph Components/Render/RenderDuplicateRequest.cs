using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Components
{
    public struct RenderDuplicateRequest : IBufferElementData{
        public float3 InitPostion;
        public bool IsOverlay;
    }
    public struct RenderableDuplicateReference : IBufferElementData{
        public Entity Dublicate;
    }
}
