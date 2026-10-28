using GraphProcessor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class CustomContextMenuGraphWindow : BaseGraphWindow
{
    BaseGraph tmpGraph;
    public bool openedFromScene;

    [MenuItem("Window/02 Custom Context Menu")]
    public static BaseGraphWindow OpenWithTmpGraph()
    {
        var graphWindow = CreateWindow<CustomContextMenuGraphWindow>();

        // When the graph is opened from the window, we don't save the graph to disk
        graphWindow.tmpGraph = ScriptableObject.CreateInstance<BaseGraph>();
        graphWindow.tmpGraph.hideFlags = HideFlags.HideAndDontSave;
        graphWindow.InitializeGraph(graphWindow.tmpGraph);

        graphWindow.Show();

        return graphWindow;
    }

    public static BaseGraphWindow OpenWithGraph(BaseGraph graph)
    {
        if (graph == null) return OpenWithTmpGraph();
        var graphWindow = CreateWindow<CustomContextMenuGraphWindow>();

        graphWindow.tmpGraph = graph;
        graphWindow.SetGraph(graphWindow.tmpGraph);
        graphWindow.openedFromScene = true;

        graphWindow.Show();

        return graphWindow;
    }

    protected override void OnDestroy()
    {
        graphView?.Dispose();
        if(!openedFromScene)
            DestroyImmediate(tmpGraph);
    }

    protected override void InitializeWindow(BaseGraph graph)
    {
        titleContent = new GUIContent("Context Menu Graph");

        if (graphView == null)
        {
            graphView = new CustomContextMenuGraphView(this);
            graphView.Add(new MiniMapView(graphView));
            graphView.Add(new ToolBarGraphView(graphView)); 
        }

        rootView.Add(graphView);
    }

    protected override void InitializeGraphView(GraphProcessor.BaseGraphView view)
    {
        view.OpenPinned<ExposedParameterView>();
    }
}

[CustomEditor(typeof(BaseGraph), true)]
public class GraphAssetInspector : GraphInspector
{
    // protected override void CreateInspector()
    // {
    // }

    protected override void CreateInspector()
    {
        base.CreateInspector();

        root.Add(new Button(() => EditorWindow.GetWindow<CustomContextMenuGraphWindow>().InitializeGraph(target as BaseGraph))
        {
            text = "Open custom context menu graph window"
        });
    }
}
