using GraphProcessor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Audio.ProcessorInstance.AvailableData;

[NodeCustomEditor(typeof(ParameterNode))]
public class ParameterNodeView : BaseNodeView
{
    static readonly Dictionary<System.Type, System.Action<ParameterNodeView>> appearances = new();

    public static void RegisterAppearance<T>(System.Action<ParameterNodeView> apply) where T : ExposedParameter
        => appearances[typeof(T)] = apply;

    ParameterNode parameterNode;
    Image currentTexture2DImage;
    const int maxWidth = 100;
    const int maxHeight = 100;

    public override void Enable(bool fromInspector = false)
    {
        parameterNode = nodeTarget as ParameterNode;
        
        EnumField accessorSelector = new EnumField(parameterNode.accessor);
        accessorSelector.SetValueWithoutNotify(parameterNode.accessor);
        accessorSelector.RegisterValueChangedCallback(evt =>
        {
            parameterNode.accessor = (ParameterAccessor)evt.newValue;
            UpdatePort();
            controlsContainer.MarkDirtyRepaint();
            ForceUpdatePorts();
        });

        UpdatePort();
        controlsContainer.Add(accessorSelector);
        
        //    Find and remove expand/collapse button
        titleContainer.Remove(titleContainer.Q("title-button-container"));
        //    Remove Port from the #content
        topContainer.parent.Remove(topContainer);
        //    Add Port to the #title
        titleContainer.Add(topContainer);

        parameterNode.onParameterChanged += UpdateView;
        ChangeViewParameterNode(parameterNode);
        UpdateView();
        if (parameterNode.parameter != null && appearances.TryGetValue(parameterNode.parameter.GetType(), out var apply))
            apply(this);
    }

    private void ChangeViewParameterNode(ParameterNode node) {
        if(node.parameter.value is Texture2D tex)
        {
            if(currentTexture2DImage != null)
                controlsContainer.Remove(currentTexture2DImage);
            currentTexture2DImage = new Image();
            currentTexture2DImage.image = tex;
            currentTexture2DImage.style.alignSelf = new StyleEnum<Align>(Align.Center);
            currentTexture2DImage.style.width = new Length(maxWidth, LengthUnit.Pixel);
            currentTexture2DImage.style.height = new Length(maxHeight, LengthUnit.Pixel);
            controlsContainer.Add(currentTexture2DImage);
        }
    }
    void UpdateView()
    {
        title = parameterNode.parameter?.name;
        ChangeViewParameterNode(parameterNode);
    }
    
    void UpdatePort()
    {
        if(parameterNode.accessor == ParameterAccessor.Set)
        {
            titleContainer.AddToClassList("input");
        }
        else
        {
            titleContainer.RemoveFromClassList("input");
        }
    }
}
