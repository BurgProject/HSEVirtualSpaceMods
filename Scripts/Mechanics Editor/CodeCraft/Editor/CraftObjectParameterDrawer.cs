using GraphProcessor;
using UnityEditor;
using HSE.CodeCraft.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;

namespace HSE.CodeCraft
{
    [InitializeOnLoad]
    [CustomPropertyDrawer(typeof(CraftObjectParameter))]
    public sealed class CraftObjectParameterDrawer : ExposedParameterDrawer
    {
        static CraftObjectParameterDrawer()
        {
            ParameterNodeView.RegisterAppearance<CraftObjectParameter>(ApplyNodeAppearance);
            ExposedParameterView.RegisterObjectDrop<GameObject>(DropObject);
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            var data = property.FindPropertyRelative("data");
            foreach (var name in new[] { "Reference", "TransformUsage", "UniqueName", "Mode", "IsPhysics" })
                root.Add(new PropertyField(data.FindPropertyRelative(name)));
            root.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (property.serializedObject.targetObject is not ExposedParameterWorkaround wrapper || !wrapper.graph) return;
                EditorUtility.SetDirty(wrapper.graph);
                var parameter = property.managedReferenceValue as CraftObjectParameter;
                if (parameter != null) wrapper.graph.NotifyExposedParameterChanged(parameter);
            });
            return root;
        }

        private static bool DropObject(BaseGraphView view, GameObject value, bool perform)
        {
            if (view is not CustomContextMenuGraphView || !value.scene.IsValid() || !value.scene.isLoaded) return false;
            if (!perform) return true;

            view.RegisterCompleteObjectUndo("Add CraftObject parameter");
            string name = value.name;
            int suffix = 1;
            while (view.graph.exposedParameters.Any(parameter => parameter.name == name))
                name = value.name + " " + suffix++;
            var source = view.graph.nodes.OfType<CodeCraftNode>().FirstOrDefault(node => node.Reference == value);
            object initialValue = source != null && value.TryGetComponent<CraftObject>(out var marker)
                ? new CraftInstance(marker) { Source = source } : value;
            string guid = view.graph.AddExposedParameter(name, typeof(CraftObjectParameter), initialValue);
            var parameter = (CraftObjectParameter)view.graph.GetExposedParameterFromGUID(guid);
            if (!parameter.TryReadValue(view.graph, out _, out var error)) Debug.LogWarning(error, value);
            EditorUtility.SetDirty(view.graph);
            return true;
        }

        private static void ApplyNodeAppearance(ParameterNodeView view)
        {
            CraftParameterNavigation.Install(view);
            const string iconName = "EditorNodeTitleIcons/CodeCraftNode";

            view.titleContainer.style.height = StyleKeyword.Auto;
            view.titleContainer.style.minHeight = 36f;
            view.titleContainer.style.alignItems = Align.Center;
            view.titleContainer.style.borderBottomColor = Color.red;
            view.titleContainer.style.borderBottomWidth = 5f;

            var border = view.Q("node-border");
            if (border != null) border.style.borderBottomColor = Color.red;

            var icon = view.titleContainer.Q<Image>(iconName) ??
                view.titleContainer.AddResourceImageWithBorderStyle(iconName, null, 25f, 100);
            if (icon == null) return;

            view.titleContainer.Insert(0, icon);
            icon.style.alignSelf = Align.Center;
            icon.style.marginLeft = 5f;
            icon.style.marginRight = 5f;
            icon.style.flexShrink = 0f;
        }
    }
}
