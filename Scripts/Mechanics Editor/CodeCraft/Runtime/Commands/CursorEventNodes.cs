using System;
using System.Collections.Generic;
using System.Linq;
using GraphProcessor;
using HSE.Core.Components;
using UnityEngine;

namespace HSE.CodeCraft
{
    /// <summary>
    /// Fires while a player's view ray (the crosshair / cursor) hits the Source within <see cref="Distance"/>.
    /// Client: only the hovering client runs the chain (World = Client). Server: the client reports the hover in its
    /// input, the server checks it with one raycast when the target changes and runs the chain for every hovering player.
    /// </summary>
    public abstract class CursorEventNode : CommandEventNode
    {
        public CursorTriggerMode Mode = CursorTriggerMode.Client;
        [Tooltip("Largest distance (metres) from the player's view to the hovered point at which the event fires.")]
        public float Distance = 3f;

        protected override bool Process()
        {
            if (!base.Process()) return false;
            bool validDistance = Distance > 0 && float.IsFinite(Distance);
            AddStateMessage("Distance must be above zero.", !validDistance);
            if (!validDistance) return false;
            // The view ray needs a collider on the source
            if (Source.CraftObj) Source.CraftObj.AddPhysics();
            Exec = new ConditionalLink { OverrideEvent = new ActionOverrideEvent(EventType, (uint)Mode, Source.InstanceID) };
            return true;
        }
    }

    [Serializable, NodeMenuItem("Events/Cursor/OnCursorEnter")]
    public sealed class CursorEnterEventNode : CursorEventNode
    {
        public override ActionType EventType => ActionType.OnCursorEnter;
    }

    [Serializable, NodeMenuItem("Events/Cursor/OnCursorStay")]
    public sealed class CursorStayEventNode : CursorEventNode
    {
        public override ActionType EventType => ActionType.OnCursorStay;
    }

    [Serializable, NodeMenuItem("Events/Cursor/OnCursorExit")]
    public sealed class CursorExitEventNode : CursorEventNode
    {
        public override ActionType EventType => ActionType.OnCursorExit;
    }

    /// <summary>Collects the cursor event nodes of each source into one <see cref="CursorTriggerRule"/> buffer.</summary>
    internal static class CursorTriggerCompiler
    {
        public static bool IsCursorEvent(ActionType type) =>
            type == ActionType.OnCursorEnter || type == ActionType.OnCursorStay || type == ActionType.OnCursorExit;

        public static bool Compile(BaseGraph graph, GraphBaker baker)
        {
            var rules = new Dictionary<int, List<CursorTriggerRule>>();
            var keys = new HashSet<(int, ActionType, CursorTriggerMode)>();
            bool valid = true;
            foreach (var node in graph.nodes.OfType<CursorEventNode>())
            {
                if (node.GetPort(nameof(node.Exec), null).GetEdges().Count == 0) continue;
                if (node.Source == null || node.Source.InstanceID == 0 || !(node.Distance > 0))
                {
                    valid &= Fail(node, "Cursor event requires a Source and a positive Distance.");
                    continue;
                }
                int id = node.Source.InstanceID;
                if (!keys.Add((id, node.EventType, node.Mode)))
                    valid &= Fail(node, "Use one cursor event of each kind and mode per source.");
                if (!rules.TryGetValue(id, out var entries)) rules.Add(id, entries = new List<CursorTriggerRule>());
                entries.Add(new CursorTriggerRule
                {
                    Event = node.EventType, Mode = node.Mode, SourceInstanceId = id, Distance = node.Distance,
                });
            }
            if (!valid || baker == null) return valid;
            foreach (var pair in rules)
            {
                var action = new ActionOverrideEvent(ActionType.Initialization, 0, pair.Key);
                baker.AddBuffer(baker.GetEntity(pair.Key), action,
                    pair.Value.OrderBy(rule => rule.Event).ThenBy(rule => rule.Mode).ToArray(), HSEComponentWorld.Both);
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
