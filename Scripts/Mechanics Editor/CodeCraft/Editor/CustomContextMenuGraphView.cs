using GraphProcessor;
using HSE.CodeCraft;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class CustomContextMenuGraphView : GraphProcessor.BaseGraphView
{
    BaseGraph observedGraph;
    bool validationPending;

    public CustomContextMenuGraphView(EditorWindow window) : base(window)
    {
        initialized += ObserveGraph;
        RegisterCallback<AttachToPanelEvent>(_ => ObserveGraph());
        RegisterCallback<DetachFromPanelEvent>(_ => StopObserving());
    }

    void ObserveGraph()
    {
        if (observedGraph != graph)
        {
            if (observedGraph != null) observedGraph.onGraphChanges -= OnGraphChanged;
            observedGraph = graph;
            if (observedGraph != null) observedGraph.onGraphChanges += OnGraphChanged;
        }
        QueueValidation();
    }

    void OnGraphChanged(GraphChanges changes) => QueueValidation();

    void QueueValidation()
    {
        if (validationPending) return;
        validationPending = true;
        EditorApplication.delayCall += ValidateGraph;
    }

    void ValidateGraph()
    {
        validationPending = false;
        if (observedGraph == null || panel == null) return;
        new ConditionalProcessor(observedGraph).ValidatePreview();
    }

    void StopObserving()
    {
        if (observedGraph != null) observedGraph.onGraphChanges -= OnGraphChanged;
        observedGraph = null;
        EditorApplication.delayCall -= ValidateGraph;
        validationPending = false;
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendSeparator();

        foreach (var nodeMenuItem in NodeProvider.GetNodeMenuEntries())
        {
            var mousePos = (evt.currentTarget as VisualElement).ChangeCoordinatesTo(contentViewContainer, evt.localMousePosition);
            Vector2 nodePosition = mousePos;
            evt.menu.AppendAction("Create/" + nodeMenuItem.path,
                (e) => CreateNodeOfType(nodeMenuItem.type, nodePosition),
                DropdownMenuAction.AlwaysEnabled
            );
        }

        base.BuildContextualMenu(evt);
    }


    void CreateNodeOfType(System.Type type, Vector2 position)
    {
        RegisterCompleteObjectUndo("Added " + type + " node");
        AddNode(BaseNode.CreateFromType(type, position));
    }
}
