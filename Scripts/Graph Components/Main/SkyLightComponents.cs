using Unity.Entities;
using UnityEngine;

namespace HSE.Core.Components
{
    public struct SkyboxMaterialComponent : IComponentData
    {
        public UnityObjectRef<Material> Material;
    }

    public struct FogSettingsComponent : IComponentData
    {
        public bool EnableFog;
        public Color FogColor;
        public float FogDensity;
        public float FogStartDistance;
        public float FogEndDistance;
    }

    public struct SceneLightingSettingsComponent : IComponentData
    {
        public float AmbientIntensity;
        public Color AmbientSkyColor;
        public Color AmbientEquatorColor;
        public Color AmbientGroundColor;
    }

    // Separate from FogSettingsComponent / SceneLightingSettingsComponent on purpose: changing those structs would
    // change their type hash and break every room already baked with them. Rooms without these keep the old
    // behaviour (exponential fog, three-colour ambient).

    /// <summary>UnityEngine.FogMode as int: 1 linear, 2 exponential, 3 exponential squared.</summary>
    public struct FogModeComponent : IComponentData
    {
        public int Mode;
    }

    /// <summary>UnityEngine.Rendering.AmbientMode as int: 0 skybox, 1 three colours, 3 one colour (the sky colour).</summary>
    public struct AmbientModeComponent : IComponentData
    {
        public int Mode;
    }
}

