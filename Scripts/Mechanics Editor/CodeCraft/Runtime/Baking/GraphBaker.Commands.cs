using System;
using System.Collections.Generic;
using HSE.Core.Components;
using HSE.Mechanics.Components;
using HSE.Serialization.Wire.Generated;
using Unity.Entities;
using UnityEngine;

namespace HSE.CodeCraft
{
    /// <summary>Явное API команд общего графа: target адресует сущность, world задаёт место исполнения.
    /// Пропущенный world берётся из метаданных типа; для сетевых записей после конвертации выбирается Server.</summary>
    public partial class GraphBaker
    {
        private readonly List<GraphCommandBakingRecord> _commands = new List<GraphCommandBakingRecord>();

        /// <summary>Добавляет типовую команду без payload; полная проверка выполняется компилятором программы.</summary>
        public void AddCommand(Entity target, in ActionOverrideEvent action, in GraphInstruction instruction) =>
            Append(target, action, instruction);

        /// <summary>Добавляет отсутствующий компонент с default-данными; имеющиеся данные не перезаписывает.</summary>
        public void AddComponent<T>(Entity target, in ActionOverrideEvent action, HSEComponentWorld world = default) where T : unmanaged, IComponentData =>
            Append(target, action, GraphInstruction.ForType(GraphOpcode.AddComponent, RequireTypeId<T>(), world: world));

        /// <summary>Добавляет компонент при отсутствии и записывает значение, включая уже существующий компонент.
        /// Компилируется в Add и Set; enabled-состояние существующего компонента сохраняется.</summary>
        public unsafe void AddComponent<T>(Entity target, in ActionOverrideEvent action, T value, HSEComponentWorld world = default)
            where T : unmanaged, IComponentData
        {
            uint typeId = RequireTypeId<T>();
            if (ComponentCodecs.IsStateOnly(typeId))
            {
                if (!EqualityComparer<T>.Default.Equals(value, default))
                    throw new InvalidOperationException("State-only graph components cannot carry data: " + typeof(T));
                AddComponent<T>(target, action, world);
                return;
            }

            AppendPayload(target, action, GraphOpcode.SetComponent, &value, 1, world, true);
        }

        /// <summary>Удаляет компонент или буфер. Отсутствие допустимо; сетевой тип удалять нельзя.</summary>
        public void RemoveComponent<T>(Entity target, in ActionOverrideEvent action, HSEComponentWorld world = default) where T : unmanaged =>
            Append(target, action, GraphInstruction.ForType(GraphOpcode.RemoveComponent, RequireTypeId<T>(), world: world));

        /// <summary>Записывает wire-payload для полной замены существующего компонента в runtime.</summary>
        public unsafe void SetComponent<T>(Entity target, in ActionOverrideEvent action, T value, HSEComponentWorld world = default) where T : unmanaged, IComponentData =>
            AppendPayload(target, action, GraphOpcode.SetComponent, &value, 1, world);

        /// <summary>Переключает enabled bit существующего enableable-типа, не создавая его неявно.</summary>
        public void SetEnabled<T>(Entity target, in ActionOverrideEvent action, bool enabled, HSEComponentWorld world = default) where T : unmanaged =>
            Append(target, action, GraphInstruction.ForType(GraphOpcode.SetEnabled, RequireTypeId<T>(), enabled, world));

        /// <summary>Добавляет пустой буфер, только если он отсутствует; сохраняет существующие элементы.</summary>
        public void AddBuffer<T>(Entity target, in ActionOverrideEvent action, HSEComponentWorld world = default) where T : unmanaged, IBufferElementData =>
            Append(target, action, GraphInstruction.ForType(GraphOpcode.AddBuffer, RequireTypeId<T>(), world: world));

        /// <summary>Добавляет наполненный буфер, только если он отсутствует; сохраняет существующие элементы.</summary>
        public void AddBuffer<T>(Entity target, in ActionOverrideEvent action, T[] values, HSEComponentWorld world = default) where T : unmanaged, IBufferElementData{ 
            if(values.Length == 0)
            {
                Debug.LogWarning($"DynamicBuffer values by type {typeof(T)} is empty");
                AddBuffer<T>(target, action, world);
                return;
            }
            AddBuffer<T>(target, action, world);
            ReplaceBuffer(target, action, values, world);
        }

        /// <summary>Очищает существующий буфер без удаления компонента.</summary>
        public void ClearBuffer<T>(Entity target, in ActionOverrideEvent action, HSEComponentWorld world = default) where T : unmanaged, IBufferElementData =>
            Append(target, action, GraphInstruction.ForType(GraphOpcode.ClearBuffer, RequireTypeId<T>(), world: world));

        /// <summary>Записывает wire-payload для полной замены существующего буфера в runtime.</summary>
        public unsafe void ReplaceBuffer<T>(Entity target, in ActionOverrideEvent action, T[] values, HSEComponentWorld world = default) where T : unmanaged, IBufferElementData
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            fixed (T* source = values) AppendPayload(target, action, GraphOpcode.ReplaceBuffer, source, values.Length, world);
        }

        /// <summary>Сериализует данные текущим wire-кодеком; версия команды и версия компонента независимы.</summary>
        private unsafe void AppendPayload<T>(Entity target, in ActionOverrideEvent action, GraphOpcode opcode, T* source, int count,
            HSEComponentWorld world, bool addComponent = false) where T : unmanaged
        {
            var key = CreateKey<T>(count);
            var bytes = new byte[key.DataSize];
            fixed (byte* destination = bytes)
                RequireSuccess(ComponentCodecs.Serialize(source, count, destination, bytes.Length), typeof(T));

            if (addComponent)
                Append(target, action, GraphInstruction.ForType(GraphOpcode.AddComponent, key.TypeId, world: world));
            Append(target, action, GraphInstruction.WithPayload(opcode, key, world));
            _allDatas.AddRange(bytes);
        }

        /// <summary>Регистрирует команду на уже объявленной целевой сущности; индекс задаёт исходный порядок.</summary>
        private void Append(Entity target, in ActionOverrideEvent action, in GraphInstruction instruction)
        {
            if (target == Entity.Null || action.ActionType == ActionType.None)
                throw new ArgumentException("A graph command requires a target entity and event.");
            _commands.Add(new GraphCommandBakingRecord { Target = target, Event = action, Instruction = instruction, Order = _commands.Count });
        }
    }
}
