using System;
using System.Collections.Generic;
using HSE.Core.Components;
using HSE.Mechanics.Components;
using HSE.Serialization.Wire;
using HSE.Serialization.Wire.Entities;
using HSE.Serialization.Wire.Generated;
using Unity.Collections;
using Unity.Entities;

namespace HSE.CodeCraft
{
    /// <summary>Компилятор программы с миром исполнения. Сохраняет порядок команд и сортирует только точки входа.</summary>
    public sealed class GraphProgramBuilder
    {
        private readonly Dictionary<ActionOverrideEvent, List<GraphInstruction>> _entries = new Dictionary<ActionOverrideEvent, List<GraphInstruction>>();

        /// <summary>Добавляет явную команду в конец события. Payload должен ссылаться на общее byte-хранилище графа.</summary>
        public void Add(in ActionOverrideEvent action, in GraphInstruction command)
        {
            if (GraphProgramReader.ResolveEvent(action.ActionType) == ActionType.None)
                throw new ArgumentException("A command requires a supported event.");
            var resolved = command;
            if (resolved.World == 0)
                resolved.World = GraphProgramReader.DefaultWorld(command.Payload.TypeId, action.ActionType);
            if (!_entries.TryGetValue(action, out var commands))
                _entries.Add(action, commands = new List<GraphInstruction>());
            commands.Add(resolved);
        }

        /// <summary>Кодирует программу, проверяет каждую команду и передаёт владение blob вызывающему коду.</summary>
        public BlobAssetReference<GraphProgramStorage> Build(ref BlobArray<byte> data)
        {
            var events = new List<ActionOverrideEvent>(_entries.Keys);
            events.Sort((a, b) => GraphProgramReader.Compare(a, b));
            int count = 0;
            foreach (var action in events) count = checked(count + _entries[action].Count);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GraphProgramStorage>();
            var bytes = builder.Allocate(ref root.Bytes, checked(GraphProgramReader.HeaderSize + events.Count * GraphProgramReader.EntrySize +
                count * GraphProgramReader.InstructionSize));
            Write(bytes, 0, GraphProgramReader.Magic);
            Write(bytes, 4, GraphProgramReader.FormatVersion);
            Write(bytes, 8, (uint)events.Count);
            Write(bytes, 12, (uint)count);

            int cursor = 0;
            for (int i = 0; i < events.Count; i++)
            {
                var action = events[i];
                var commands = _entries[action];
                int offset = GraphProgramReader.HeaderSize + i * GraphProgramReader.EntrySize;
                Write(bytes, offset, (uint)action.ActionType);
                Write(bytes, offset + 4, action.ActionIndex);
                Write(bytes, offset + 8, unchecked((uint)action.InstanceID));
                Write(bytes, offset + 12, (uint)cursor);
                Write(bytes, offset + 16, (uint)commands.Count);
                foreach (var command in commands)
                {
                    offset = GraphProgramReader.HeaderSize + events.Count * GraphProgramReader.EntrySize + cursor++ * GraphProgramReader.InstructionSize;
                    Write(bytes, offset, (uint)command.Opcode | ((uint)command.Version << 16));
                    Write(bytes, offset + 4, command.Payload.TypeId);
                    Write(bytes, offset + 8, command.Payload.Version);
                    Write(bytes, offset + 12, unchecked((uint)command.Payload.DataOffset));
                    Write(bytes, offset + 16, unchecked((uint)command.Payload.DataSize));
                    Write(bytes, offset + 20, unchecked((uint)command.Payload.ElementCount));
                    Write(bytes, offset + 24, command.Enabled ? 1u : 0u);
                    Write(bytes, offset + 28, (uint)command.World);
                }
            }

            var blob = builder.CreateBlobAssetReference<GraphProgramStorage>(Allocator.Persistent);
            var result = GraphProgramReader.Validate(ref blob.Value, ref data, out int error);
            if (result == CodecResult.Success) return blob;
            blob.Dispose();
            throw new InvalidOperationException($"Graph program instruction {error}: {result}.");
        }

