using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine;

namespace GraphProcessor
{
	public class ExposedParameterPropertyView : VisualElement
	{
		protected BaseGraphView baseGraphView;

		public ExposedParameter parameter { get; private set; }

		public Toggle     hideInInspector { get; private set; }

		public ExposedParameterPropertyView(BaseGraphView graphView, ExposedParameter param)
		{
			baseGraphView = graphView;
			parameter      = param;

			var field = graphView.exposedParameterFactory.GetParameterSettingsField(param, (newValue) => {
				param.settings = newValue as ExposedParameter.Settings;
			});
            var val = graphView.exposedParameterFactory.GetParameterValueField(param, (newValue) => {
                param.value = newValue;
				baseGraphView.graph.NotifyExposedParameterChanged(param);
            });

            Add(field);
			Add(val);
		}
	}
} 