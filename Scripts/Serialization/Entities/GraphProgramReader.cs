using HSE.Core.Components;
using HSE.Mechanics.Components;
using HSE.Serialization.Wire.Generated;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace HSE.Serialization.Wire.Entities
{
    /// <summary>Читает форматы v1/v2 и проверяет команды до исполнения. Неизвестные форматы отклоняются без fallback.</summary>
    public static unsafe class GraphProgramReader
    {
        public const uint Magic = 0x50474348;
        public const uint FormatVersion = 2;
        public const int HeaderSize = 16;
        public const int EntrySize = 20;
        public const int InstructionSize = 32;

        /// <summary>Проверяет оболочку, все команды, payload и ограничения сетевого состава; возвращает индекс ошибки.</summary>
        public static CodecResult Validate(ref GraphProgramStorage program, ref BlobArray<byte> data, out int errorIndex)
        {
            errorIndex = -1;
            if (!ComponentCodecs.IsReady) return CodecResult.RegenerationRequired;
            ref var bytes = ref program.Bytes;
            if (bytes.Length < HeaderSize || Read(ref bytes, 0) != Magic) return CodecResult.InvalidProgram;
            uint format = Read(ref bytes, 4);
            if (format != 1 && format != FormatVersion) return CodecResult.UnsupportedProgramVersion;

            int entries = EntryCount(ref program), instructions = InstructionCount(ref program);
            if (entries < 0 || instructions < 0 ||
                HeaderSize + (long)entries * EntrySize + (long)instructions * InstructionSize != bytes.Length)
                return CodecResult.InvalidProgram;

            int expectedStart = 0;
            ActionOverrideEvent previous = default;
            for (int i = 0; i < entries; i++)
            {
                var action = Entry(ref program, i, out int first, out int count);
                if (Read(ref bytes, HeaderSize + i * EntrySize) > byte.MaxValue ||
                    action.ActionType == ActionType.None || (i != 0 && Compare(previous, action) >= 0) ||
                    first != expectedStart || count <= 0 || (long)first + count > instructions)
                    return CodecResult.InvalidProgram;

                for (int j = first; j < first + count; j++)
                {
                    errorIndex = j;
                    int offset = HeaderSize + entries * EntrySize + j * InstructionSize;
                    uint world = Read(ref bytes, offset + 28);
                    if (Read(ref bytes, offset + 24) > 1 ||
                        (format == 1 ? world != 0 : world < 1 || world > 3))
                        return CodecResult.InvalidProgram;
                    var command = Instruction(ref program, j);
                    if (format == 1) command.World = DefaultWorld(command.Payload.TypeId, action.ActionType);
                    var result = ValidateInstruction(command, action, format, ref data);
                    if (result != CodecResult.Success) return result;
                }
                previous = action;
                expectedStart += count;
            }
            errorIndex = -1;
            return expectedStart == instructions ? CodecResult.Success : CodecResult.InvalidProgram;
        }

        /// <summary>UI-состояния добавляются и изменяются только в клиентском мире; остальные типы сохраняют свои правила.</summary>
        public static bool IsUiWorldAllowed(uint typeId, GraphOpcode opcode, HSEComponentWorld world)
        {
            if (opcode != GraphOpcode.AddComponent && opcode != GraphOpcode.SetComponent) return true;
            bool ui = typeId != 0 && (typeId == ComponentCodecs.GetTypeId<UiPanelState>() ||
                typeId == ComponentCodecs.GetTypeId<PointerHintState>());
            return !ui || world == HSEComponentWorld.Client;
        }

        private static CodecResult ValidateInstruction(in GraphInstruction command, in ActionOverrideEvent action, uint format, ref BlobArray<byte> data)
        {
            if (command.Opcode < GraphOpcode.AddComponent || command.Opcode > GraphOpcode.RequireComponent)
                return CodecResult.UnknownCommand;
            if (command.Version != 1) return CodecResult.UnsupportedCommandVersion;

            var type = ComponentCodecs.GetComponentType(command.Payload.TypeId);
            if (type.TypeIndex == TypeIndex.Null) return CodecResult.UnknownType;
            bool network = ComponentCodecs.IsNetworkComponent(command.Payload.TypeId);
            if (!IsUiWorldAllowed(command.Payload.TypeId, command.Opcode, command.World)) return CodecResult.InvalidCommand;
            if (format >= 2 && ResolveEvent(action.ActionType) != ActionType.None)
            {
                if (action.ActionType == ActionType.Initialization)
                {
                    if (command.World != HSEComponentWorld.Both) return CodecResult.InvalidCommand;
                }
                else if (network && command.World != HSEComponentWorld.Server) return CodecResult.InvalidCommand;
            }
            if (network &&
                (command.Opcode == GraphOpcode.RemoveComponent ||
                 (action.ActionType != ActionType.Initialization &&
                  (command.Opcode == GraphOpcode.AddComponent || command.Opcode == GraphOpcode.AddBuffer))))
                return CodecResult.InvalidCommand;

            bool payload = command.Opcode == GraphOpcode.SetComponent || command.Opcode == GraphOpcode.ReplaceBuffer;
            if (command.Opcode != GraphOpcode.SetEnabled && command.Enabled) return CodecResult.InvalidCommand;
            if (!payload && (command.Payload.Version != 0 || command.Payload.DataOffset != 0 ||
                command.Payload.DataSize != 0 || command.Payload.ElementCount != 0)) return CodecResult.InvalidCommand;
            if (command.Opcode == GraphOpcode.SetEnabled && !type.IsEnableable) return CodecResult.IncompatibleComponent;
            if ((command.Opcode == GraphOpcode.AddComponent || command.Opcode == GraphOpcode.SetComponent) && !type.IsComponent)
                return CodecResult.IncompatibleComponent;
            if ((command.Opcode == GraphOpcode.AddBuffer || command.Opcode == GraphOpcode.ReplaceBuffer ||
                 command.Opcode == GraphOpcode.ClearBuffer) && !type.IsBuffer) return CodecResult.IncompatibleComponent;

            return payload ? GraphWireDecoder.Validate(command.Payload, ref data, out _, out _) : CodecResult.Success;
        }

        /// <summary>Число точек входа; вызывать после успешной проверки оболочки.</summary>
        public static int EntryCount(ref GraphProgramStorage program) => (int)Read(ref program.Bytes, 8);

        /// <summary>Число инструкций; вызывать после успешной проверки оболочки.</summary>
        public static int InstructionCount(ref GraphProgramStorage program) => (int)Read(ref program.Bytes, 12);

        /// <summary>Читает диапазон инструкций одной точки входа из проверенной программы.</summary>
        public static ActionOverrideEvent Entry(ref GraphProgramStorage program, int index, out int first, out int count)
        {
            int offset = HeaderSize + index * EntrySize;
            first = (int)Read(ref program.Bytes, offset + 12);
            count = (int)Read(ref program.Bytes, offset + 16);
            return new ActionOverrideEvent((ActionType)Read(ref program.Bytes, offset), Read(ref program.Bytes, offset + 4),
                (int)Read(ref program.Bytes, offset + 8));
        }

        /// <summary>Ищет точку входа бинарным поиском в проверенной отсортированной таблице.</summary>
        public static bool Find(ref GraphProgramStorage program, in ActionOverrideEvent action, out int first, out int count)
        {
            first = count = 0;
            if (ResolveEvent(action.ActionType) == ActionType.None) return false;
            int lo = 0, hi = EntryCount(ref program) - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                var current = Entry(ref program, mid, out first, out count);
                int comparison = Compare(current, action);
                if (comparison == 0) return true;
                if (comparison < 0) lo = mid + 1;
                else hi = mid - 1;
            }
            first = count = 0;
            return false;
        }

        /// <summary>Читает инструкцию без reinterpret native-структуры и без аллокаций.</summary>
        public static GraphInstruction Instruction(ref GraphProgramStorage program, int index)
        {
            int offset = HeaderSize + EntryCount(ref program) * EntrySize + index * InstructionSize;
            uint operation = Read(ref program.Bytes, offset);
            return new GraphInstruction
            {
                Opcode = (GraphOpcode)(operation & 0xffff), Version = (ushort)(operation >> 16),
                Payload = new OverrideKey
                {
                    TypeId = Read(ref program.Bytes, offset + 4), Version = Read(ref program.Bytes, offset + 8),
                    DataOffset = (int)Read(ref program.Bytes, offset + 12), DataSize = (int)Read(ref program.Bytes, offset + 16),
                    ElementCount = (int)Read(ref program.Bytes, offset + 20)
                },
                Enabled = Read(ref program.Bytes, offset + 24) != 0,
                World = (HSEComponentWorld)Read(ref program.Bytes, offset + 28)
            };
        }

        /// <summary>Стабильный порядок событий, не зависящий от порядка обхода hash map.</summary>
        public static int Compare(in ActionOverrideEvent a, in ActionOverrideEvent b)
        {
            int order = ((byte)a.ActionType).CompareTo((byte)b.ActionType);
            if (order == 0) order = a.ActionIndex.CompareTo(b.ActionIndex);
            return order != 0 ? order : a.InstanceID.CompareTo(b.InstanceID);
        }

        /// <summary>Начальные события, выполняемые до обычной очереди экземпляра.</summary>
        public static bool IsInitial(in ActionOverrideEvent action) =>
            action.ActionType == ActionType.Initialization || action.ActionType == ActionType.OnStart;

        /// <summary>Разрешает поддерживаемое событие; неизвестный ID остаётся в blob, но не исполняется.</summary>
        public static ActionType ResolveEvent(ActionType type) => type switch
        {
            ActionType.Initialization => type,
            ActionType.OnStart        => type,
            ActionType.Raycast        => type,
            ActionType.OnTriggerEnter => type,
            ActionType.OnTriggerStay  => type,
            ActionType.OnTriggerExit  => type,
            ActionType.OnCursorEnter  => type,
            ActionType.OnCursorStay   => type,
            ActionType.OnCursorExit   => type,
            _                         => ActionType.None
        };

        /// <summary>Выбирает мир до сохранения программы или при чтении старой записи без поля World.</summary>
        public static HSEComponentWorld DefaultWorld(uint typeId, ActionType action) =>
            ComponentCodecs.IsNetworkComponent(typeId)
                ? (action == ActionType.Initialization ? HSEComponentWorld.Both : HSEComponentWorld.Server)
                : ComponentCodecs.GetWorld(typeId);

        /// <summary>Читает little-endian слово; границы гарантирует Validate и проверенная таблица.</summary>
        private static uint Read(ref BlobArray<byte> bytes, int offset) => (uint)WirePrimitives.Read((byte*)bytes.GetUnsafePtr() + offset, 4);
    }
}
