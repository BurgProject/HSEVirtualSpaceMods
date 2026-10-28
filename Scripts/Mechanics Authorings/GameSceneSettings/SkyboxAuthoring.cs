using HSE.Core.Components;
using Unity.Entities;
using UnityEngine;

public class SkyboxAuthoring : MonoBehaviour
{
    public Material SkyboxMaterial;

    class Baker : Baker<SkyboxAuthoring>
    {
        public override void Bake(SkyboxAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SkyboxMaterialComponent
            {
                Material = authoring.SkyboxMaterial
            });
        }
    }
}