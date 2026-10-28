using HSE.Core.Components;
using HSE.UI;
using Unity.Entities;
using UnityEngine;

namespace HSE.Mechanics.Authorings
{
    [DisallowMultipleComponent]
    public sealed class UiCanvasAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
        public PointerHintSettings HintSettings = PointerHintSettings.Default;

        private sealed class Baker : Baker<UiCanvasAuthoring>
        {
            public override void Bake(UiCanvasAuthoring authoring)
            {
                var prefab = authoring.Prefab;
                if (prefab != null && (prefab.scene.IsValid() || !UiCanvasUtility.Validate(prefab)))
                {
                    Debug.LogError("Assign a prefab with one enabled Screen Space Canvas on its root, or leave it empty.", authoring);
                    return;
                }
                if (prefab != null) DependsOn(prefab);
                AddComponentObject(GetEntity(TransformUsageFlags.None), new UiCanvasPrefab { Prefab = prefab, HintSettings = authoring.HintSettings });
            }
        }
    }
}
