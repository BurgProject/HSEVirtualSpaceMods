
using HSE.Collections;
using HSE.Core.Components;
using HSE.Mechanics.Components;
using HSE.Serialization.Wire;
using HSE.Serialization.Wire.Generated;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HSE.CodeCraft
{
    /// <summary>Собирает данные компонентов и команды графа для последующей выпечки программы.</summary>
    public partial class GraphBaker
    {
        public readonly IBaker Baker;
        public List<AnimationClip> AnimationClips { get; private set; }

        private readonly List<byte> _allDatas;

        public readonly Dictionary<int, Entity> Entities;

        public GraphBaker(CodeCraftScenario ghostCraftObj, IBaker baker)
        {
            Baker             = baker;
            Entities          = new Dictionary<int, Entity>();
            _allDatas         = new List<byte>();
        }

        public void AddOverrideEntity(GameObject source, int instanceID, TransformUsageFlags flags = TransformUsageFlags.Dynamic)
        {
            var entity = Baker.GetEntity(source, flags);
            Entities.TryAdd(instanceID, entity);
        }
        public void AddOverrideEntity(GameObject source, TransformUsageFlags flags = TransformUsageFlags.Dynamic)
        {
            var entity = Baker.GetEntity(source, flags);
            Entities.TryAdd(source.GetInstanceID(), entity);
        }
        public void AddOverrideEntity(Entity entity, int instanceID)=>
            Entities.TryAdd(instanceID, entity);

        public Entity GetEntity(int instanceID)
        {
            if (!Entities.TryGetValue(instanceID, out Entity entity))
                entity = Entity.Null;
            return entity;
        }
        public Entity CreateAdditionalEntity(TransformUsageFlags usageFlags) => Baker.CreateAdditionalEntity(usageFlags);


        private OverrideKey CreateKey<T>(int count) where T : unmanaged
        {
            uint typeId  = RequireTypeId<T>();
            uint version = ComponentCodecs.CurrentVersion(typeId);
            RequireSuccess(ComponentCodecs.GetLayout(typeId, version, out int size, out _, out _), typeof(T));

            return new OverrideKey
            {
                TypeId       = typeId,
                Version      = version,
                DataOffset   = _allDatas.Count,
                DataSize     = checked(size * count),
                ElementCount = count
            };
        }

        private static uint RequireTypeId<T>() where T : unmanaged
        {
            if (!ComponentCodecs.IsReady)
                throw new InvalidOperationException("Finish wire codec regeneration before baking graphs.");

            uint id = ComponentCodecs.GetTypeId<T>();
            if (id == 0)
                throw new InvalidOperationException("Register a permanent wire ID and regenerate codecs: " + typeof(T));
            return id;
        }

        private static void RequireSuccess(CodecResult result, Type type)
        {
            if (result != CodecResult.Success)
                throw new InvalidOperationException("Graph wire encoding failed for " + type + ": " + result);
        }

        public void AddAnimationClipsToBake(AnimationClip[] clips)
        {
            if (AnimationClips == null) AnimationClips = new List<AnimationClip>();
            AnimationClips.AddRange(clips);
        }

        /// <summary>Передаёт данные и временные записи команд системе выпечки, не добавляя игровые компоненты.</summary>
        public void Complete()
        {
            if (Entities.Count == 0 && _commands.Count != 0)
                throw new InvalidOperationException("Register the command target through AddOverrideEntity first.");

            if (Entities.Count == 0) return;

            if (_commands.Count == 0) return;

            var targets = new HashSet<Entity>(Entities.Values);
            foreach (var command in _commands)
                if (!targets.Contains(command.Target))
                    throw new InvalidOperationException("Register the command target through AddOverrideEntity first.");

            var builder     = new BlobBuilder(Allocator.Temp);
            var dataBuilder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var storage = ref builder.ConstructRoot<CodeCraftEntityStorage>();
                ref var data    = ref dataBuilder.ConstructRoot<CodeCraftDataStorage>();

                // Compatibility containers for the existing baking reader; new graphs only emit commands.
                builder.AllocateMultiHashMap(ref storage.OverrideStatesComponents, 1);
                builder.AllocateMultiHashMap(ref storage.OverridesComponents, 1);

                var bytes = dataBuilder.Allocate(ref data.AllDatas, _allDatas.Count);
                for (int i = 0; i < _allDatas.Count; i++)
                    bytes[i] = _allDatas[i];

                var result     = builder.CreateBlobAssetReference<CodeCraftEntityStorage>(Allocator.Persistent);
                var resultData = dataBuilder.CreateBlobAssetReference<CodeCraftDataStorage>(Allocator.Persistent);
                Baker.AddBlobAsset(ref result, out _);
                Baker.AddBlobAsset(ref resultData, out _);

                var entity   = Baker.CreateAdditionalEntity(TransformUsageFlags.None);
                var entities = Baker.AddBuffer<EntityOverride>(entity);
                foreach (var target in Entities.Values)
                    entities.Add(new EntityOverride { Value = target });

                Baker.AddComponent(entity, new CodeCraftAsset { Storage = result });
                Baker.AddComponent(entity, new CodeCraftDataAsset { Storage = resultData });
                var commands = Baker.AddBuffer<GraphCommandBakingRecord>(entity);
                foreach (var command in _commands) commands.Add(command);
                Baker.AddBuffer<ActionOverrideEventForInput>(entity);
                Baker.AddBuffer<ActionOverrideEventCopy>(entity);
            }
            finally
            {
                builder.Dispose();
                dataBuilder.Dispose();
            }
        }
    }
}
