using HSE.Core.Components;
using HSE.UI;
using Unity.Entities;
using UnityEngine;

namespace HSE.Mechanics.Authorings
{
    [DisallowMultipleComponent]
    public sealed class PointerHintAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        private sealed class Baker : Baker<PointerHintAuthoring>
        {
            public override void Bake(PointerHintAuthoring authoring)
            {
                var prefab = authoring.Prefab;
                if (prefab == null || prefab.scene.IsValid() || !prefab.TryGetComponent<UiPanelView>(out var view))
                {
                    Debug.LogError("Assign a UI prefab asset with UiPanelView on its root.", authoring);
                    return;
                }
                if (!view.Validate(out var error))
                {
                    Debug.LogError(error, authoring);
                    return;
                }
                DependsOn(prefab);
                DependsOn(view);
                AddComponentObject(GetEntity(TransformUsageFlags.None), new PointerHintPrefab { Prefab = prefab });
            }
        }
    }
}
