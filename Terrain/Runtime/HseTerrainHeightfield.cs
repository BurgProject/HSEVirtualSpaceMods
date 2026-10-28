using UnityEngine;

namespace HSEVerse.TerrainTools
{
    // Authoring only: asks the baker for a Unity Physics height-field collider built from the TerrainData's heights,
    // in place of one MeshCollider per chunk. Placed on the converted root when the terrain has no holes (height
    // fields cannot have holes). Nothing reads it at runtime; the client only gets the baked PhysicsCollider.
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class HseTerrainHeightfield : MonoBehaviour
    {
        [HideInInspector] public TerrainData terrainData;
        [HideInInspector] public PhysicsMaterial material;
        [HideInInspector] public int includeLayers;
        [HideInInspector] public int excludeLayers;
    }
}
