namespace HSE.Mechanics.Components
{
    /// <summary>Постоянные коды операций. Опубликованные номера и семантику версии нельзя переиспользовать.</summary>
    public enum GraphOpcode : ushort
    {
        AddComponent  = 1,
        SetComponent  = 2,
        RemoveComponent = 3,
        SetEnabled    = 4,
        AddBuffer     = 5,
        ReplaceBuffer = 6,
        ClearBuffer   = 7,
        RequireComponent = 8
    }

    /// <summary>Представление декодированной команды в памяти; в blob записывается явно в little-endian.</summary>
    public struct GraphInstruction
    {
        public GraphOpcode Opcode;
        public ushort Version;
        public OverrideKey Payload;
        public bool Enabled;
        /// <summary>Мир исполнения; ноль допустим только до компиляции и означает значение по умолчанию.</summary>
        public HSEComponentWorld World;

        /// <summary>Создаёт команду v1 без payload. Add не перезаписывает существующее значение.</summary>
        public static GraphInstruction ForType(GraphOpcode opcode, uint typeId, bool enabled = false, HSEComponentWorld world = default) => new GraphInstruction
        {
            Opcode = opcode, Version = 1,
            Payload = new OverrideKey { TypeId = typeId }, Enabled = enabled, World = world
        };

        /// <summary>Создаёт команду v1 записи значения или буфера с независимой wire-версией данных.</summary>
        public static GraphInstruction WithPayload(GraphOpcode opcode, in OverrideKey payload, HSEComponentWorld world = default) => new GraphInstruction
        {
            Opcode = opcode, Version = 1, Payload = payload, World = world
        };
    }
}
