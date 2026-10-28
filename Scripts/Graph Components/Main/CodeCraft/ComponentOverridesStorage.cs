using HSE.Collections;
using Unity.Entities;
using HSE.Core.Components;

namespace HSE.Mechanics.Components
{
    public struct Overrides : IComponentData
    {
        public BlobAssetReference<ComponentOverridesStorage> ComponentsStorage;
    }
    public struct ComponentOverridesStorage
    {
        public BlobMultiHashMap<ActionOverrideEvent, OverrideKeyState> OverrideStates;
        public BlobMultiHashMap<ActionOverrideEvent, OverrideKey> Overrides;
    }
}
