using Unity.Entities;
using UnityEngine;

namespace HSE.Mechanics.Components
{
    public class MaterialChanger : IComponentData
    {
        public Material[] materials;
        public Texture2D[] texSwitches;

        public bool switchByTex;
    }
}

