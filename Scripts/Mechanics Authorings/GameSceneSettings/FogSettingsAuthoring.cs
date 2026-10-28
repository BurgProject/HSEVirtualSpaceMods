using HSE.Core.Components;
using Unity.Entities;
using UnityEngine;

public class FogSettingsAuthoring : MonoBehaviour
{
    public bool EnableFog = true;
    public Color FogColor = Color.gray;
    public float FogDensity = 0.01f;
    public float FogStartDistance = 10f;
    public float FogEndDistance = 100f;
    public FogMode FogMode = FogMode.Exponential;

    class FogSettingsBaker : Baker<FogSettingsAuthoring>
    {
        public override void Bake(FogSettingsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new FogSettingsComponent
            {
                EnableFog = authoring.EnableFog,
                FogColor = authoring.FogColor,
                FogDensity = authoring.FogDensity,
                FogStartDistance = authoring.FogStartDistance,
                FogEndDistance = authoring.FogEndDistance
            });
            AddComponent(entity, new FogModeComponent { Mode = (int)authoring.FogMode });
        }
    }
}