using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphProcessor
{
    /// <summary>Read-only preview of the last computed outputs; opening Debug never processes the graph.</summary>
    public sealed class NodeOutputDebugView : VisualElement
    {
        private const int PreviewLimit = 64;
        private readonly BaseNode _node;
        private readonly FieldInfo[] _outputs;
        private readonly Label _values;

        public NodeOutputDebugView(BaseNode node)
        {
            _node = node;
            _outputs = node.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.IsDefined(typeof(OutputAttribute), true)).ToArray();
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 240;
            scroll.style.width = 280;
            _values = new Label { name = "debug-output-values", enableRichText = false };
            _values.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(_values);
            Add(scroll);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _node.onProcessed += Refresh;
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => _node.onProcessed -= Refresh);
            Refresh();
        }

        public void Refresh()
        {
            if (!_node.debug) return;
            var text = new StringBuilder();
            foreach (var field in _outputs)
            {
                object value = field.GetValue(_node);
                if (text.Length > 0) text.AppendLine();
                text.Append(field.Name);
                if (!(value is Array array))
                {
                    text.Append(": ").AppendLine(Format(value));
                    continue;
                }

                text.Append(" [").Append(array.Length).AppendLine("]");
                int index = 0;
                foreach (object item in array)
                {
                    if (index == PreviewLimit) break;
                    text.Append('[').Append(index++).Append("] ").AppendLine(Format(item));
                }
                if (array.Length > PreviewLimit)
                    text.Append("... ").Append(array.Length - PreviewLimit).AppendLine(" more items");
            }
            _values.text = text.ToString();
        }

        private static string Format(object value)
        {
            if (value == null) return "null";
            if (value is UnityEngine.Object reference)
                return reference == null ? "null" : reference.name + " (" + reference.GetType().Name + ")";
            if (value is Vector3 vector) return vector.ToString("G9", CultureInfo.InvariantCulture);
            if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
            return value.ToString();
        }
    }
}
