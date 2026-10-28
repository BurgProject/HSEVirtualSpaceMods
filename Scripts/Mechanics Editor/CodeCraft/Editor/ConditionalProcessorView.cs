using GraphProcessor;
using HSE.CodeCraft;
using UnityEngine;
using UnityEngine.UIElements;

public class ConditionalProcessorView : PinnedElementView
{
    BaseGraphView graphView;

    public ConditionalProcessorView()
    {
        title = "Process panel";
    }

    protected override void Initialize(GraphProcessor.BaseGraphView graphView)
    {
        this.graphView = graphView;

        Button b = new Button(OnPlay) { name = "ActionButton", text = "Play !" };

        content.Add(b);
    }


    void OnPlay()
    {
        var graph = graphView.graph;
        var linkedObject = graph != null ? graph.linkedSceneObject : null;
        var scenario = linkedObject != null ? linkedObject.GetComponent<CodeCraftScenario>() : null;
        if (scenario == null)
        {
            Debug.LogError("The current graph is not linked to a CodeCraftScenario.");
            return;
        }

        var processor = new ConditionalProcessor(graph);
        processor.Run();

        if (!processor.IsGraphWithErrors)
            scenario.BakeScene();
        else Debug.LogError($"Scenario graph for object {scenario.name} have an error, please check the warnings on nodes and resolve them!");
        processor.ClearGraphErrors();
    }
}
