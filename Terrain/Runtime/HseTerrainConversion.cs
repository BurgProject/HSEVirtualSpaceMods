using UnityEngine;

namespace HSEVerse.TerrainTools
{
    // Authoring metadata only. No runtime ECS component or client-side system is required.
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class HseTerrainConversion : MonoBehaviour
    {
        [HideInInspector] public GameObject generatedRoot;
        [HideInInspector] public bool terrainWasEnabled;
        [HideInInspector] public bool colliderWasEnabled;
        [HideInInspector] public string assetPath;
    }
}
