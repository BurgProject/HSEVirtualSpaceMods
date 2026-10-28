using GraphProcessor;
using HSE.CodeCraft;
using HSE.CodeCraft.Editor;
using UnityEngine;
using UnityEngine.UIElements;

[NodeCustomEditor(typeof(BaseHSENode))]
public class BaseCraftNodeView : BaseNodeView
{
    private const string _editorNodesPathIcon = "EditorNodeTitleIcons/";
    public override void Enable()
    {
        base.Enable();
        var node = nodeTarget as BaseHSENode;

        // Create your fields using node's variables and add them to the controlsContainer
        string nodeTitleIconName = string.IsNullOrEmpty(node.EditorNodeTitleIconName) ? node.GetType().Name : node.EditorNodeTitleIconName;
        var element = titleContainer.AddResourceImageWithBorderStyle(_editorNodesPathIcon + nodeTitleIconName, null, 25f, 100);
        if(element != null)
            element.style.marginRight = new Length(5);
        VisualElement nodeBorder = this.Q("node-border");
        nodeBorder.style.borderBottomColor = nodeTarget.color;
    }
}
