using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HSE.CodeCraft.Editor
{
    public static class VEAnimationExtensions
    {
        public static IVisualElementScheduledItem StartGradientBackgroundColorTransition(this VisualElement element, Color[] colors,float duration = .99f, long intervalMS = 50)
        {
            double lastTimeSinceStartup = 0f;
            double editorDeltaTime;
            float time = 0;
            int colorCurrentIndx = 0;
            int colorTargetIndx = 1;
            return element.schedule.Execute(() =>{
                if (lastTimeSinceStartup == 0f)
                    lastTimeSinceStartup = EditorApplication.timeSinceStartup;

                editorDeltaTime = EditorApplication.timeSinceStartup - lastTimeSinceStartup;
                lastTimeSinceStartup = EditorApplication.timeSinceStartup;
                time += (float)editorDeltaTime;
                element.style.backgroundColor = Color.Lerp(colors[colorCurrentIndx], colors[colorTargetIndx], time);
                if (time >= duration)
                {
                    time = 0f;
                    colorCurrentIndx = colorTargetIndx;
                    colorTargetIndx++;
                    if (colorTargetIndx >= colors.Length)
                        colorTargetIndx = 0;
                }
            }).Every(intervalMS);
        }
        public static IVisualElementScheduledItem StartGradientBackgroundColorTransition(this VisualElement element, List<Color> colors, float duration = .99f, long intervalMS = 50)
        {
            double lastTimeSinceStartup = 0f;
            double editorDeltaTime;
            float time = 0;
            int colorCurrentIndx = 0;
            int colorTargetIndx = 1;
            return element.schedule.Execute(() => {
              
                if (colors.Count == 0) return;
                if (lastTimeSinceStartup == 0f)
                    lastTimeSinceStartup = EditorApplication.timeSinceStartup;

                editorDeltaTime = EditorApplication.timeSinceStartup - lastTimeSinceStartup;
                lastTimeSinceStartup = EditorApplication.timeSinceStartup;
                time += (float)editorDeltaTime;
                element.style.backgroundColor = Color.Lerp(colors[colorCurrentIndx], colors[colorTargetIndx], time);
                if (time >= duration)
                {
                    time = 0f; 
                    colorCurrentIndx = colorTargetIndx;
                    colorTargetIndx++;
                    if (colorTargetIndx >= colors.Count)
                        colorTargetIndx = 0;
                }
            }).Every(intervalMS);
        }
        public static IVisualElementScheduledItem StartGradientColorTransition(this VisualElement element, List<Color> colors, float duration = .99f, long intervalMS = 50)
        {
            double lastTimeSinceStartup = 0f;
            double editorDeltaTime;
            float time = 0;
            int colorCurrentIndx = 0;
            int colorTargetIndx = 1;
            return element.schedule.Execute(() => {

                if (colors.Count == 0) return;
                if (lastTimeSinceStartup == 0f)
                    lastTimeSinceStartup = EditorApplication.timeSinceStartup;

                editorDeltaTime = EditorApplication.timeSinceStartup - lastTimeSinceStartup;
                lastTimeSinceStartup = EditorApplication.timeSinceStartup;
                time += (float)editorDeltaTime;
                element.style.color = Color.Lerp(colors[colorCurrentIndx], colors[colorTargetIndx], time);
                if (time >= duration)
                {
                    time = 0f;
                    colorCurrentIndx = colorTargetIndx;
                    colorTargetIndx++;
                    if (colorTargetIndx >= colors.Count)
                        colorTargetIndx = 0;
                }
            }).Every(intervalMS);
        }
    }
}
