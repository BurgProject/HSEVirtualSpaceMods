using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using PhysicsCollider = Unity.Physics.PhysicsCollider;
using UnityPhysicsMaterial = UnityEngine.PhysicsMaterial;

namespace HSEVerse.TerrainTools.Editor
{
    // The converted terrain's collision: one Unity Physics height field over the TerrainData's heights (full
    // resolution, triangle contacts) as a static body, with the source TerrainCollider's material and layer rules
    // resolved the way Unity Physics resolves them for its own collider bakers. Only baked data reaches the client.
    internal sealed class TerrainHeightfieldBaker : Baker<HseTerrainHeightfield>
    {
        public override void Bake(HseTerrainHeightfield authoring)
        {
            var data = authoring.terrainData;
            if (!data) return;
            DependsOn(data);
            if (authoring.material) DependsOn(authoring.material);

            int res = data.heightmapResolution;
            var source = data.GetHeights(0, 0, res, res);
            var heights = new NativeArray<float>(res * res, Allocator.Temp);
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z * res + x] = source[z, x];

            int layer = GetLayer();
            uint include = 0u;
            for (int i = 0; i < 32; i++) include |= UnityEngine.Physics.GetIgnoreLayerCollision(layer, i) ? 0u : 1u << i;
            include |= (uint)authoring.includeLayers;
            include &= ~(uint)authoring.excludeLayers;
            var filter = new CollisionFilter { BelongsTo = 1u << layer, CollidesWith = include };

            var material = Unity.Physics.Material.Default;
            var physicsMaterial = authoring.material;
            material.Friction = physicsMaterial ? physicsMaterial.dynamicFriction : 0.6f;
            material.Restitution = physicsMaterial ? physicsMaterial.bounciness : 0f;
            material.FrictionCombinePolicy = Combine(physicsMaterial ? physicsMaterial.frictionCombine : PhysicsMaterialCombine.Average);
            material.RestitutionCombinePolicy = Combine(physicsMaterial ? physicsMaterial.bounceCombine : PhysicsMaterialCombine.Average);

            var scale = new float3(data.size.x / (res - 1), data.size.y, data.size.z / (res - 1));
            var blob = Unity.Physics.TerrainCollider.Create(heights, new int2(res, res), scale,
                Unity.Physics.TerrainCollider.CollisionMethod.Triangles, filter, material);
            heights.Dispose();
            AddBlobAsset(ref blob, out _);

            var entity = GetEntity(TransformUsageFlags.WorldSpace);
            AddComponent(entity, new PhysicsCollider { Value = blob });
            AddSharedComponent(entity, new PhysicsWorldIndex());
        }

        private static Unity.Physics.Material.CombinePolicy Combine(PhysicsMaterialCombine combine) => combine switch
        {
            PhysicsMaterialCombine.Maximum => Unity.Physics.Material.CombinePolicy.Maximum,
            PhysicsMaterialCombine.Minimum => Unity.Physics.Material.CombinePolicy.Minimum,
            PhysicsMaterialCombine.Multiply => Unity.Physics.Material.CombinePolicy.GeometricMean,
            _ => Unity.Physics.Material.CombinePolicy.ArithmeticMean,
        };
    }
}
