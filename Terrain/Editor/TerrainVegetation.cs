using System;
using System.Collections.Generic;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HSEVerse.TerrainTools.Editor
{
    internal static class TerrainVegetation
    {
        // Entities Graphics evaluates LODs the Unity way: screen height = size / (distance * 2 tan(fov / 2)).
        // Distances are turned into heights for the client's 60 degree camera.
        private const float ClientFov = 60f;
        private static readonly float TanTimesTwo = 2f * Mathf.Tan(ClientFov * 0.5f * Mathf.Deg2Rad);
        internal const float GrassCell = 32f;

        internal static float ScreenHeight(float size, float distance) => distance > 0f ? size / (distance * TanTimesTwo) : 0f;

        internal static void CreateTrees(Terrain terrain, Transform parent, bool treeColliders, bool drawTrees, HseTerrainGpuCache cache, Action<Object> own)
        {
            var data = terrain.terrainData;
            var prototypes = data.treePrototypes;
            var compatibleMaterials = new TerrainVegetationMaterials(own);
            var tintMaterials = new Dictionary<(Material, Color32, Color32), Material>();
            var reportedBillboards = new HashSet<GameObject>();
            int index = 0;
            foreach (var tree in data.treeInstances)
            {
                if (tree.prototypeIndex < 0 || tree.prototypeIndex >= prototypes.Length || !prototypes[tree.prototypeIndex].prefab)
                    throw new InvalidOperationException($"{terrain.name}: tree {index} references a missing prototype.");
                var prefab = prototypes[tree.prototypeIndex].prefab;
                var instance = Object.Instantiate(prefab, parent);
                instance.name = $"Tree_{index++}_{prefab.name}";
                instance.transform.localPosition = Vector3.Scale(tree.position, data.size);
                instance.transform.localRotation = Quaternion.Euler(0, tree.rotation * Mathf.Rad2Deg, 0);
                instance.transform.localScale = Vector3.Scale(prefab.transform.localScale, new Vector3(tree.widthScale, tree.heightScale, tree.widthScale));
                ReplaceBillboardLods(instance, reportedBillboards.Add(prefab));
                if (!treeColliders)
                    foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                bool tinted = !(tree.color.Equals((Color32)Color.white) && tree.lightmapColor.Equals((Color32)Color.white));
                // Keep prefab hierarchy, original meshes, materials, LODGroups and colliders.
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = compatibleMaterials.Get(materials[i]);
                    if (tinted) Tint(renderer, materials, tree, tintMaterials, own);
                    renderer.sharedMaterials = materials;
                }
                ApplyTreeLods(instance, terrain, TerrainGpuCacheBuilder.FindImpostor(cache, prefab));
                if (!drawTrees)
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            }
        }

        /// <summary>
        /// A single-material renderer on a URP shader gets its tint as a per-entity base-colour override (Entities
        /// Graphics' own URPMaterialPropertyBaseColor): one material for every tint. Anything else keeps a tinted
        /// material copy, with the tint rounded to 16 steps per channel so thousands of random tints make a handful
        /// of copies.
        /// </summary>
        private static void Tint(Renderer renderer, Material[] materials, TreeInstance tree,
            Dictionary<(Material, Color32, Color32), Material> copies, Action<Object> own)
        {
            if (materials.Length == 1 && SupportsBaseColorOverride(materials[0]))
            {
                var authoring = renderer.GetComponent<URPMaterialPropertyBaseColorAuthoring>();
                Color current = authoring ? authoring.color : materials[0].GetColor("_BaseColor");
                if (!authoring) authoring = renderer.gameObject.AddComponent<URPMaterialPropertyBaseColorAuthoring>();
                authoring.color = current * (Color)tree.color;
                return;
            }
            Color32 color = Quantize(tree.color), lightmap = Quantize(tree.lightmapColor);
            for (int i = 0; i < materials.Length; i++)
            {
                var original = materials[i];
                var key = (original, color, lightmap);
                if (!copies.TryGetValue(key, out var copy))
                {
                    copy = new Material(original) { name = original.name + "_TerrainTint", enableInstancing = true };
                    Color tint = color;
                    if (copy.HasProperty("_TreeInstanceColor")) copy.SetColor("_TreeInstanceColor", tint);
                    else if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", copy.GetColor("_BaseColor") * tint);
                    else if (copy.HasProperty("_Color")) copy.SetColor("_Color", copy.GetColor("_Color") * tint);
                    if (copy.HasProperty("_TreeInstanceLightmapColor")) copy.SetColor("_TreeInstanceLightmapColor", (Color)lightmap);
                    own(copy);
                    copies.Add(key, copy);
                }
                materials[i] = copy;
            }
        }

        private static bool SupportsBaseColorOverride(Material m) =>
            m && m.shader && m.HasProperty("_BaseColor") &&
            (m.shader.name == "Universal Render Pipeline/Lit" || m.shader.name == "Universal Render Pipeline/Simple Lit" ||
             m.shader.name == "Universal Render Pipeline/Unlit");

        private static Color32 Quantize(Color32 c)
        {
            byte Q(byte v) => (byte)(Mathf.RoundToInt(v / 17f) * 17);
            return new Color32(Q(c.r), Q(c.g), Q(c.b), Q(c.a));
        }

        /// <summary>
        /// The terrain's own tree distances, applied to each tree's LOD group: with an impostor, the last mesh LOD
        /// hands over at the terrain's billboard distance and the impostor draws until the tree distance; without one,
        /// the last mesh LOD is culled at the tree distance. A prefab's own earlier switches and culls are kept.
        /// </summary>
        private static void ApplyTreeLods(GameObject instance, Terrain terrain, HseTerrainGpuCache.Impostor impostor)
        {
            var group = instance.GetComponent<LODGroup>();
            if (!group)
            {
                if (instance.GetComponentInChildren<LODGroup>(true)) return; // nested groups: left as authored
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) return;
                group = instance.AddComponent<LODGroup>();
                var local = LocalBounds(instance.transform, renderers);
                group.localReferencePoint = local.center;
                group.size = Mathf.Max(local.size.x, local.size.y, local.size.z);
                group.SetLODs(new[] { new LOD(0f, renderers) });
            }
            var lods = new List<LOD>(group.GetLODs());
            if (lods.Count == 0) return;
            var s = instance.transform.lossyScale;
            float size = group.size * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            float cull = ScreenHeight(size, terrain.treeDistance);
            float billboard = ScreenHeight(size, terrain.treeBillboardDistance);
            int k = lods.Count - 1;
            float upper = k > 0 ? lods[k - 1].screenRelativeTransitionHeight * 0.99f : 1f;
            var last = lods[k];
            if (impostor != null && billboard > cull && lods.Count < 8)
            {
                last.screenRelativeTransitionHeight = Mathf.Min(upper, Mathf.Max(last.screenRelativeTransitionHeight, billboard));
                lods[k] = last;
                var go = new GameObject("Impostor");
                go.transform.SetParent(instance.transform, false);
                go.layer = instance.layer;
                go.AddComponent<MeshFilter>().sharedMesh = impostor.mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = impostor.material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
                lods.Add(new LOD(Mathf.Min(cull, last.screenRelativeTransitionHeight * 0.99f), new Renderer[] { r }));
            }
            else
            {
                last.screenRelativeTransitionHeight = Mathf.Min(upper, Mathf.Max(last.screenRelativeTransitionHeight, cull));
                lods[k] = last;
            }
            group.SetLODs(lods.ToArray());
        }

        /// <summary>Renderer mesh bounds in <paramref name="space"/>'s local space (valid under inactive parents,
        /// where Renderer.bounds is empty).</summary>
        private static Bounds LocalBounds(Transform space, IList<Renderer> renderers)
        {
            bool any = false;
            var result = new Bounds();
            foreach (var r in renderers)
            {
                var filter = r ? r.GetComponent<MeshFilter>() : null;
                if (!filter || !filter.sharedMesh) continue;
                var m = space.worldToLocalMatrix * r.transform.localToWorldMatrix;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        /// <summary>
        /// Details are baked into static meshes grouped in <see cref="GrassCell"/> m cells; each cell is one LOD group
        /// culled at the terrain's detail distance, so grass only draws near the camera (as Terrain does).
        /// </summary>
        internal static void CreateDetails(Terrain terrain, Transform parent, Action<Object> own)
        {
            var data = terrain.terrainData;
            if (data.detailResolution == 0 || data.detailPrototypes.Length == 0) return;
            var prototypes = data.detailPrototypes;
            var compatibleMaterials = new TerrainVegetationMaterials(own);
            int patchCount = data.detailPatchCount;
            int count = 0;
            var cells = new Dictionary<Vector2Int, (GameObject go, List<Renderer> renderers)>();
            Transform Cell(Vector2Int key)
            {
                if (!cells.TryGetValue(key, out var cell))
                {
                    var go = new GameObject($"GrassCell_{key.x}_{key.y}");
                    go.transform.SetParent(parent, false);
                    go.layer = parent.gameObject.layer;
                    cell = (go, new List<Renderer>());
                    cells.Add(key, cell);
                }
                return cell.go.transform;
            }
            for (int layer = 0; layer < prototypes.Length; layer++)
            {
                var prototype = prototypes[layer];
                if (!prototype.Validate(out string error))
                    throw new InvalidOperationException($"{terrain.name}: detail {layer}: {error}");
                Material grassMaterial = null;
                if (!prototype.usePrototypeMesh)
                {
                    grassMaterial = new Material(TerrainPaths.Load(TerrainPaths.GrassShader)) { name = $"Grass_{layer}", enableInstancing = true };
                    grassMaterial.SetTexture("_BaseMap", prototype.prototypeTexture);
                    grassMaterial.SetFloat("_Cutoff", 0.5f);
                    own(grassMaterial);
                }
                var byCell = new Dictionary<Vector2Int, List<DetailInstanceTransform>>();
                for (int z = 0; z < patchCount; z++)
                for (int x = 0; x < patchCount; x++)
                {
                    var transforms = data.ComputeDetailInstanceTransforms(x, z, layer, terrain.detailObjectDensity, out _);
                    foreach (var t in transforms)
                    {
                        var key = new Vector2Int(Mathf.FloorToInt(t.posX / GrassCell), Mathf.FloorToInt(t.posZ / GrassCell));
                        if (!byCell.TryGetValue(key, out var list)) byCell.Add(key, list = new List<DetailInstanceTransform>());
                        list.Add(t);
                    }
                }
                foreach (var pair in byCell)
                {
                    var transforms = pair.Value.ToArray();
                    count += transforms.Length;
                    var cellTransform = Cell(pair.Key);
                    var made = prototype.usePrototypeMesh
                        ? CreateMeshDetails(prototype, transforms, cellTransform, layer, pair.Key, own, compatibleMaterials)
                        : CreateGrass(prototype, transforms, cellTransform, grassMaterial, layer, pair.Key, own);
                    cells[pair.Key].renderers.AddRange(made);
                }
            }
            foreach (var cell in cells.Values)
            {
                var bounds = LocalBounds(cell.go.transform, cell.renderers);
                var group = cell.go.AddComponent<LODGroup>();
                group.localReferencePoint = bounds.center;
                group.size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 1f);
                float height = Mathf.Clamp(ScreenHeight(group.size, terrain.detailObjectDistance), 0.0001f, 1f);
                group.SetLODs(new[] { new LOD(height, cell.renderers.ToArray()) });
            }
            if (count > 0)
                Debug.Log($"{terrain.name}: converted {count:N0} detail instances in {cells.Count} cells at Terrain density {terrain.detailObjectDensity}, culled beyond {terrain.detailObjectDistance} m. Grass cards use fixed crossed planes; Terrain wind, billboard facing and distance fading are not reproduced.", terrain);
        }

        private static Matrix4x4 DetailMatrix(DetailInstanceTransform t) => Matrix4x4.TRS(
            new Vector3(t.posX, t.posY, t.posZ), Quaternion.Euler(0, t.rotationY * Mathf.Rad2Deg, 0),
            new Vector3(t.scaleXZ, t.scaleY, t.scaleXZ));

        private static List<Renderer> CreateMeshDetails(DetailPrototype prototype, DetailInstanceTransform[] transforms,
            Transform parent, int layer, Vector2Int cell, Action<Object> own, TerrainVegetationMaterials compatibleMaterials)
        {
            var made = new List<Renderer>();
            var prefab = prototype.prototype;
            if (!prefab) throw new InvalidOperationException($"Detail layer {layer} has no prefab.");
            var lod = prefab.GetComponent<LODGroup>();
            var renderers = lod && lod.lodCount > 0 ? lod.GetLODs()[0].renderers : prefab.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (!(renderer is MeshRenderer meshRenderer) || !renderer.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh)
                    throw new InvalidOperationException($"Detail prefab {prefab.name} must contain static MeshRenderers with MeshFilters.");
                var source = filter.sharedMesh;
                var materials = meshRenderer.sharedMaterials;
                var toRoot = prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                toRoot = Matrix4x4.Scale(prefab.transform.localScale) * toRoot;
                for (int submesh = 0; submesh < source.subMeshCount; submesh++)
                {
                    if (materials.Length == 0 || !materials[Mathf.Min(submesh, materials.Length - 1)])
                        throw new InvalidOperationException($"Detail prefab {prefab.name} has a missing material.");
                    // Bound combined mesh sizes even on very dense detail cells (and keep 16-bit indices).
                    int batchSize = Mathf.Max(1, 60000 / Mathf.Max(1, source.vertexCount));
                    for (int start = 0; start < transforms.Length; start += batchSize)
                    {
                        int length = Mathf.Min(batchSize, transforms.Length - start);
                        var combines = new CombineInstance[length];
                        for (int i = 0; i < length; i++)
                            combines[i] = new CombineInstance { mesh = source, subMeshIndex = submesh, transform = DetailMatrix(transforms[start + i]) * toRoot };
                        var mesh = new Mesh { name = $"Detail_{layer}_{cell.x}_{cell.y}_{renderer.name}_{submesh}_{start}" };
                        mesh.indexFormat = length * source.vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                        mesh.CombineMeshes(combines, true, true, false);
                        own(mesh);
                        made.Add(CreateRenderer(parent, mesh, compatibleMaterials.Get(materials[Mathf.Min(submesh, materials.Length - 1)]), meshRenderer.shadowCastingMode));
                    }
                }
            }
            return made;
        }

        private static List<Renderer> CreateGrass(DetailPrototype prototype, DetailInstanceTransform[] transforms,
            Transform parent, Material material, int layer, Vector2Int cell, Action<Object> own)
        {
            var made = new List<Renderer>();
            const int batchSize = 4096; // 8 vertices each: 32768, inside 16-bit indices
            for (int start = 0; start < transforms.Length; start += batchSize)
            {
                int count = Mathf.Min(batchSize, transforms.Length - start);
                var vertices = new Vector3[count * 8];
                var normals = new Vector3[vertices.Length];
                var uv = new Vector2[vertices.Length];
                var colors = new Color[vertices.Length];
                var triangles = new int[count * 12];
                for (int i = 0; i < count; i++)
                {
                    var t = transforms[start + i];
                    var matrix = DetailMatrix(t);
                    float noise = Mathf.PerlinNoise(t.posX / Mathf.Max(0.01f, prototype.noiseSpread), t.posZ / Mathf.Max(0.01f, prototype.noiseSpread));
                    var color = Color.Lerp(prototype.dryColor, prototype.healthyColor, noise).linear;
                    for (int plane = 0; plane < 2; plane++)
                    {
                        int v = i * 8 + plane * 4;
                        Vector3 right = plane == 0 ? Vector3.right : Vector3.forward;
                        vertices[v] = matrix.MultiplyPoint3x4(-right * 0.5f);
                        vertices[v + 1] = matrix.MultiplyPoint3x4(right * 0.5f);
                        vertices[v + 2] = matrix.MultiplyPoint3x4(-right * 0.5f + Vector3.up);
                        vertices[v + 3] = matrix.MultiplyPoint3x4(right * 0.5f + Vector3.up);
                        uv[v] = new Vector2(0, 0); uv[v + 1] = new Vector2(1, 0);
                        uv[v + 2] = new Vector2(0, 1); uv[v + 3] = new Vector2(1, 1);
                        for (int j = 0; j < 4; j++) { normals[v + j] = Vector3.up; colors[v + j] = color; }
                        int k = i * 12 + plane * 6;
                        triangles[k] = v; triangles[k + 1] = v + 2; triangles[k + 2] = v + 1;
                        triangles[k + 3] = v + 1; triangles[k + 4] = v + 2; triangles[k + 5] = v + 3;
                    }
                }
                var mesh = new Mesh { name = $"Grass_{layer}_{cell.x}_{cell.y}_{start}", indexFormat = IndexFormat.UInt16 };
                mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.colors = colors;
                mesh.triangles = triangles; mesh.RecalculateBounds();
                own(mesh);
                made.Add(CreateRenderer(parent, mesh, material, ShadowCastingMode.Off));
            }
            return made;
        }

        private static Renderer CreateRenderer(Transform parent, Mesh mesh, Material material, ShadowCastingMode shadows)
        {
            var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = true;
            return renderer;
        }

        private static void ReplaceBillboardLods(GameObject instance, bool report)
        {
            foreach (var group in instance.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                var supported = new List<LOD>();
                bool changed = false;
                foreach (var lod in lods)
                {
                    var renderers = new List<Renderer>();
                    foreach (var renderer in lod.renderers)
                    {
                        if (renderer is BillboardRenderer) { renderer.enabled = false; changed = true; }
                        else if (renderer) renderers.Add(renderer);
                    }
                    if (renderers.Count > 0) supported.Add(new LOD(lod.screenRelativeTransitionHeight, renderers.ToArray()) { fadeTransitionWidth = lod.fadeTransitionWidth });
                }
                if (!changed) continue;
                if (supported.Count == 0) throw new InvalidOperationException($"Tree {instance.name} has only BillboardRenderers and no mesh LOD to export.");
                var last = supported[supported.Count - 1];
                last.screenRelativeTransitionHeight = lods[lods.Length - 1].screenRelativeTransitionHeight;
                supported[supported.Count - 1] = last;
                group.SetLODs(supported.ToArray());
                if (report) Debug.LogWarning($"HSEVerse: {instance.name} uses its last mesh LOD instead of a Terrain BillboardRenderer in ECS (an impostor replaces it when the manual conversion builds one).");
            }
        }
    }
}
