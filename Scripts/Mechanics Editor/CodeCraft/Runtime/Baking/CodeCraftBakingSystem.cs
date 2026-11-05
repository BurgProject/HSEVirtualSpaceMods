using System;
using System.Collections.Generic;
using HSE.Collections;
using HSE.Core.Components;
using HSE.Mechanics.Components;
using HSE.Serialization.Wire;
using HSE.Serialization.Wire.Entities;
using HSE.Serialization.Wire.Generated;
using Unity.Collections;
using Unity.Entities;

namespace HSE.CodeCraft
{
    /// <summary>Выпекает только служебные данные графа и wire-payload, не добавляя игровые компоненты.</summary>
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct CodeCraftBakingSystem : ISystem
    {
        /// <summary>Ожидает данные графа и список сущностей для выпечки.</summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CodeCraftAsset>();
            state.RequireForUpdate<CodeCraftDataAsset>();
            state.RequireForUpdate<EntityOverride>();
        }

        /// <summary>Проверяет готовность кодеков и сохраняет граф каждой сущности.</summary>
        public void OnUpdate(ref SystemState state)
        {
            var asset = SystemAPI.GetSingleton<CodeCraftAsset>();
            var data  = SystemAPI.GetSingleton<CodeCraftDataAsset>();
            if (!asset.Storage.IsCreated || !data.Storage.IsCreated) return;
            if (!ComponentCodecs.IsReady)
                throw new InvalidOperationException("Finish wire regeneration before baking graphs.");

            using var entities = SystemAPI.GetSingletonBuffer<EntityOverride>().ToNativeArray(Allocator.Temp);
            var blobStore = state.World.GetExistingSystemManaged<BakingSystem>().BlobAssetStore;
            bool hasCommands = SystemAPI.TryGetSingletonBuffer<GraphCommandBakingRecord>(out var commandBuffer, true);
            using var commands = new NativeParallelMultiHashMap<Entity, GraphCommandBakingRecord>(hasCommands ? commandBuffer.Length + 1 : 1, Allocator.Temp);
            if (hasCommands)
                foreach (var command in commandBuffer) commands.Add(command.Target, command);

            for (int i = 0; i < entities.Length; i++)
                BakeEntity(ref state, entities[i].Value, i, ref asset.Storage.Value,
                    ref data.Storage.Value.AllDatas, ref blobStore, commands);

            var roots = new List<Entity>(entities.Length);
            foreach (var entity in entities) roots.Add(entity.Value);
            GraphHierarchyBaking.Build(state.EntityManager, roots);
        }

        /// <summary>Сохраняет начальные и событийные записи без зависимости контента от native layout игровых типов.</summary>
        private static void BakeEntity(ref SystemState state, Entity entity, int index,
            ref CodeCraftEntityStorage graph, ref BlobArray<byte> data, ref BlobAssetStore blobStore,
            NativeParallelMultiHashMap<Entity, GraphCommandBakingRecord> commands)
        {
            var manager = state.EntityManager;
            using var states = new NativeList<OverrideStateElement>(Allocator.Temp);
            using var keys   = new NativeList<OverrideKeyElement>(Allocator.Temp);
            using var ids    = new NativeHashSet<int>(16, Allocator.Temp);

            if (!manager.HasBuffer<ActionOverrideEvent>(entity))
                manager.AddBuffer<ActionOverrideEvent>(entity);
            manager.AddComponentData(entity, new GraphPrefabIndex { Value = index });

            if (graph.OverrideStatesComponents.TryGetFirstValue(entity, out var stateEntry, out var stateIterator))
            {
                do
                {
                    var type = ComponentCodecs.GetComponentType(stateEntry.OverrideState.TypeId);
                    if (type.TypeIndex == TypeIndex.Null)
                        throw new InvalidOperationException("Unknown graph state type: " + stateEntry.OverrideState.TypeId);

                    RequireRuntimeOnly(manager, entity, type);
                    if (ComponentCodecs.IsNetworkComponent(stateEntry.OverrideState.TypeId) &&
                        !stateEntry.OverrideState.ActivState && !type.IsEnableable)
                        throw new InvalidOperationException("A non-enableable Ghost component cannot be deactivated by a graph.");

                    states.Add(stateEntry);
                    if (!IsInitial(stateEntry.Event)) ids.Add(stateEntry.Event.InstanceID);
                }
                while (graph.OverrideStatesComponents.TryGetNextValue(out stateEntry, ref stateIterator));
            }

            if (graph.OverridesComponents.TryGetFirstValue(entity, out var entry, out var iterator))
            {
                do
                {
                    RequireSuccess(GraphWireDecoder.Validate(entry.OverrideKey, ref data, out var type, out _), entry.OverrideKey);
                    RequireRuntimeOnly(manager, entity, type);

                    keys.Add(entry);
                    if (!IsInitial(entry.Event)) ids.Add(entry.Event.InstanceID);
                }
                while (graph.OverridesComponents.TryGetNextValue(out entry, ref iterator));
            }

            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var storage = ref builder.ConstructRoot<ComponentOverridesStorage>();
                var stateMap = builder.AllocateMultiHashMap(ref storage.OverrideStates, Math.Max(1, states.Length));
                foreach (var item in states)
                    stateMap.Add(item.Event, item.OverrideState);

                var keyMap = builder.AllocateMultiHashMap(ref storage.Overrides, Math.Max(1, keys.Length));
                foreach (var item in keys)
                    keyMap.Add(item.Event, item.OverrideKey);

                var result = builder.CreateBlobAssetReference<ComponentOverridesStorage>(Allocator.Persistent);
                blobStore.TryAdd(ref result);
                manager.AddComponentData(entity, new Overrides { ComponentsStorage = result });
            }
            finally
            {
                builder.Dispose();
            }

