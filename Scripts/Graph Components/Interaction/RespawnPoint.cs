using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Core.Components
{
    /// <summary>
    /// Точка возрождения комнаты (синглтон). Используется и при первом спавне персонажа на сервере, и при возрождении
    /// по клавише R. Если в мире её нет - персонаж возрождается в начале координат, сохраняя свой поворот.
    /// Чтобы сменить точку (узел графа, чекпоинт), достаточно создать/обновить этот синглтон - в ОБОИХ мирах,
    /// иначе предсказание клиента уведёт игрока в старую точку до коррекции сервером.
    /// </summary>
    public struct RespawnPoint : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
    }
}
