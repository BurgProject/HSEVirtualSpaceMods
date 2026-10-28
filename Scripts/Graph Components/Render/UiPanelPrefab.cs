using Unity.Entities;
using UnityEngine;

namespace HSE.Core.Components
{
    // Resource references use Entities serialization, not the graph wire codec.
    public sealed class UiPanelPrefab : IComponentData
    {
        public GameObject Prefab;
    }
}
