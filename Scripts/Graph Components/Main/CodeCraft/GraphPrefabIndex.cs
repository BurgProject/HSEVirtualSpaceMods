using Unity.Entities;

namespace HSE.Mechanics.Components
{
    /// <summary>Сохранённый индекс объекта единственной подсцены, общий для клиента и сервера.</summary>
    public struct GraphPrefabIndex : IComponentData
    {
        public int Value;
    }
}
