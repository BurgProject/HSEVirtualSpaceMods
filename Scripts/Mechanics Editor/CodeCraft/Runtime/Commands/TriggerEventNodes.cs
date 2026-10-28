using System;
using System.Collections.Generic;
using System.Linq;
using GraphProcessor;
using HSE.Core.Components;
using UnityEngine;

namespace HSE.CodeCraft
{
    public abstract class TriggerEventNode : CommandEventNode
    {
        public virtual uint RequiredPlayers => 1;

        protected override bool Process()
        {
            if (!base.Process()) return false;
            if (Source.CraftObj) Source.CraftObj.AddTriggerPhysics();
            return true;
        }
    }

    [Serializable, NodeMenuItem("Events/OnTriggerEnter")]
    public sealed class TriggerEnterEventNode : TriggerEventNode
    {
        public override ActionType EventType => ActionType.OnTriggerEnter;
    }

    [Serializable, NodeMenuItem("Events/OnTriggerStay")]
    public sealed class TriggerStayEventNode : TriggerEventNode
    {
        public uint PlayerCount = 1;
        public override uint RequiredPlayers => PlayerCount;
        public override ActionType EventType => ActionType.OnTriggerStay;

        protected override bool Process()
        {
            bool valid = base.Process();
            AddStateMessage("Player count must be greater than zero.", PlayerCount == 0);
            return valid && PlayerCount != 0;
        }
    }

    [Serializable, NodeMenuItem("Events/OnTriggerExit")]
    public sealed class TriggerExitEventNode : TriggerEventNode
    {
        public override ActionType EventType => ActionType.OnTriggerExit;
    }

    /// <summary>Декларативный потребитель события зоны: перенос выполняется сервером для участников этой фазы, без изменения скорости.</summary>
    [Serializable, NodeMenuItem("Data/Players/Teleport From Trigger")]
    public sealed class TriggerTeleportNode : BaseHSENode
    {
        [Input] public ConditionalLink Exec;
        [Output(allowMultiple = false)] public ConditionalLink Next;
        [Input, ShowAsDrawer] public Vector3 Destination;

        protected override bool Process()
        {
            var edges = GetPort(nameof(Exec), null).GetEdges();
            bool valid = edges.Count == 1 && edges[0].outputNode is TriggerEventNode &&
                edges[0].outputFieldName == nameof(CommandEventNode.Exec);
            AddStateMessage("Connect Exec directly to a trigger event.", !valid);
            Next = Exec;
            return valid;
        }
    }

    internal static class TriggerGraphCompiler
    {
        public static bool Compile(BaseGraph graph, GraphBaker baker)
        {
            var rules = new Dictionary<int, List<TriggerGraphRule>>();
            var keys = new HashSet<(int, ActionType)>();
            bool valid = true;
            foreach (var node in graph.nodes.OfType<TriggerEventNode>())
            {
                var edges = node.GetPort(nameof(node.Exec), null).GetEdges();
                if (edges.Count == 0) continue;
                if (node.Source == null || node.Source.InstanceID == 0 || node.RequiredPlayers == 0)
                {
                    valid &= Fail(node, "Trigger event requires a source and positive player count.");
                    continue;
                }
                int id = node.Source.InstanceID;
                if (!keys.Add((id, node.EventType)))
                    valid &= Fail(node, "Use one trigger event of each kind per source.");
                var rule = new TriggerGraphRule { Event = node.EventType, SourceInstanceId = id, RequiredPlayers = node.RequiredPlayers };
                foreach (var edge in edges)
                {
                    if (edge.inputNode is TriggerTeleportNode teleport)
                    {
                        if (rule.Teleport) valid &= Fail(node, "Trigger event has multiple teleport destinations.");
                        rule.Teleport = true;
                        rule.Destination = teleport.Destination;
                        if (!Unity.Mathematics.math.all(Unity.Mathematics.math.isfinite(rule.Destination)))
                            valid &= Fail(teleport, "Teleport destination must be finite.");
                    }
                }
                if (!rules.TryGetValue(id, out var entries)) rules.Add(id, entries = new List<TriggerGraphRule>());
                entries.Add(rule);
            }
            if (!valid || baker == null) return valid;
            foreach (var pair in rules)
            {
                var entity = baker.GetEntity(pair.Key);
                var action = new ActionOverrideEvent(ActionType.OnStart, 0, pair.Key);
                baker.AddBuffer<TriggerGraphRule>(entity, action, HSEComponentWorld.Server);
                baker.ReplaceBuffer(entity, action, pair.Value.OrderBy(rule => rule.Event).ToArray(), HSEComponentWorld.Server);
            }
            return true;
        }

        private static bool Fail(BaseNode node, string message)
        {
            node.AddMessage(message, NodeMessageType.Error);
            return false;
        }
    }
}
