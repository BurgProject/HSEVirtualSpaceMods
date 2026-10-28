using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HSEVerse.TerrainTools.Editor
{
    internal static class TerrainGeometry
    {
        // 128 x 128 heightmap cells per chunk; chunk boundaries share identical samples and normals.
        internal const int ChunkQuads = 128;
        // Heightmap step per LOD: every sample, every 2nd, 4th, 8th. The last level never culls.
        internal static readonly int[] LodSteps = { 1, 2, 4, 8 };
        internal static readonly float[] LodHeights = { 0.5f, 0.25f, 0.1f, 0f };
        // The hole collider uses the 1/4 level (collision needs no render-grade density).
        internal const int ColliderStep = 4;

        internal sealed class Chunk
        {
            public int X, Z;
            public Mesh[] Lods;
            public Mesh Collider;
        }

        internal static bool HasHoles(TerrainData data)
        {
            int res = data.holesResolution;
            var holes = data.GetHoles(0, 0, res, res);
            foreach (bool solid in holes) if (!solid) return true;
            return false;
        }

        /// <param name="lightmapUv">Adds the second UV set lightmaps read; left out otherwise (it only duplicated uv0).</param>
        /// <param name="colliderMesh">Also builds a 1/4-density mesh per chunk for MeshColliders (terrains with holes).</param>
        internal static List<Chunk> CreateChunks(TerrainData data, bool lightmapUv, bool colliderMesh)
        {
            int resolution = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            bool[,] holes = data.GetHoles(0, 0, data.holesResolution, data.holesResolution);
            var result = new List<Chunk>();
            for (int z = 0; z < resolution - 1; z += ChunkQuads)
            for (int x = 0; x < resolution - 1; x += ChunkQuads)
            {
                int qx = Mathf.Min(ChunkQuads, resolution - 1 - x), qz = Mathf.Min(ChunkQuads, resolution - 1 - z);
                // skirts hang below the chunk edges and hide the cracks between neighbours drawn at different LODs;
                // deep enough for the largest edge difference between the finest and the coarsest level
                float skirt = EdgeError(data, heights, x, z, qx, qz, LodSteps[LodSteps.Length - 1]) + 0.05f;
                var chunk = new Chunk { X = x, Z = z, Lods = new Mesh[LodSteps.Length] };
                for (int l = 0; l < LodSteps.Length; l++)
                    chunk.Lods[l] = Build(data, heights, holes, x, z, qx, qz, LodSteps[l], skirt, lightmapUv, true, $"Terrain_{x}_{z}_LOD{l}");
                if (colliderMesh)
                    chunk.Collider = Build(data, heights, holes, x, z, qx, qz, ColliderStep, 0f, false, false, $"Terrain_{x}_{z}_Collider");
                result.Add(chunk);
            }
            return result;
        }

        /// <summary>Sample offsets inside a chunk: every <paramref name="step"/>-th, always ending on the edge.</summary>
        private static List<int> Offsets(int quads, int step)
        {
            var list = new List<int>();
            for (int o = 0; o < quads; o += step) list.Add(o);
            list.Add(quads);
            return list;
        }

        private static float EdgeError(TerrainData data, float[,] heights, int x0, int z0, int qx, int qz, int step)
        {
            float worst = 0f;
            void Edge(bool alongX, int fixedIndex, int count)
            {
                var coarse = Offsets(count, step);
                for (int c = 0; c + 1 < coarse.Count; c++)
                {
                    int a = coarse[c], b = coarse[c + 1];
                    float ha = alongX ? heights[fixedIndex, x0 + a] : heights[z0 + a, fixedIndex];
                    float hb = alongX ? heights[fixedIndex, x0 + b] : heights[z0 + b, fixedIndex];
                    for (int i = a + 1; i < b; i++)
                    {
                        float h = alongX ? heights[fixedIndex, x0 + i] : heights[z0 + i, fixedIndex];
                        worst = Mathf.Max(worst, Mathf.Abs(h - Mathf.Lerp(ha, hb, (i - a) / (float)(b - a))));
                    }
                }
            }
            Edge(true, z0, qx); Edge(true, z0 + qz, qx);
            Edge(false, x0, qz); Edge(false, x0 + qx, qz);
            return worst * data.size.y;
        }

        private static Mesh Build(TerrainData data, float[,] heights, bool[,] holes, int startX, int startZ, int quadsX, int quadsZ,
            int step, float skirt, bool lightmapUv, bool shading, string name)
        {
            var xs = Offsets(quadsX, step);
            var zs = Offsets(quadsZ, step);
            int nx = xs.Count, nz = zs.Count;
            int surface = nx * nz;
            int edgeVerts = shading && skirt > 0f ? 2 * nx + 2 * nz : 0;
            int count = surface + edgeVerts;
            var vertices = new Vector3[count];
            var normals = shading ? new Vector3[count] : null;
            var tangents = shading ? new Vector4[count] : null;
            var uv = shading ? new Vector2[count] : null;
            int cells = data.heightmapResolution - 1;
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int k = j * nx + i;
                int hx = startX + xs[i], hz = startZ + zs[j];
                float u = (float)hx / cells, v = (float)hz / cells;
                vertices[k] = new Vector3(u * data.size.x, heights[hz, hx] * data.size.y, v * data.size.z);
                if (!shading) continue;
                normals[k] = data.GetInterpolatedNormal(u, v);
                // Terrain tangent points along +X; bitangent points along +Z.
                Vector3 tangent = Vector3.Cross(normals[k], Vector3.forward).normalized;
                tangents[k] = new Vector4(tangent.x, tangent.y, tangent.z, -1);
                uv[k] = new Vector2(u, v);
            }
            var triangles = new List<int>((nx - 1) * (nz - 1) * 6 + edgeVerts * 3);
            int holesW = holes.GetLength(1), holesH = holes.GetLength(0);
            for (int j = 0; j + 1 < nz; j++)
            for (int i = 0; i + 1 < nx; i++)
            {
                // a coarse quad is a hole when the fine cell at its centre is
                int cx = startX + (xs[i] + xs[i + 1]) / 2, cz = startZ + (zs[j] + zs[j + 1]) / 2;
                int hx = Mathf.Min(holesW - 1, cx * holesW / cells), hz = Mathf.Min(holesH - 1, cz * holesH / cells);
                if (!holes[hz, hx]) continue;
                int a = j * nx + i;
                triangles.Add(a); triangles.Add(a + nx); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + nx); triangles.Add(a + nx + 1);
            }
            if (edgeVerts > 0)
            {
                // one lowered copy of every edge vertex; faces wound to face outward (seen from outside the chunk)
                int next = surface;
                int[] Lower(List<int> top)
                {
                    var bottom = new int[top.Count];
                    for (int t = 0; t < top.Count; t++)
                    {
                        int src = top[t], dst = next++;
                        vertices[dst] = vertices[src] - Vector3.up * skirt;
                        normals[dst] = normals[src]; tangents[dst] = tangents[src]; uv[dst] = uv[src];
                        bottom[t] = dst;
                    }
                    return bottom;
                }
                var south = new List<int>(); var north = new List<int>();
                for (int i = 0; i < nx; i++) { south.Add(i); north.Add((nz - 1) * nx + i); }
                var west = new List<int>(); var east = new List<int>();
                for (int j = 0; j < nz; j++) { west.Add(j * nx); east.Add(j * nx + nx - 1); }
                var sB = Lower(south); var nB = Lower(north); var wB = Lower(west); var eB = Lower(east);
                void Quad(int tl, int tr, int br, int bl) { triangles.Add(tl); triangles.Add(tr); triangles.Add(br); triangles.Add(tl); triangles.Add(br); triangles.Add(bl); }
                for (int i = 0; i + 1 < nx; i++)
                {
                    Quad(south[i], south[i + 1], sB[i + 1], sB[i]);        // seen from -Z: +X to the right
                    Quad(north[i + 1], north[i], nB[i], nB[i + 1]);        // seen from +Z: +X to the left
                }
                for (int j = 0; j + 1 < nz; j++)
                {
                    Quad(west[j + 1], west[j], wB[j], wB[j + 1]);          // seen from -X: +Z to the left
                    Quad(east[j], east[j + 1], eB[j + 1], eB[j]);          // seen from +X: +Z to the right
                }
            }
            var mesh = new Mesh { name = name, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices;
            if (shading)
            {
                mesh.normals = normals;
                mesh.tangents = tangents;
                mesh.uv = uv;
                if (lightmapUv) mesh.uv2 = uv;
            }
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
