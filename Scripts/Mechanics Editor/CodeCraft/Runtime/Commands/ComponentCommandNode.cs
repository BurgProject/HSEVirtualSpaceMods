using System;
using System.Collections.Generic;
using HSE.Core.Components;
using GraphProcessor;
using HSE.Mechanics.Components;
using HSE.Serialization.Wire.Generated;
using Unity.Entities;
using UnityEngine;

namespace HSE.CodeCraft
{
    public abstract class ComponentCommandNode : BaseHSENode
    {
        [Input] public ConditionalLink Exec;
        [Output(allowMultiple = false)] public ConditionalLink Next;
        [Input] public CraftInstance Target;
        [NonSerialized, Input(allowMultiple = false)] public ComponentValue[] Value;
        public bool HasValueConnection => GetPort(nameof(Value), null)?.GetEdges().Count > 0;
        public HSEComponentWorld World = HSEComponentWorld.Server;
        [HideInInspector] public uint TypeId;

        public abstract GraphOpcode Opcode { get; }
        public virtual bool Enabled => false;

        [CustomPortInput(nameof(Value), typeof(ComponentValue[]))]
        public void PullValue(List<SerializableEdge> edges)
        {
            // NodeGraphProcessor очищает IList при отключении: вход не должен владеть массивом источника.
            var values = edges.Count == 1 ? edges[0].passThroughBuffer as ComponentValue[] : null;
            if (edges.Count == 1 && edges[0].outputFieldName == nameof(Value) &&
                edges[0].outputNode is ICommandComponentSource source)
                values = source.GetComponents(Opcode);
            Value = values == null ? null : (ComponentValue[])values.Clone();
        }

        public bool AcceptsType(CommandComponentType type)
        {
            switch (Opcode)
            {
                case GraphOpcode.AddBuffer:
                case GraphOpcode.ReplaceBuffer:
                case GraphOpcode.ClearBuffer: return type.IsBuffer;
                case GraphOpcode.AddComponent:
                case GraphOpcode.SetComponent: return !type.IsBuffer;
                case GraphOpcode.SetEnabled: return type.IsEnableable;
                default: return true;
            }
        }

        protected override bool Process()
        {
            Next = Exec;
            return TryPrepareCommands(out _);
        }

        public CommandComponentType ResolveType()
        {
            if (HasValueConnection && (Value == null || Value.Length != 1 || Value[0] == null))
            {
                Fail("Expected exactly one component value.");
                return null;
            }

            var type = HasValueConnection ? CommandComponentTypes.Find(Value[0].Type) : CommandComponentTypes.Find(TypeId);
            if (type == null) Fail("Select a registered component or connect its data.");
            return type;
        }

        internal bool TryPrepareCommands(out List<(CommandComponentType Type, ComponentValue Value)> commands)
        {
            commands = new List<(CommandComponentType Type, ComponentValue Value)>();
            if (!ComponentCodecs.IsReady) return Fail("Wire codec is unavailable. Install a complete SDK with its exported codec.");
            if (Target == null || Target.InstanceID == 0) return Fail("Target must be connected to CraftObject.");
            if ((byte)World < 1 || (byte)World > 3) return Fail("Invalid command world.");

            if (!HasValueConnection)
            {
                var type = ResolveType();
                if (type == null) return false;
                if (!type.IsUserSelectable)
                    return Fail("Core components must be supplied by a logic node.");
                if (!ValidateValue(type, null)) return false;
                commands.Add((type, null));
                return true;
            }

            if (Value == null || Value.Length == 0)
                return Fail("Connected component set is empty.");

            var ids = new HashSet<uint>();
            foreach (var value in Value)
            {
                if (value == null) return Fail("Component set contains a null entry.");
                var type = CommandComponentTypes.Find(value.Type);
                if (type == null) return Fail("Component set contains an unregistered type: " + value.Type);
                if (!ids.Add(type.Id)) return Fail("Component set contains a duplicate type: " + value.Type);
                bool stateOnly = ComponentCodecs.IsStateOnly(type.Id);
                if (!value.HasData && !stateOnly)
                    return Fail("A type without data must be registered as state-only: " + value.Type);
                if (stateOnly && value.HasData)
                    return Fail("Use StateComponent<T> for state-only types: " + value.Type);
                if (Opcode == GraphOpcode.SetComponent && stateOnly) continue;
                if (!ValidateValue(type, value)) return false;
                commands.Add((type, value));
            }
            return true;
        }

