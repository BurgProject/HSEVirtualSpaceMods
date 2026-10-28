using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

namespace GraphProcessor
{
	public class NodeBadgeView : IconBadge
	{
		Label		label;
		Texture		icon;
		bool		isCustom;

		public Color Color { get; private set; }
        public NodeMessageImportance MessageImportance { get; private set; }

        public NodeBadgeView(string message, NodeMessageType messageType, NodeMessageImportance importance)
		{
			switch (messageType)
			{
				case NodeMessageType.Warning:
					CreateCustom(message, EditorGUIUtility.IconContent("Collab.Warning").image, Color.yellow, importance);
					break ;
				case NodeMessageType.Error:	
					CreateCustom(message, EditorGUIUtility.IconContent("Collab.Warning").image, Color.red, importance);
					break ;
				case NodeMessageType.Info:
					CreateCustom(message, EditorGUIUtility.IconContent("console.infoicon").image, Color.white, importance);
					break ;
				default:
				case NodeMessageType.None:
					CreateCustom(message, null, Color.grey, NodeMessageImportance.Low);
					break ;
			}
		}
        public NodeBadgeView(string message, Texture icon, Color color, NodeMessageImportance importance)
        {
            CreateCustom(message, icon, color, importance);
        }

        public static int GetColorPriority(StyleColor color)
        {
            if (color == Color.grey) return 1; 
            if (color == Color.yellow) return 2; 
            if (color == Color.red) return 3; 
            return 0; 
        }
        public static int GetMessagePriority(NodeMessageImportance importance)
        {
            if (importance == NodeMessageImportance.Low) return 1;
            if (importance == NodeMessageImportance.Medium) return 2;
            if (importance == NodeMessageImportance.High) return 3;
            return 0;
        }

		void CreateCustom(string message, Texture icon, Color color, NodeMessageImportance importance)
		{
			badgeText = message;
			this.Color = color;
			this.MessageImportance = importance;

			var image = this.Q< Image >("icon");
			image.image = icon;
			image.style.backgroundColor = color;
			style.color = color;
			// This will set a class name containing the hash code of the string
			// We use this little trick to retrieve the label once it is added to the graph
			visualStyle = badgeText.GetHashCode().ToString();
		}

		protected override void ExecuteDefaultAction(EventBase evt)
		{
			// When the mouse enter the icon, this will add the label to the hierarchy
			base.ExecuteDefaultAction(evt);

            if (evt.eventTypeId == MouseEnterEvent.TypeId())
			{
				// And then we can fetch it here:
				GraphView gv = GetFirstAncestorOfType<GraphView>();
				var label = gv.Q<Label>(classes: new string[]{"icon-badge__text--" + badgeText.GetHashCode()});
				if (label != null)
					label.style.color = Color;
			}
		}
	}
}