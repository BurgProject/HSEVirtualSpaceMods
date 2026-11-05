using HSE.Core.Components;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;

namespace HSE.Mechanics.Authorings
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class LocationSceneAuthoring : MonoBehaviour
    {
        [HideInInspector] public WeakObjectSceneReference Scene;

        private sealed class Baker : Baker<LocationSceneAuthoring>
        {
            public override void Bake(LocationSceneAuthoring authoring)
            {
                if (!authoring.Scene.IsReferenceValid)
                {
                    Debug.LogError("Assign a valid GameObject scene to Location Scene Reference.", authoring);
                    return;
                }

                AddComponent(GetEntity(TransformUsageFlags.None), new LocationSceneReference { Scene = authoring.Scene });
            }
        }
    }
}
