using System;
using HSE.Mechanics.Components;
using HSE.Core.Components;
using Unity.Entities;

namespace HSE.CodeCraft
{
    /// <summary>Временная команда authoring. Удаляется baking pipeline и не является форматом опубликованной программы.</summary>
    [BakingType]
    public struct GraphCommandBakingRecord : IBufferElementData, IComparable<GraphCommandBakingRecord>
    {
        public Entity Target;
        public ActionOverrideEvent Event;
        public GraphInstruction Instruction;
        public int Order;

        /// <summary>Восстанавливает порядок вызовов GraphBaker после группировки по сущности.</summary>
        public int CompareTo(GraphCommandBakingRecord other) => Order.CompareTo(other.Order);
    }
}
