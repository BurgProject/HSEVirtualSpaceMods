using Unity.Entities;

namespace HSE.Mechanics.Components
{
    /// <summary>Версионированная программа графа. При наличии имеет приоритет над прежними Overrides.</summary>
    public struct GraphProgram : IComponentData
    {
        public BlobAssetReference<GraphProgramStorage> Storage;
    }

    /// <summary>Стабильная оболочка: формат и инструкции кодируются внутри байтов, а не native layout команд.</summary>
    public struct GraphProgramStorage
    {
        public BlobArray<byte> Bytes;
    }
}
