using System;
using System.Collections.Generic;
using System.Linq;
using GraphProcessor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HSE.CodeCraft.Editor
{
    [NodeCustomEditor(typeof(RaycastEventNode))]
    public sealed class RaycastEventNodeView : BaseCraftNodeView
    {
        public override void Enable()
        {
            base.Enable();
            var node = (RaycastEventNode)nodeTarget;
            void ChangeCount(bool add)
            {
                owner.RegisterCompleteObjectUndo("Change click count");
                if (add) node.OnClickPlusButton();
                else node.OnClickMinusButton();
                node.UpdateAllPortsLocal();
                NotifyNodeChanged();
            }
            titleButtonContainer.Add(new Button(() => ChangeCount(true)) { text = "+", tooltip = "Add click" });
            titleButtonContainer.Add(new Button(() => ChangeCount(false)) { text = "-", tooltip = "Remove last click" });
        }
    }

    [NodeCustomEditor(typeof(ComponentCommandNode))]
    public sealed class ComponentCommandNodeView : BaseCraftNodeView
    {
        public override void Enable()
        {
            base.Enable();
            var node = (ComponentCommandNode)nodeTarget;
            var picker = new VisualElement();
            controlsContainer.Add(picker);
            foreach(var fe in portsPerFieldName["Value"])
            {
                fe.portColor = nodeTarget.color;
            }
            CommandTypePicker.Add(this, picker, () => node.TypeId, id => node.TypeId = id, node.AcceptsType);

            void RefreshPicker()
            {
                bool usesData = node.HasValueConnection;
                bool requiresData = node is SetComponentNode || node is ReplaceBufferNode;
                picker.style.display = usesData || requiresData ? DisplayStyle.None : DisplayStyle.Flex;
            }

            onPortConnected += _ => schedule.Execute(RefreshPicker);
            onPortDisconnected += _ => schedule.Execute(RefreshPicker);
            RefreshPicker();
        }
    }

    internal static class CommandTypePicker
    {
        public static void Add(BaseNodeView view, VisualElement container, Func<uint> read, Action<uint> write, Func<CommandComponentType, bool> accepts)
        {
            try
            {
                var choices = new List<string> { "Select component" };
                var ids = new List<uint> { 0 };
                var types = CommandComponentTypes.All.Where(item => item.IsUserSelectable && accepts(item)).ToArray();
                var duplicateNames = new HashSet<string>(types.GroupBy(item => item.Type.Name)
                    .Where(group => group.Count() > 1).Select(group => group.Key));
                foreach (var item in types)
                {
                    choices.Add(duplicateNames.Contains(item.Type.Name) ? item.Type.Name + " [" + item.Id + "]" : item.Type.Name);
                    ids.Add(item.Id);
                }
                int index = ids.IndexOf(read());
                if (index < 0)
                {
                    index = choices.Count;
                    choices.Add("Missing type: " + read());
                    ids.Add(read());
                }
                var dropdown = new DropdownField("Component", choices, index);
                dropdown.RegisterValueChangedCallback(change =>
                {
                    view.owner.RegisterCompleteObjectUndo("Change command component");
                    write(ids[choices.IndexOf(change.newValue)]);
                    view.NotifyNodeChanged();
                });
                container.Add(dropdown);
            }
            catch (Exception exception)
            {
                container.Add(new Label(exception.Message));
            }
        }
    }
}
