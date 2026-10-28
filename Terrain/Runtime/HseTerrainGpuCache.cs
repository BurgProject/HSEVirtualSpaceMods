using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSEVerse.TerrainTools
{
    // What a manual conversion builds on the GPU, kept as an asset next to the other generated data so conversions
    // without a GPU (the SubScene importer's worker, a -nographics build) can still use it. Each part carries the
    // hash of its inputs; a stale part is ignored and that conversion falls back (per-4-layer passes, no base map,
    // plain tree culling) until the next manual conversion rebuilds it.
    public sealed class HseTerrainGpuCache : ScriptableObject
    {
        [Serializable]
        public sealed class Impostor
        {
            public GameObject prefab;
            public string hash;
            public Mesh mesh;
            public Material material;
            public Texture2D atlas;
        }

        // layer textures packed into arrays, one slice per TerrainLayer (see TerrainTextureArrays)
        public string layersHash;
        public Texture2DArray albedo;
        public Texture2DArray normals;
        public Texture2DArray masks;
        public Texture2D layerParams;
        public bool hasNormals;
        public bool hasMasks;

        // the blended surface baked into one colour + one normal texture, for the far LODs
        public string controlsHash;
        public Texture2D baseAlbedo;
        public Texture2D baseNormal;

        // one crossed-quad impostor per tree prototype
        public List<Impostor> impostors = new List<Impostor>();
    }
}
