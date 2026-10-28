using UnityEngine;
using UnityEngine.UI;

namespace HSE.UI
{
    public static class UiCanvasUtility
    {
        public static bool Validate(GameObject prefab) => prefab != null &&
            prefab.TryGetComponent<Canvas>(out var canvas) && canvas.enabled &&
            canvas.renderMode != RenderMode.WorldSpace && prefab.GetComponentsInChildren<Canvas>(true).Length == 1;

        public static Canvas CreateDefault(Transform parent)
        {
            var host = new GameObject("Location UI Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            host.transform.SetParent(parent, false);
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }
    }
}
