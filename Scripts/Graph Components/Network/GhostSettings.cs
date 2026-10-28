using Unity.Entities;

namespace HSE.Core.Components.Network
{
    public enum GhostType: byte
    {
        None = 0,
        Local = 1,
        Interpolated = 2,
        Predicted = 3
    }
    public struct GhostSettings: IComponentData
    {
        public GhostType Type;
    }
}
