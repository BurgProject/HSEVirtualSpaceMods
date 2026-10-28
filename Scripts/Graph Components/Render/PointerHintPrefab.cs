using Unity.Entities;
using UnityEngine;

namespace HSE.Core.Components
{
    public sealed class PointerHintPrefab : IComponentData
    {
        public GameObject Prefab;
    }
}
