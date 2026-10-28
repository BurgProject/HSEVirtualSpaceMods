using System;
using System.Linq;
using GraphProcessor;
using HSE.Mechanics.Components;
using Unity.Entities;
using UnityEngine;

namespace HSE.CodeCraft
{
    /// <summary>Составная механика выдаёт значения и StateComponent без раскрытия состава пользователю графа.</summary>
    public abstract class ComponentSetNode<TValue> : BaseHSENode, ICommandComponentSetNode, ICommandComponentSource
        where TValue : ComponentValue
    {
        [NonSerialized, Output] public TValue[] Value;
        public virtual string ComponentSetName => name;

        public override string EditorNodeTitleIconName => "BufferNode";
        public override Color color => Color.darkMagenta;
        protected abstract TValue[] CreateComponents();

        /// <summary>Выбирает состав для конкретной команды из вычисленного Value, не меняя данные других потребителей.</summary>
        public ComponentValue[] GetComponents(GraphOpcode opcode) => opcode switch
        {
            GraphOpcode.AddComponent    => CreateAddComponents(),
            GraphOpcode.SetComponent    => CreateSetComponents(),
            GraphOpcode.RemoveComponent => CreateRemoveComponents(),
            GraphOpcode.SetEnabled      => CreateEnabledComponents(),
            GraphOpcode.RequireComponent => CreateRequiredComponents(),
            GraphOpcode.AddBuffer       => CreateAddBuffers(),
            GraphOpcode.ReplaceBuffer   => CreateReplaceBuffers(),
            GraphOpcode.ClearBuffer     => CreateClearBuffers(),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };

        protected virtual ComponentValue[] CreateAddComponents() => Value;
        protected virtual ComponentValue[] CreateSetComponents() => Value?.Where(value => value == null || value.HasData).ToArray();
        protected virtual ComponentValue[] CreateRemoveComponents() => Value;
        protected virtual ComponentValue[] CreateEnabledComponents() => Value?.Where(value =>
            value == null || typeof(IEnableableComponent).IsAssignableFrom(value.Type)).ToArray();
        protected virtual ComponentValue[] CreateRequiredComponents() => Value;
        protected virtual ComponentValue[] CreateAddBuffers() => Value;
        protected virtual ComponentValue[] CreateReplaceBuffers() => Value;
        protected virtual ComponentValue[] CreateClearBuffers() => Value;

        protected override bool Process()
        {
            Value = CreateComponents();
            return Value != null && Value.Length != 0;
        }
    }

    public abstract class ComponentSetNode : ComponentSetNode<ComponentValue> { }

    [Serializable, NodeMenuItem("Data/RotateAroundAxis")]
    public sealed class RotateAroundAxisDataNode : MechanicsDataNode<RotateAroundAxis>
    {
        [Input, ShowAsDrawer] public Vector3 Axis = Vector3.up;
        [Input, ShowAsDrawer] public float Speed;
        protected override ComponentDataValue[] CreateComponents() => new ComponentDataValue[]
        {
            new ComponentValue<RotateAroundAxis>(new RotateAroundAxis { Axis = Axis, Speed = Speed })
        };
    }

}
