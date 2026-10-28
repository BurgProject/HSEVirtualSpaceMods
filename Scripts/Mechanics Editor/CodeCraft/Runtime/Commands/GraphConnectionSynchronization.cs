using System;
using System.Collections.Generic;
using GraphProcessor;

namespace HSE.CodeCraft
{
    internal static class GraphConnectionSynchronization
    {
        /// <summary>Восстанавливает несериализованные связи после замены экземпляров нод, не удаляя повреждённые рёбра.</summary>
        public static bool EnsureCurrent(BaseGraph graph)
        {
            var nodes = new Dictionary<string, BaseNode>(StringComparer.Ordinal);
            bool stale = graph.nodesPerGUID.Count != graph.nodes.Count;
            foreach (var node in graph.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.GUID) || !nodes.TryAdd(node.GUID, node))
                    return Fail(graph, null, "Graph contains a missing node or duplicate node GUID.");
                stale |= !graph.nodesPerGUID.TryGetValue(node.GUID, out var cached) || !ReferenceEquals(cached, node);
            }

            foreach (var edge in graph.edges)
            {
                if (edge == null) return Fail(graph, null, "Graph contains a missing edge.");
                stale |= !IsCurrent(nodes, edge.inputNode) || !IsCurrent(nodes, edge.outputNode) ||
                    edge.inputPort == null || edge.outputPort == null ||
                    !ReferenceEquals(edge.inputPort, edge.inputNode?.GetPort(edge.inputFieldName, edge.inputPortIdentifier)) ||
                    !ReferenceEquals(edge.outputPort, edge.outputNode?.GetPort(edge.outputFieldName, edge.outputPortIdentifier)) ||
                    !edge.inputPort.GetEdges().Contains(edge) || !edge.outputPort.GetEdges().Contains(edge);
            }
            if (!stale) return true;

            graph.nodesPerGUID.Clear();
            foreach (var node in graph.nodes)
            {
                graph.nodesPerGUID.Add(node.GUID, node);
                if (node.inputPorts.Count == 0 && node.outputPorts.Count == 0)
                    node.Initialize(graph);
            }

            // Проверка до Deserialize: штатный API иначе молча удаляет связи с отсутствующими портами.
            bool valid = true;
            foreach (var edge in graph.edges)
            {
                if (edge.inputNode?.GUID != null && nodes.ContainsKey(edge.inputNode.GUID) &&
                    edge.outputNode?.GUID != null && nodes.ContainsKey(edge.outputNode.GUID))
                    edge.OnBeforeSerialize();

                edge.inputNode = null;
                edge.outputNode = null;
                edge.inputPort = null;
                edge.outputPort = null;
                edge.Deserialize(graph);
                if (!IsCurrent(nodes, edge.inputNode) || !IsCurrent(nodes, edge.outputNode) ||
                    edge.inputPort == null || edge.outputPort == null)
                    valid &= Fail(graph, edge, $"Edge {edge.GUID}: node or port is missing ({edge.outputFieldName} -> {edge.inputFieldName}). Connection preserved.");
            }
            if (!valid) return false;

            graph.edgesPerGUID.Clear();
            graph.Deserialize();
            return true;
        }

        private static bool Fail(BaseGraph graph, SerializableEdge edge, string message)
        {
            if (edge?.inputNode != null) edge.inputNode.AddMessage(message, NodeMessageType.Error);
            else if (edge?.outputNode != null) edge.outputNode.AddMessage(message, NodeMessageType.Error);
            else UnityEngine.Debug.LogError(message, graph);
            return false;
        }

        private static bool IsCurrent(Dictionary<string, BaseNode> nodes, BaseNode node) =>
            node != null && node.GUID != null && nodes.TryGetValue(node.GUID, out var current) && ReferenceEquals(current, node);
    }
}
