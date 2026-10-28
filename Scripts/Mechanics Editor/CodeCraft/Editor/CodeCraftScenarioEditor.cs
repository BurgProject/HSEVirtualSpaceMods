using UnityEditor;
using UnityEngine;

namespace HSE.CodeCraft.Editor
{
    [CustomEditor(typeof(CodeCraftScenario))]
    public class CodeCraftScenarioEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var obj = (CodeCraftScenario)target;
            if (GUILayout.Button("Edit craft"))
                CustomContextMenuGraphWindow.OpenWithGraph(obj.GraphInstance);
        }
    }
}
