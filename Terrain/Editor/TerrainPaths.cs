using UnityEditor;
using UnityEngine;

namespace HSEVerse.TerrainTools.Editor
{
    internal static class TerrainPaths
    {
        internal const string Shaders = "Packages/com.hseverse.mods/Terrain/Shaders/";
        internal const string MeshShader = Shaders + "TerrainMesh.shader";           // per-4-layer passes (fallback)
        internal const string ArrayShader = Shaders + "TerrainMeshArray.shader";     // all layers in one pass
        internal const string BaseShader = Shaders + "TerrainMeshBase.shader";       // baked colour + normal (far LODs)
        internal const string GrassShader = Shaders + "TerrainGrass.shader";         // grass cards and tree impostors
        internal const string BakeShader = Shaders + "TerrainBaseBake.shader";       // renders the base textures
        internal const string RepackShader = Shaders + "TerrainNormalRepack.shader"; // normal maps -> two channels
        // Generated assets go to the project, never into the package (read-only where the package is installed).
        internal const string GeneratedFolder = "Assets/HSEVerseGenerated/Terrain";

        internal static Shader Load(string path)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (!shader) throw new System.InvalidOperationException("HSEVerse terrain shader is missing: " + path);
            return shader;
        }

        internal static void EnsureGeneratedFolder()
        {
            if (AssetDatabase.IsValidFolder(GeneratedFolder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/HSEVerseGenerated")) AssetDatabase.CreateFolder("Assets", "HSEVerseGenerated");
            AssetDatabase.CreateFolder("Assets/HSEVerseGenerated", "Terrain");
        }
    }
}
