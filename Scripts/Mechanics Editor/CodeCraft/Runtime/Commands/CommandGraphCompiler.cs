using System;
using System.Collections.Generic;
using HSE.Core.Components;
using System.Linq;
using GraphProcessor;
using HSE.Mechanics.Components;
using HSE.Serialization.Wire.Entities;
using HSE.Serialization.Wire.Generated;

namespace HSE.CodeCraft
{
    /// <summary>Линейные Exec-цепочки компилируются после вычисления входных данных. Циклы и неоднозначные порядки запрещены.</summary>
    public static class CommandGraphCompiler
    {
        public static bool ValidateTopology(BaseGraph graph)
        {
            if (!graph.nodes.Any(n => n is ComponentCommandNode)) return true;
            var incoming = graph.nodes.ToDictionary(n => n, n => 0);
            var outputs = graph.nodes.ToDictionary(n => n, n => new List<BaseNode>());
            foreach (var edge in graph.edges)
            {
                if (edge.inputNode == null || edge.outputNode == null ||
                    !incoming.ContainsKey(edge.inputNode) || !outputs.ContainsKey(edge.outputNode))
                {
                    UnityEngine.Debug.LogError($"Edge {edge.GUID} refers to a node outside the current graph.", graph);
                    return false;
                }
                incoming[edge.inputNode]++;
                outputs[edge.outputNode].Add(edge.inputNode);
            }
            var ready = new Queue<BaseNode>(incoming.Where(p => p.Value == 0).Select(p => p.Key));
            int count = 0;
            while (ready.Count != 0)
            {
                var node = ready.Dequeue();
                count++;
                foreach (var next in outputs[node]) if (--incoming[next] == 0) ready.Enqueue(next);
            }
            if (count != graph.nodes.Count)
            {
                foreach (var pair in incoming.Where(pair => pair.Value > 0))
                    Fail(pair.Key, "Command graphs cannot contain data or execution cycles.");
                return false;
            }

            bool valid = true;
            foreach (var node in graph.nodes.OfType<ComponentCommandNode>().Where(n => n.IsConnected))
            {
                if (node.GetPort(nameof(node.Exec), null).GetEdges().Count != 1)
                {
                    valid &= Fail(node, "Connect exactly one execution input.");
                    continue;
                }
                var edges = node.GetPort(nameof(node.Next), null).GetEdges();
                if (edges.Count > 1 || edges.Any(e => !(e.inputNode is ComponentCommandNode) || e.inputFieldName != nameof(node.Exec)))
                    valid &= Fail(node, "Next must connect to one command Exec; branching is not supported.");
                var source = node.GetPort(nameof(node.Exec), null).GetEdges()[0];
                if (source.outputNode is ComponentCommandNode && source.outputFieldName != nameof(node.Next))
                    valid &= Fail(node, "Execution input must come from Next.");
                if (source.outputPort.GetEdges().Count != 1)
                    valid &= Fail(node, "An event output must start one sequence. Chain commands through Next.");
            }
            return valid;
        }

        public static bool Compile(BaseGraph graph, GraphBaker baker)
        {
            if (!ValidateTopology(graph)) return false;
            var nodes = graph.nodes.OfType<ComponentCommandNode>().Where(n => n.IsConnected).ToArray();
            var roots = nodes.Where(n => !(n.GetPort(nameof(n.Exec), null).GetEdges()[0].outputNode is ComponentCommandNode))
                .OrderBy(n => n.GUID, StringComparer.Ordinal);
            var plan = new List<(ComponentCommandNode Node, ActionOverrideEvent Action, CommandComponentType Type, ComponentValue Value)>();
            int visited = 0;
            bool valid = true;
            var ownership = new Dictionary<(int Target, ActionOverrideEvent Action, uint Type), ComponentCommandNode>();
            var eventNodes = new HashSet<CommandEventNode>();
            foreach (var root in roots)
            {
                var action = root.Exec.OverrideEvent;
                if (GraphProgramReader.ResolveEvent(action.ActionType) == ActionType.None)
                    valid &= Fail(root, "Unsupported or missing event.");
                var source = root.GetPort(nameof(root.Exec), null).GetEdges()[0].outputNode;
                if (source is CommandEventNode eventNode) eventNodes.Add(eventNode);
                ComponentCommandNode node = root;
                while (node != null)
                {
                    try
                    {
                        bool prepared = node.TryPrepareCommands(out var commands);
                        valid &= prepared;
                        foreach (var command in prepared ? commands : Enumerable.Empty<(CommandComponentType Type, ComponentValue Value)>())
                        {
                            uint id = command.Type.Id;
                            valid &= ValidateWorld(node, action, id);
                            var key = (node.Target.InstanceID, action, id);
                            if (ownership.TryGetValue(key, out var owner) && owner != root)
                                valid &= Fail(node, "Independent sequences modify the same target/event/type. Join them through Next.");
                            ownership[key] = root;
                            plan.Add((node, action, command.Type, command.Value));
                        }
                    }
                    catch (Exception exception) { valid &= Fail(node, exception.GetBaseException().Message); }
                    visited++;
                    var edges = node.GetPort(nameof(node.Next), null).GetEdges();
                    node = edges.Count == 0 ? null : (ComponentCommandNode)edges[0].inputNode;
                }
            }
            if (visited != nodes.Length)
            {
                UnityEngine.Debug.LogError("Not all commands are reachable from events.", graph);
                return false;
            }
            if (!valid || baker == null) return valid;
            foreach (var source in eventNodes) source.Prepare(baker);
            foreach (var item in plan) item.Node.Emit(baker, item.Action, item.Type, item.Value);
            return true;
        }

        private static bool ValidateWorld(ComponentCommandNode node, ActionOverrideEvent action, uint id)
        {
            bool valid = true;
            bool network = ComponentCodecs.IsNetworkComponent(id);
            if (!GraphProgramReader.IsUiWorldAllowed(id, node.Opcode, node.World))
                valid &= Fail(node, "UI AddComponent and SetComponent require Client. Use OnStart or another runtime event, not Initialization.");
            if (action.ActionType == ActionType.Initialization)
            {
                if (node.World != HSEComponentWorld.Both) valid &= Fail(node, "Initialization requires Both.");
            }
            else if (network && node.World != HSEComponentWorld.Server) valid &= Fail(node, "Network component writes require Server.");
            if (CursorTriggerCompiler.IsCursorEvent(action.ActionType))
            {
                // Client cursor events only reach the hovering client; server ones only the server
                if (action.ActionIndex == (uint)CursorTriggerMode.Client && node.World == HSEComponentWorld.Server)
                    valid &= Fail(node, "A Client cursor event runs only on the hovering client: set World to Client.");
                if (action.ActionIndex == (uint)CursorTriggerMode.Server && node.World == HSEComponentWorld.Client)
                    valid &= Fail(node, "A Server cursor event runs on the server: set World to Server or Both.");
            }
            if (network && (node.Opcode == GraphOpcode.RemoveComponent ||
                action.ActionType != ActionType.Initialization && (node.Opcode == GraphOpcode.AddComponent || node.Opcode == GraphOpcode.AddBuffer)))
                valid &= Fail(node, "Ghost composition cannot be changed after initialization; use Set for existing network data.");
            return valid;
        }

        private static bool Fail(BaseNode node, string message)
        {
            node.AddMessage(message, NodeMessageType.Error);
            return false;
        }
    }
}
