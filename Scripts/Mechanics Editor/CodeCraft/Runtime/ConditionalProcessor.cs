using GraphProcessor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HSE.CodeCraft
{
    public class ConditionalProcessor : BaseGraphProcessor
    {
        private List<BaseNode> processList;
        private readonly GraphBaker _baker;
        private readonly Dictionary<string, ParameterNode> parameterWriters = new();

        public ConditionalProcessor(BaseGraph graph, GraphBaker baker) : base(graph) { _baker = baker; }
        public ConditionalProcessor(BaseGraph graph) : base(graph) {  }
        public override void UpdateComputeOrder()
        {
            processList = new List<BaseNode>();
            var visited = new HashSet<BaseNode>();
            var visiting = new HashSet<BaseNode>();

            parameterWriters.Clear();
            foreach (var writer in graph.nodes.OfType<ParameterNode>().Where(node => node.IsConnected && node.accessor == ParameterAccessor.Set))
            {
                if (graph.GetExposedParameterFromGUID(writer.parameterGUID) is not CraftObjectParameter) continue;
                if (parameterWriters.TryAdd(writer.parameterGUID, writer)) continue;
                writer.AddMessage("Only one connected Set is allowed per CraftObject parameter.", NodeMessageType.Error);
                IsGraphWithErrors = true;
            }

            foreach (var node in graph.nodes.Where(n => n != null && n.IsConnected).OrderBy(n => n.computeOrder))
                Visit(node, visited, visiting);
        }

        private void Visit(BaseNode node, HashSet<BaseNode> visited, HashSet<BaseNode> visiting)
        {
            if (visited.Contains(node)) return;
            if (!visiting.Add(node))
            {
                node.AddMessage("Cyclic parameter dependency.", NodeMessageType.Error);
                IsGraphWithErrors = true;
                return;
            }

            foreach (var input in node.GetInputNodes())
                Visit(input, visited, visiting);

            if (node is ParameterNode parameterNode &&
                parameterNode.accessor == ParameterAccessor.Get &&
                graph.GetExposedParameterFromGUID(parameterNode.parameterGUID) is CraftObjectParameter &&
                parameterWriters.TryGetValue(parameterNode.parameterGUID, out var writer))
            {
                Visit(writer, visited, visiting);
            }

            visiting.Remove(node);
            visited.Add(node);
            processList.Add(node);
        }

        public override void Run()
        {
            try { RunValidated(); }
            catch (System.Exception exception)
            {
                IsGraphWithErrors = true;
                Debug.LogError(exception.GetBaseException().Message, graph);
            }
        }

        /// <summary>Проверяет все подключённые ноды и команды, не записывая их в GraphBaker.</summary>
        public void ValidatePreview()
        {
            IsGraphWithErrors = false;
            foreach (var node in graph.nodes) node?.BeginMessageUpdate();
            try { ValidateNodes(); }
            catch (System.Exception exception)
            {
                IsGraphWithErrors = true;
                Debug.LogError(exception.GetBaseException().Message, graph);
            }
            finally
            {
                foreach (var node in graph.nodes) node?.EndMessageUpdate();
            }
        }

        private void ValidateNodes()
        {
            bool connectionsValid = false;
            try
            {
                connectionsValid = GraphConnectionSynchronization.EnsureCurrent(graph);
                if (connectionsValid) graph.UpdateComputeOrder();
                else IsGraphWithErrors = true;
                UpdateComputeOrder();
            }
            catch (System.Exception exception)
            {
                IsGraphWithErrors = true;
                Debug.LogError(exception.GetBaseException().Message, graph);
                processList = graph.nodes.Where(node => node != null && node.IsConnected).ToList();
            }

            if (IsGraphWithErrors) return;
            var failed = new HashSet<BaseNode>();
            foreach (var node in processList)
            {
                try
                {
                    bool failedWriter = node is ParameterNode reader && reader.accessor == ParameterAccessor.Get &&
                        parameterWriters.TryGetValue(reader.parameterGUID, out var writer) && failed.Contains(writer);
                    if (failedWriter || node.GetInputNodes().Any(failed.Contains))
                    {
                        if (node is ParameterNode parameterNode) parameterNode.output = null;
                        node.AddMessage("An input dependency is invalid.", NodeMessageType.Error);
                        IsGraphWithErrors = true;
                        failed.Add(node);
                        continue;
                    }
                    if (!node.OnProcess())
                    {
                        IsGraphWithErrors = true;
                        failed.Add(node);
                    }
                }
                catch (System.Exception exception)
                {
                    node.AddMessage(exception.GetBaseException().Message, NodeMessageType.Error);
                    IsGraphWithErrors = true;
                    failed.Add(node);
                }
            }

            if (!connectionsValid) return;
            ValidateStage(() => CommandGraphCompiler.Compile(graph, null));
            ValidateStage(() => TriggerGraphCompiler.Compile(graph, null));
            ValidateStage(() => CursorTriggerCompiler.Compile(graph, null));
        }

        private void ValidateStage(System.Func<bool> validate)
        {
            try { IsGraphWithErrors |= !validate(); }
            catch (System.Exception exception)
            {
                IsGraphWithErrors = true;
                Debug.LogError(exception.GetBaseException().Message, graph);
            }
        }

        private void RunValidated()
        {
            ValidatePreview();
            if (IsGraphWithErrors) return;

            if (_baker != null)
            {
                var prepared = new Dictionary<int, CraftObjectSettings>();
                foreach (var node in processList)
                {
                    var instance = node is CodeCraftNode craft ? craft.Entity :
                        node is ParameterNode parameterNode && parameterNode.accessor == ParameterAccessor.Get
                            ? parameterNode.output as CraftInstance : null;
                    if (instance?.Source == null) continue;
                    try
                    {
                        if (prepared.TryGetValue(instance.InstanceID, out var settings))
                        {
                            if (!settings.Matches(instance.Source))
                            {
                                node.AddMessage("Conflicting CraftObject settings for the same object.", NodeMessageType.Error);
                                IsGraphWithErrors = true;
                            }
                            continue;
                        }
                        prepared.Add(instance.InstanceID, CraftObjectSettings.Copy(instance.Source));
                        instance.Prepare(_baker);
                    }
                    catch (System.Exception exception)
                    {
                        node.AddMessage(exception.GetBaseException().Message, NodeMessageType.Error);
                        IsGraphWithErrors = true;
                    }
                }
                if (IsGraphWithErrors) return;

                ValidateStage(() => GraphAuthoringAdapters.Process(graph, _baker));
                if (IsGraphWithErrors) return;
                ValidateStage(() => CommandGraphCompiler.Compile(graph, _baker));
                ValidateStage(() => TriggerGraphCompiler.Compile(graph, _baker));
                ValidateStage(() => CursorTriggerCompiler.Compile(graph, _baker));
                return;
            }
            ValidateStage(() => GraphAuthoringAdapters.Process(graph, _baker));
        }

    }
}