            var compiler = GraphProgramBuilder.FromLegacy(states.AsArray(), keys.AsArray());
            var implicitEvents = new HashSet<(ActionOverrideEvent Event, uint TypeId)>();
            foreach (var item in states)
                implicitEvents.Add((GraphProgramBuilder.InstanceAction(item.Event, ComponentCodecs.IsNetworkComponent(item.OverrideState.TypeId)), item.OverrideState.TypeId));
            foreach (var item in keys)
                implicitEvents.Add((GraphProgramBuilder.InstanceAction(item.Event, ComponentCodecs.IsNetworkComponent(item.OverrideKey.TypeId)), item.OverrideKey.TypeId));

            using var explicitCommands = new NativeList<GraphCommandBakingRecord>(Allocator.Temp);
            foreach (var command in commands.GetValuesForKey(entity)) explicitCommands.Add(command);
            explicitCommands.Sort();

            foreach (var command in explicitCommands)
            {
                if (implicitEvents.Contains((command.Event, command.Instruction.Payload.TypeId)))
                    throw new InvalidOperationException($"Do not mix legacy and explicit commands for target {entity}, event {command.Event}, type {command.Instruction.Payload.TypeId}.");
                var type = ComponentCodecs.GetComponentType(command.Instruction.Payload.TypeId);
                if (type.TypeIndex == TypeIndex.Null) throw new InvalidOperationException("Unknown command component type.");
                RequireRuntimeOnly(manager, entity, type);
                compiler.Add(command.Event, command.Instruction);
                if (!IsInitial(command.Event)) ids.Add(command.Event.InstanceID);
            }
            var program = compiler.Build(ref data);
            blobStore.TryAdd(ref program);
            manager.AddComponentData(entity, new GraphProgram { Storage = program });

            var objectIds = manager.HasBuffer<EventedObjectElement>(entity)
                ? manager.GetBuffer<EventedObjectElement>(entity) : manager.AddBuffer<EventedObjectElement>(entity);
            objectIds.Clear();
            foreach (int id in ids)
                objectIds.Add(new EventedObjectElement(id));
        }

        /// <summary>Начальные события выполняются при подготовке runtime, а не через пользовательский ввод.</summary>
        private static bool IsInitial(in ActionOverrideEvent action) =>
            action.ActionType == ActionType.OnStart || action.ActionType == ActionType.Initialization;

        /// <summary>Не допускает скрытой зависимости локации от компонента графа, добавленного другим Baker.</summary>
        private static void RequireRuntimeOnly(EntityManager manager, Entity entity, ComponentType type)
        {
            if (manager.HasComponent(entity, type))
                throw new InvalidOperationException($"Graph component {type} was added by another Baker. Move its creation to runtime.");
        }

        /// <summary>Прерывает выпечку при несовместимой wire-записи.</summary>
        private static void RequireSuccess(CodecResult result, in OverrideKey key)
        {
            if (result != CodecResult.Success)
                throw new InvalidOperationException($"Graph wire decode failed: type {key.TypeId}, version {key.Version}, {result}.");
        }
    }
}
