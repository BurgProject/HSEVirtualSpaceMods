using Unity.Entities;
using Unity.Entities.Content;

namespace HSE.Core.Components
{
    /// <summary>Ссылка доставки Mono-сцены. Запекается напрямую для обнаружения зависимости сборщиком Entities, не является командой графа.</summary>
    public struct LocationSceneReference : IComponentData
    {
        public WeakObjectSceneReference Scene;
    }
}
