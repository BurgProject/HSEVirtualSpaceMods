using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HSE.CodeCraft
{
    [BakingType]
    public struct GraphHierarchyBakingMember : IBufferElementData
    {
        public Entity Value;
    }

    public static class GraphHierarchyBaking
    {
        /// <summary>Обычные потомки принадлежат ближайшему CraftObject; вложенные CraftObject создаются отдельно.</summary>
        public static void Build(EntityManager manager, IReadOnlyList<Entity> roots)
        {
            var owners = new Dictionary<Entity, Entity>();
            var groups = new Dictionary<Entity, HashSet<Entity>>();
            foreach (var root in roots)
            {
                owners.Add(root, root);
                groups.Add(root, new HashSet<Entity> { root });
            }

            using var query = manager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Parent>() },
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            });
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var owner = FindOwner(manager, entity, owners, new HashSet<Entity>());
                if (owner != Entity.Null) groups[owner].Add(entity);
            }

            // Сохраняем дополнительные сущности Baker, которые могут не иметь Parent.
            foreach (var root in roots)
            {
                if (!manager.HasBuffer<LinkedEntityGroup>(root)) continue;
                var previous = new HashSet<Entity>();
                if (manager.HasBuffer<GraphHierarchyBakingMember>(root))
                    foreach (var member in manager.GetBuffer<GraphHierarchyBakingMember>(root)) previous.Add(member.Value);

                foreach (var member in manager.GetBuffer<LinkedEntityGroup>(root))
                {
                    if (!manager.Exists(member.Value)) continue;
                    var owner = FindOwner(manager, member.Value, owners, new HashSet<Entity>());
                    if (owner != Entity.Null && owner != root) continue;
                    if (owner == Entity.Null && previous.Contains(member.Value)) continue;
                    groups[root].Add(member.Value);
                }
            }

            var assigned = new HashSet<Entity>();
            foreach (var root in roots)
            {
                var members = new List<Entity>(groups[root]);
                members.Remove(root);
                members.Sort((left, right) => left.Index.CompareTo(right.Index));
                members.Insert(0, root);
                foreach (var member in members)
                    if (!assigned.Add(member))
                        throw new InvalidOperationException("Graph prefab groups share an entity: " + member);

                var buffer = manager.HasBuffer<LinkedEntityGroup>(root)
                    ? manager.GetBuffer<LinkedEntityGroup>(root) : manager.AddBuffer<LinkedEntityGroup>(root);
                buffer.Clear();
                foreach (var member in members) buffer.Add(new LinkedEntityGroup { Value = member });

                var generated = manager.HasBuffer<GraphHierarchyBakingMember>(root)
                    ? manager.GetBuffer<GraphHierarchyBakingMember>(root) : manager.AddBuffer<GraphHierarchyBakingMember>(root);
                generated.Clear();
                foreach (var member in members)
                    if (owners.TryGetValue(member, out var owner) && owner == root)
                        generated.Add(new GraphHierarchyBakingMember { Value = member });
            }
        }

        private static Entity FindOwner(EntityManager manager, Entity entity,
            Dictionary<Entity, Entity> owners, HashSet<Entity> path)
        {
            if (owners.TryGetValue(entity, out var owner)) return owner;
            if (!path.Add(entity)) throw new InvalidOperationException("Cycle in graph transform hierarchy: " + entity);

            owner = manager.HasComponent<Parent>(entity)
                ? FindOwner(manager, manager.GetComponentData<Parent>(entity).Value, owners, path)
                : Entity.Null;
            owners[entity] = owner;
            return owner;
        }
    }
}