        private bool ValidateValue(CommandComponentType type, ComponentValue value)
        {
            if (!AcceptsType(type)) return Fail("Type does not match this command.");
            if (Opcode != GraphOpcode.RemoveComponent && Opcode != GraphOpcode.RequireComponent)
            {
                if (Target.IsPhysics && typeof(ITransformMotionComponent).IsAssignableFrom(type.Type))
                    return Fail(type.Type.Name + " writes transforms directly and cannot be used on IsPhysics targets.");
                if (typeof(IPhysicsMotionComponent).IsAssignableFrom(type.Type) && !Target.IsPhysics)
                    return Fail(type.Type.Name + " requires an IsPhysics target with Predicted ghost mode.");
            }
            if (value != null && !value.HasData && !ComponentCodecs.IsStateOnly(type.Id))
                return Fail("StateComponent requires a registered state-only type.");
            if ((Opcode == GraphOpcode.SetComponent || Opcode == GraphOpcode.ReplaceBuffer) && value == null)
                return Fail("Connect component data.");
            if (Opcode == GraphOpcode.SetComponent && ComponentCodecs.IsStateOnly(type.Id))
                return Fail("State-only types have no data to set.");
            return true;
        }

        private bool Fail(string message)
        {
            AddMessage(message, NodeMessageType.Error);
            return false;
        }

        internal void Emit(GraphBaker baker, ActionOverrideEvent action, CommandComponentType type, ComponentValue value)
        {
            Entity entity = baker.GetEntity(Target.InstanceID);
            if (entity == Entity.Null) throw new InvalidOperationException("Command target was not registered by CraftObject.");
            bool writesData = Opcode == GraphOpcode.AddComponent || Opcode == GraphOpcode.SetComponent || Opcode == GraphOpcode.ReplaceBuffer || Opcode == GraphOpcode.AddBuffer;
            if (value != null && writesData) value.Emit(baker, entity, action, Opcode, World);
            else baker.AddCommand(entity, action, GraphInstruction.ForType(Opcode, type.Id, Enabled, World));
        }
        [CustomPortBehavior(nameof(Value))]
        public IEnumerable<PortData> ValuePort(List<SerializableEdge> edges)
        {
            var source = edges.Count == 1 ? edges[0].outputNode : null;
            yield return new PortData
            {
                displayName = (source as ICommandDataNode)?.ComponentType.Name ??
                    (source as ICommandComponentSetNode)?.ComponentSetName ?? "Value",
                displayType = Opcode == GraphOpcode.AddBuffer || Opcode == GraphOpcode.ReplaceBuffer || Opcode == GraphOpcode.ClearBuffer
                    ? typeof(BufferDataValue[])
                    : Opcode == GraphOpcode.AddComponent || Opcode == GraphOpcode.SetComponent
                        ? typeof(ComponentDataValue[]) : typeof(ComponentValue[]),
                acceptMultipleEdges = false
            };
        }
    }

    [Serializable, NodeMenuItem("Commands/Components/Add")]
    public sealed class AddComponentNode : ComponentCommandNode { 
        public override GraphOpcode Opcode => GraphOpcode.AddComponent;
        public override Color color => Color.cornflowerBlue;
    }

    [Serializable, NodeMenuItem("Commands/Components/Set")]
    public sealed class SetComponentNode : ComponentCommandNode {
        public override GraphOpcode Opcode => GraphOpcode.SetComponent;
        public override Color color => Color.blue;
    }

    [Serializable, NodeMenuItem("Commands/Components/Remove")]
    public sealed class RemoveComponentNode : ComponentCommandNode { 
        public override GraphOpcode Opcode => GraphOpcode.RemoveComponent;
        public override Color color => Color.darkBlue;
    }

    [Serializable, NodeMenuItem("Commands/Components/Require")]
    public sealed class RequireComponentNode : ComponentCommandNode { public override GraphOpcode Opcode => GraphOpcode.RequireComponent; }

    [Serializable, NodeMenuItem("Commands/Components/Set Enabled")]
    public sealed class SetComponentEnabledNode : ComponentCommandNode
    {
        public bool IsEnabled = true;
        public override bool Enabled => IsEnabled;
        public override GraphOpcode Opcode => GraphOpcode.SetEnabled;
        public override Color color => Color.steelBlue;
    }

    [Serializable, NodeMenuItem("Commands/Buffers/Add")]
    public sealed class AddBufferNode : ComponentCommandNode { 
        public override GraphOpcode Opcode => GraphOpcode.AddBuffer;
        public override Color color => Color.purple;
    
    }

    [Serializable, NodeMenuItem("Commands/Buffers/Replace")]
    public sealed class ReplaceBufferNode : ComponentCommandNode {
        public override GraphOpcode Opcode => GraphOpcode.ReplaceBuffer;
        public override Color color => Color.mediumPurple;
    }

    [Serializable, NodeMenuItem("Commands/Buffers/Clear")]
    public sealed class ClearBufferNode : ComponentCommandNode { 
        public override GraphOpcode Opcode => GraphOpcode.ClearBuffer;
        public override Color color => Color.rebeccaPurple;
    }
}
