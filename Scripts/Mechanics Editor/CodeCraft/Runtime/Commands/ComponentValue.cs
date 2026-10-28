using System;
using HSE.Mechanics.Components;
using HSE.Core.Components;
using HSE.Serialization.Wire.Generated;
using Unity.Entities;

namespace HSE.CodeCraft
{
    public interface ICommandDataNode
    {
        Type ComponentType { get; }
    }

    public interface ICommandComponentSetNode
    {
        string ComponentSetName { get; }
    }

    public interface ICommandComponentSource
    {
        ComponentValue[] GetComponents(GraphOpcode opcode);
    }

    // Только данные этапа выпечки; managed-объекты не попадают в программу runtime.
    public abstract class ComponentValue
    {
        public abstract Type Type { get; }
        public virtual bool HasData => true;
        public abstract void Emit(GraphBaker baker, Entity target, in ActionOverrideEvent action, GraphOpcode opcode, HSEComponentWorld world);
    }

    public abstract class ComponentDataValue : ComponentValue { }
    public abstract class BufferDataValue : ComponentValue { }

    /// <summary>State-only тип в составе механики, без значения и wire-payload.</summary>
    public sealed class StateComponent<T> : ComponentDataValue where T : unmanaged, IComponentData
    {
        public override Type Type => typeof(T);
        public override bool HasData => false;

        public override void Emit(GraphBaker baker, Entity target, in ActionOverrideEvent action, GraphOpcode opcode, HSEComponentWorld world)
        {
            if (opcode != GraphOpcode.AddComponent) throw new InvalidOperationException("State component has no writable data.");
            baker.AddCommand(target, action, GraphInstruction.ForType(opcode, ComponentCodecs.GetTypeId<T>(), false, world));
        }
    }

    public sealed class ComponentValue<T> : ComponentDataValue where T : unmanaged, IComponentData
    {
        public T Value;
        public ComponentValue(T value) => Value = value;
        public override Type Type => typeof(T);

        public override void Emit(GraphBaker baker, Entity target, in ActionOverrideEvent action, GraphOpcode opcode, HSEComponentWorld world)
        {
            switch (opcode)
            {
                case GraphOpcode.AddComponent: baker.AddComponent(target, action, Value, world); break;
                case GraphOpcode.SetComponent: baker.SetComponent(target, action, Value, world); break;
                default: throw new InvalidOperationException("Component value requires AddComponent or SetComponent.");
            }
        }
    }

    public sealed class BufferValue<T> : BufferDataValue where T : unmanaged, IBufferElementData
    {
        public T[] Values;
        public BufferValue(T[] values) => Values = values;
        public override Type Type => typeof(T);

        public override void Emit(GraphBaker baker, Entity target, in ActionOverrideEvent action, GraphOpcode opcode, HSEComponentWorld world)
        {
            switch (opcode)
            {
                case GraphOpcode.AddBuffer:
                    baker.AddBuffer(target, action, Values, world);
                    break;

                case GraphOpcode.ReplaceBuffer:
                    baker.ReplaceBuffer(target, action, Values, world);
                    break;

                default:
                    throw new InvalidOperationException("Buffer value requires AddBuffer or ReplaceBuffer.");
            }
        }
    }
}
