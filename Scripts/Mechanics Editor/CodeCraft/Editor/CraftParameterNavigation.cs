using System.Collections.Generic;
using System.Linq;
using GraphProcessor;
using UnityEngine.UIElements;

namespace HSE.CodeCraft
{
    internal static class CraftParameterNavigation
    {
        internal static void Install(ParameterNodeView view)
        {
            view.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                var parameter = (ParameterNode)view.nodeTarget;
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Locate/Exposed Parameter", _ => FocusParameter(view.owner, parameter.parameterGUID));

                var source = FindSource(view.owner.graph, parameter.parameterGUID);
                evt.menu.AppendAction("Locate/Source CraftObject", _ => FocusSource(view.owner, source),
                    source != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }));
        }

        private static void FocusParameter(BaseGraphView graphView, string guid)
        {
            graphView.OpenPinned<ExposedParameterView>();
            var panel = graphView.Q<ExposedParameterView>();
            if (panel == null) return;

            panel.schedule.Execute(() =>
            {
                var field = panel.Query<ExposedParameterFieldView>().ToList().FirstOrDefault(item => item.parameter.guid == guid);
                if (field == null) return;
                graphView.ClearSelection();
                graphView.AddToSelection(field);
                panel.Q<ScrollView>()?.ScrollTo(field);
                field.Focus();
            });
        }

        private static void FocusSource(BaseGraphView graphView, CodeCraftNode source)
        {
            if (source == null || !graphView.nodeViewsPerNode.TryGetValue(source, out var view)) return;
            graphView.ClearSelection();
            graphView.AddToSelection(view);
            graphView.FrameSelection();
        }

        private static CodeCraftNode FindSource(BaseGraph graph, string guid)
        {
            var pending = new Stack<BaseNode>(Writers(graph, guid));
            var visited = new HashSet<BaseNode>();
            var sources = new HashSet<CodeCraftNode>();
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (!visited.Add(node)) continue;
                if (node is CodeCraftNode craft)
                {
                    sources.Add(craft);
                    continue;
                }
                foreach (var input in node.GetInputNodes()) pending.Push(input);
                if (node is ParameterNode parameter && parameter.accessor == ParameterAccessor.Get)
                    foreach (var writer in Writers(graph, parameter.parameterGUID)) pending.Push(writer);
            }
            return sources.Count == 1 ? sources.First() : null;
        }

        private static IEnumerable<ParameterNode> Writers(BaseGraph graph, string guid)
            => graph.nodes.OfType<ParameterNode>().Where(node => node.parameterGUID == guid &&
                node.accessor == ParameterAccessor.Set && node.IsConnected);
    }
}
