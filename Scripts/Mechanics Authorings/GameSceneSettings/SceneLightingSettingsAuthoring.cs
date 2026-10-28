using HSE.Core.Components;
using Unity.Entities;
using UnityEngine;

public class SceneLightingSettingsAuthoring : MonoBehaviour
{
    public UnityEngine.Rendering.AmbientMode AmbientMode = UnityEngine.Rendering.AmbientMode.Trilight;
    public float AmbientIntensity = 1.0f;

    [ColorUsageAttribute(false, true)]
    public Color AmbientSkyColor = Color.white;
    [ColorUsageAttribute(false, true)]
    public Color AmbientEquatorColor = Color.white;
    [ColorUsageAttribute(false, true)]
    public Color AmbientGroundColor = Color.white;

    class SceneLightingSettingsBaker : Baker<SceneLightingSettingsAuthoring>
    {
        public override void Bake(SceneLightingSettingsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SceneLightingSettingsComponent
            {
                AmbientIntensity = authoring.AmbientIntensity,
                AmbientSkyColor = authoring.AmbientSkyColor,
                AmbientEquatorColor = authoring.AmbientEquatorColor,
                AmbientGroundColor = authoring.AmbientGroundColor
            });
            AddComponent(entity, new AmbientModeComponent { Mode = (int)authoring.AmbientMode });
        }
    }
}