using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HSEVerse.TerrainTools.Editor
{
    /// <summary>
    /// A tree prototype's last LOD: two crossed quads showing the tree's first LOD from the front and from the side,
    /// rendered once (unlit albedo, so the quads take the scene's lighting like the grass) into a 512 x 512 atlas.
    /// Needs a graphics device - built by manual conversions only.
    /// </summary>
    internal static class TerrainImpostors
    {
        private const int ViewW = 256, ViewH = 512;

        internal static HseTerrainGpuCache.Impostor Build(GameObject prefab)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var temporary = new List<Object>();
            try
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (!instance)
                {
                    instance = Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                }
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var drawn = PrepareFirstLod(instance, temporary);
                if (drawn.Count == 0)
                {
                    Debug.LogWarning($"HSEVerse: tree {prefab.name} has nothing to render for an impostor; it keeps plain culling.", prefab);
                    return null;
                }
                var bounds = WorldBounds(drawn);

                var cameraGo = new GameObject("ImpostorCamera");
                SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var camera = cameraGo.AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.aspect = ViewW / (float)ViewH;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.renderShadows = false;
                // half height = ortho; half width = ortho * aspect (= ortho / 2) - both views must fit
                float ortho = Mathf.Max(bounds.size.y * 0.5f, bounds.size.x, bounds.size.z) * 1.02f;
                camera.orthographicSize = ortho;
                float distance = bounds.extents.magnitude * 2f + 1f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = distance * 2f;

                var atlas = new Texture2D(ViewW * 2, ViewH, TextureFormat.RGBA32, true, false) { name = prefab.name + "_ImpostorAtlas" };
                var pixels = new Color32[ViewW * 2 * ViewH];
                // front: looking along +Z (image right = +X); side: looking along -X (image right = +Z)
                Capture(camera, bounds.center - Vector3.forward * distance, Quaternion.LookRotation(Vector3.forward), pixels, 0);
                Capture(camera, bounds.center + Vector3.right * distance, Quaternion.LookRotation(Vector3.left), pixels, ViewW);
                Dilate(pixels, ViewW * 2, ViewH);
                atlas.SetPixels32(pixels);
                atlas.Apply(true);
                EditorUtility.CompressTexture(atlas, TextureFormat.BC7, TextureCompressionQuality.Normal);
                atlas.wrapMode = TextureWrapMode.Clamp;
                atlas.Apply(false, false);

                var material = new Material(TerrainPaths.Load(TerrainPaths.GrassShader)) { name = prefab.name + "_Impostor", enableInstancing = true };
                material.SetTexture("_BaseMap", atlas);
                material.SetFloat("_Cutoff", 0.5f);
                return new HseTerrainGpuCache.Impostor
                {
                    prefab = prefab,
                    mesh = BuildMesh(prefab.name, instance.transform, bounds, ortho),
                    material = material,
                    atlas = atlas,
                };
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var o in temporary) if (o) Object.DestroyImmediate(o);
            }
        }

        /// <summary>Leave only the first LOD's mesh renderers on, drawing unlit copies of their materials.</summary>
        private static List<Renderer> PrepareFirstLod(GameObject instance, List<Object> temporary)
        {
            var inLod = new HashSet<Renderer>();
            var firstLod = new HashSet<Renderer>();
            foreach (var group in instance.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                for (int l = 0; l < lods.Length; l++)
                    foreach (var r in lods[l].renderers)
                        if (r) { inLod.Add(r); if (l == 0) firstLod.Add(r); }
                group.enabled = false;
            }
            var drawn = new List<Renderer>();
            var unlit = new Dictionary<Material, Material>();
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                bool on = r.enabled && r.gameObject.activeInHierarchy && (r is MeshRenderer || r is SkinnedMeshRenderer)
                          && (!inLod.Contains(r) || firstLod.Contains(r));
                r.enabled = on;
                if (!on) continue;
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i]) continue;
                    if (!unlit.TryGetValue(materials[i], out var copy))
                    {
                        copy = Unlit(materials[i]);
                        temporary.Add(copy);
                        unlit.Add(materials[i], copy);
                    }
                    materials[i] = copy;
                }
                r.sharedMaterials = materials;
                drawn.Add(r);
            }
            return drawn;
        }

        private static Material Unlit(Material source)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
            string map = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
            if (map != null && source.GetTexture(map))
            {
                m.SetTexture("_BaseMap", source.GetTexture(map));
                m.SetTextureScale("_BaseMap", source.GetTextureScale(map));
                m.SetTextureOffset("_BaseMap", source.GetTextureOffset(map));
            }
            string color = source.HasProperty("_BaseColor") ? "_BaseColor" : source.HasProperty("_Color") ? "_Color" : null;
            m.SetColor("_BaseColor", color != null ? source.GetColor(color) : Color.white);
            bool clip = source.IsKeywordEnabled("_ALPHATEST_ON") || (source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f)
                        || (source.renderQueue >= (int)RenderQueue.AlphaTest && source.renderQueue < (int)RenderQueue.GeometryLast);
            if (clip)
            {
                m.SetFloat("_AlphaClip", 1);
                m.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            m.SetFloat("_Cull", (float)CullMode.Off);
            return m;
        }

        private static Bounds WorldBounds(List<Renderer> renderers)
        {
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>Two renders (black and white background): the difference is the coverage, so the result has a
        /// correct alpha whatever the pipeline does with the target's alpha channel.</summary>
        private static void Capture(Camera camera, Vector3 position, Quaternion rotation, Color32[] into, int xOffset)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            var onBlack = Render(camera, Color.black);
            var onWhite = Render(camera, Color.white);
            int atlasW = ViewW * 2;
            for (int y = 0; y < ViewH; y++)
            for (int x = 0; x < ViewW; x++)
            {
                var b = onBlack[y * ViewW + x];
                var w = onWhite[y * ViewW + x];
                float gap = Mathf.Max(w.r - b.r, Mathf.Max(w.g - b.g, w.b - b.b)) / 255f;
                float a = Mathf.Clamp01(1f - gap);
                var c = a > 0.01f
                    ? new Color32((byte)Mathf.Min(255, b.r / a), (byte)Mathf.Min(255, b.g / a), (byte)Mathf.Min(255, b.b / a), (byte)(a * 255))
                    : new Color32(0, 0, 0, 0);
                into[y * atlasW + xOffset + x] = c;
            }
        }

        private static Color32[] Render(Camera camera, Color background)
        {
            camera.backgroundColor = background;
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(ViewW, ViewH, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
            var previous = RenderTexture.active;
            try
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                {
                    camera.targetTexture = rt;
                    camera.Render();
                    camera.targetTexture = null;
                }
                RenderTexture.active = rt;
                var tex = new Texture2D(ViewW, ViewH, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, ViewW, ViewH), 0, 0, false);
                tex.Apply(false);
                var px = tex.GetPixels32();
                Object.DestroyImmediate(tex);
                return px;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>Spread edge colours into the transparent area, so mip levels do not bleed black into the leaves.</summary>
        private static void Dilate(Color32[] px, int w, int h)
        {
            var filled = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) filled[i] = px[i].a >= 128;
            for (int pass = 0; pass < 8; pass++)
            {
                var grow = new List<(int index, Color32 color)>();
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (filled[i]) continue;
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (!filled[j]) continue;
                        r += px[j].r; g += px[j].g; b += px[j].b; n++;
                    }
                    if (n > 0) grow.Add((i, new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), px[i].a)));
                }
                if (grow.Count == 0) break;
                foreach (var (index, color) in grow) { px[index] = color; filled[index] = true; }
            }
        }

        /// <summary>Two crossed quads in the prefab root's local space (the instance transform adds position,
        /// rotation and scale), sized to the rendered frames.</summary>
        private static Mesh BuildMesh(string name, Transform root, Bounds worldBounds, float ortho)
        {
            var scale = root.lossyScale;
            var c = root.InverseTransformPoint(worldBounds.center);
            float halfH = ortho / Mathf.Max(1e-4f, Mathf.Abs(scale.y));
            float halfX = ortho * 0.5f / Mathf.Max(1e-4f, Mathf.Abs(scale.x));
            float halfZ = ortho * 0.5f / Mathf.Max(1e-4f, Mathf.Abs(scale.z));
            var vertices = new[]
            {
                new Vector3(c.x - halfX, c.y - halfH, c.z), new Vector3(c.x + halfX, c.y - halfH, c.z),
                new Vector3(c.x - halfX, c.y + halfH, c.z), new Vector3(c.x + halfX, c.y + halfH, c.z),
                new Vector3(c.x, c.y - halfH, c.z - halfZ), new Vector3(c.x, c.y - halfH, c.z + halfZ),
                new Vector3(c.x, c.y + halfH, c.z - halfZ), new Vector3(c.x, c.y + halfH, c.z + halfZ),
            };
            var uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            };
            var normals = new Vector3[8];
            var colors = new Color[8];
            for (int i = 0; i < 8; i++) { normals[i] = Vector3.up; colors[i] = Color.white; }
            var mesh = new Mesh { name = name + "_Impostor", indexFormat = IndexFormat.UInt16 };
            mesh.vertices = vertices; mesh.uv = uv; mesh.normals = normals; mesh.colors = colors;
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
