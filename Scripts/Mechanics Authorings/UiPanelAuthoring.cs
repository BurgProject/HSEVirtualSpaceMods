using HSE.Core.Components;
using HSE.UI;
using Unity.Entities;
using UnityEngine;

namespace HSE.Mechanics.Authorings
{
    [DisallowMultipleComponent]
    public sealed class UiPanelAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

        private sealed class Baker : Baker<UiPanelAuthoring>
        {
            public override void Bake(UiPanelAuthoring authoring)
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
                // Keep the GameObject resource; do not convert the Canvas prefab into an ECS prefab.
                AddComponentObject(GetEntity(TransformUsageFlags.None), new UiPanelPrefab { Prefab = prefab });
            }
        }
    }
}
