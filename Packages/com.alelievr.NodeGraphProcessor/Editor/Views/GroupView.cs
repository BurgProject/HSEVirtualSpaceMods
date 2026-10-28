using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GraphProcessor
{
    public class GroupView : UnityEditor.Experimental.GraphView.Group
	{
		public BaseGraphView	owner;
		public Group		    group;

        Label                   titleLabel;
        ColorField              colorField;

        readonly string         groupStyle = "GraphProcessorStyles/GroupView";
        bool                    initialized;

        public GroupView()
        {
            styleSheets.Add(Resources.Load<StyleSheet>(groupStyle));
		}
		
		private static void BuildContextualMenu(ContextualMenuPopulateEvent evt) {}
		
		public void Initialize(BaseGraphView graphView, Group block)
		{
			group = block;
			owner = graphView;

            title = block.title;
            // Resizes were only written to group.size, so position.size can still be the creation size
            var rect = block.position;
            if (block.size.x > 0 && block.size.y > 0) rect.size = block.size;
            SetPosition(rect);
			
			this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
			
            headerContainer.Q<TextField>().RegisterCallback<ChangeEvent<string>>(TitleChangedCallback);
            titleLabel = headerContainer.Q<Label>();

            colorField = new ColorField{ value = group.color, name = "headerColorPicker" };
            colorField.RegisterValueChangedCallback(e =>
            {
                UpdateGroupColor(e.newValue);
            });
            UpdateGroupColor(group.color);

            headerContainer.Add(colorField);

            InitializeInnerNodes();
            initialized = true;
		}

        void InitializeInnerNodes()
        {
            foreach (var nodeGUID in group.innerNodeGUIDs.ToList())
            {
                if (!owner.graph.nodesPerGUID.ContainsKey(nodeGUID))
                {
                    Debug.LogWarning("Node GUID not found: " + nodeGUID);
                    group.innerNodeGUIDs.Remove(nodeGUID);
                    continue ;
                }
                var node = owner.graph.nodesPerGUID[nodeGUID];
                var nodeView = owner.nodeViewsPerNode[node];

                AddElement(nodeView);
            }
        }

        protected override void OnElementsAdded(IEnumerable<GraphElement> elements)
        {
            foreach (var element in elements)
            {
                var node = element as BaseNodeView;

                // Adding an element that is not a node currently supported
                if (node == null)
                    continue;

                if (!group.innerNodeGUIDs.Contains(node.nodeTarget.GUID))
                    group.innerNodeGUIDs.Add(node.nodeTarget.GUID);
            }
            MarkDirty();
            base.OnElementsAdded(elements);
        }

        protected override void OnElementsRemoved(IEnumerable<GraphElement> elements)
        {
            // Only remove the nodes when the group exists in the hierarchy
            if (parent != null)
            {
                foreach (var elem in elements)
                {
                    if (elem is BaseNodeView nodeView)
                    {
                        group.innerNodeGUIDs.Remove(nodeView.nodeTarget.GUID);
                    }
                }
            }

            MarkDirty();
            base.OnElementsRemoved(elements);
        }

        public void UpdateGroupColor(Color newColor)
        {
            group.color = newColor;
            style.backgroundColor = newColor;
            MarkDirty();
        }

        void TitleChangedCallback(ChangeEvent< string > e)
        {
            group.title = e.newValue;
            MarkDirty();
        }

		public override void SetPosition(Rect newPos)
		{
			base.SetPosition(newPos);

			group.position = newPos;
			group.size = newPos.size;
			MarkDirty();
		}

		/// <summary>
		/// Group edits bypass the graph's undo recording, so nothing marked the graph or its scene dirty: a graph embedded in
		/// a scene (CodeCraftScenario) then lost renames, colours and resizes on save. Skipped while the view is being built.
		/// </summary>
		public void MarkDirty()
		{
			if (!initialized || owner?.graph == null || Application.isPlaying) return;
			EditorUtility.SetDirty(owner.graph);
			var sceneObject = owner.graph.linkedSceneObject;
			if (sceneObject != null && sceneObject.gameObject.scene.IsValid())
				EditorSceneManager.MarkSceneDirty(sceneObject.gameObject.scene);
		}
	}
}