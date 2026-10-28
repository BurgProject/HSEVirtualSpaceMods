using GraphProcessor;
using UnityEditor;
using UnityEngine;

public class ToolBarGraphView : ToolbarView
{
    private BaseGraph graph;
    public ToolBarGraphView(GraphProcessor.BaseGraphView graphView) : base(graphView)
    {
        graph = graphView.graph;
    }
    protected override void AddButtons()
    {
        AddButton("Center", graphView.ResetPositionAndZoom);

        bool processorVisible = graphView.GetPinnedElementStatus<ConditionalProcessorView>() != UnityEngine.UIElements.DropdownMenuAction.Status.Hidden;
        showProcessor = AddToggle("Show Processor", processorVisible, (v) => graphView.ToggleView<ConditionalProcessorView>());
        bool exposedParamsVisible = graphView.GetPinnedElementStatus<ExposedParameterView>() != UnityEngine.UIElements.DropdownMenuAction.Status.Hidden;
        showParameters = AddToggle("Show Parameters", exposedParamsVisible, (v) => graphView.ToggleView<ExposedParameterView>());

        AddButton("Save in Asset", SaveAsset);
        AddButton("Show In Project", () => EditorGUIUtility.PingObject(graph), false);
    }
    public void SaveAsset()
    {
        string path = EditorUtility.SaveFilePanel("Save Graph", "Assets/CodeCraft/Resources", "NewCraft", "asset");

        if (string.IsNullOrEmpty(path))
            return;

        var relativePath = FileUtil.GetProjectRelativePath(path);

        graph = ScriptableObject.Instantiate(graphView.graph);
        AssetDatabase.CreateAsset(graph, relativePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = graph;
        EditorUtility.SetDirty(graph);
    }
}