        /// <summary>Переходный адаптер старых узлов: состояние, затем payload; неоднозначные записи одного типа запрещены.</summary>
        public static GraphProgramBuilder FromLegacy(NativeArray<OverrideStateElement> states, NativeArray<OverrideKeyElement> records)
        {
            var builder = new GraphProgramBuilder();
            var stateMap = new Dictionary<(ActionOverrideEvent, uint), OverrideKeyState>();
            foreach (var entry in states)
            {
                var key = (entry.Event, entry.OverrideState.TypeId);
                if (stateMap.TryGetValue(key, out var old) && !old.Equals(entry.OverrideState))
                    throw new InvalidOperationException("Conflicting graph states for " + entry.Event);
                stateMap[key] = entry.OverrideState;
            }
            var orderedStates = new List<(ActionOverrideEvent, uint)>(stateMap.Keys);
            orderedStates.Sort(Compare);
            foreach (var key in orderedStates)
            {
                var state = stateMap[key];
                bool network = ComponentCodecs.IsNetworkComponent(state.TypeId);
                var action = InstanceAction(key.Item1, network);
                var type = ComponentCodecs.GetComponentType(state.TypeId);
                if (!network)
                    builder.Add(action, GraphInstruction.ForType(state.ActivState ? GraphOpcode.AddComponent : GraphOpcode.RemoveComponent, state.TypeId));
                else
                    builder.Add(action, GraphInstruction.ForType(GraphOpcode.RequireComponent, state.TypeId));
                if (type.IsEnableable && (network || state.ActivState))
                    builder.Add(action, GraphInstruction.ForType(GraphOpcode.SetEnabled, state.TypeId,
                        state.ActivState && key.Item1.ActionType != ActionType.Initialization));
            }

            var payloadMap = new Dictionary<(ActionOverrideEvent, uint), OverrideKey>();
            foreach (var entry in records)
            {
                var key = (entry.Event, entry.OverrideKey.TypeId);
                if (payloadMap.TryGetValue(key, out var old) && !old.Equals(entry.OverrideKey))
                    throw new InvalidOperationException("Conflicting graph payloads for " + entry.Event + ". Use an explicit command sequence.");
                payloadMap[key] = entry.OverrideKey;
            }
            var orderedPayloads = new List<(ActionOverrideEvent, uint)>(payloadMap.Keys);
            orderedPayloads.Sort(Compare);
            foreach (var key in orderedPayloads)
            {
                var payload = payloadMap[key];
                var action = InstanceAction(key.Item1, ComponentCodecs.IsNetworkComponent(payload.TypeId));
                var type = ComponentCodecs.GetComponentType(payload.TypeId);
                if (!ComponentCodecs.IsNetworkComponent(payload.TypeId))
                    builder.Add(action, GraphInstruction.ForType(type.IsBuffer ? GraphOpcode.AddBuffer : GraphOpcode.AddComponent, payload.TypeId));
                builder.Add(action, GraphInstruction.WithPayload(type.IsBuffer ? GraphOpcode.ReplaceBuffer : GraphOpcode.SetComponent, payload));
                if (type.IsEnableable)
                    builder.Add(action, GraphInstruction.ForType(GraphOpcode.SetEnabled, payload.TypeId, key.Item1.ActionType != ActionType.Initialization));
            }
            return builder;
        }

        /// <summary>Старые локальные начальные записи переводит в фазу экземпляра при новой выпечке.</summary>
        internal static ActionOverrideEvent InstanceAction(ActionOverrideEvent action, bool network)
        {
            if (!network && action.ActionType == ActionType.Initialization) action.ActionType = ActionType.OnStart;
            return action;
        }

        /// <summary>Стабилизирует порядок исходных записей по событию и постоянному ID типа.</summary>
        private static int Compare((ActionOverrideEvent, uint) a, (ActionOverrideEvent, uint) b)
        {
            int order = GraphProgramReader.Compare(a.Item1, b.Item1);
            return order != 0 ? order : a.Item2.CompareTo(b.Item2);
        }

        /// <summary>Записывает 32-битное little-endian слово независимо от платформенного выравнивания.</summary>
        private static void Write(BlobBuilderArray<byte> bytes, int offset, uint value)
        {
            for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (i * 8));
        }
    }
}
