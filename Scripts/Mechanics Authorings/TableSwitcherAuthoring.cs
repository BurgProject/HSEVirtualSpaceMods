using HSE.Mechanics.Components;
using Unity.Entities;
using UnityEngine;

namespace HSE.Mechanics.Authorings
{
    public class TableSwitcherAuthoring : MonoBehaviour
    {
        public Material[] materials;
        public Texture2D[] texSwitches;
        public bool switchByTex;
        public Material[] GetMaterialsFromObj()
        {
            return GetComponent<MeshRenderer>().sharedMaterials;
        }

        public class Baker : Baker<TableSwitcherAuthoring>
        {

            public override void Bake(TableSwitcherAuthoring authoring)
            {
                MaterialChanger component = new MaterialChanger();

                component.texSwitches = authoring.texSwitches;
                component.materials = authoring.materials;
                component.switchByTex = authoring.switchByTex;
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponentObject(entity, component);
            }
        }
    }
}
