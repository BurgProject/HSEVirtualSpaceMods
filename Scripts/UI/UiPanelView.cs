using UnityEngine;
using UnityEngine.UI;

namespace HSE.UI
{
    /// <summary>Receives graph state without referencing ECS. Layout and resources belong to the prefab.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiPanelView : MonoBehaviour
    {
        private RectTransform _placementRoot;

        private Canvas _canvas;
        private UiTextBinding[] _textBindings;
        private readonly Vector3[] _corners = new Vector3[4];

        public bool Validate(out string error)
        {
            error = !(transform is RectTransform) || GetComponentsInChildren<Canvas>(true).Length != 0
                ? "Panel and hint prefabs require a RectTransform root without a Canvas."
                : null;
            return error == null;
        }

        public void Initialize(bool pointer)
        {
            _placementRoot = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            _textBindings = GetComponentsInChildren<UiTextBinding>(true);
            if (!pointer) return;

            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        public void ApplyText(string text)
        {
            foreach (var binding in _textBindings)
                if (binding != null) binding.Apply(text);
        }

        /// <summary>Uniform scale of the placed panel (pointer hints shrink with distance); 1 = as authored.</summary>
        public void SetScale(float scale)
        {
            if (_placementRoot == null || !float.IsFinite(scale) || scale <= 0) return;
            var value = new Vector3(scale, scale, 1f);
            if (_placementRoot.localScale != value) _placementRoot.localScale = value;
        }

        /// <summary>Projects a world marker into normalized screen coordinates; hides points outside the camera frustum.</summary>
        public static bool TryProjectWorld(Camera camera, Vector3 position, out Vector2 screenPosition)
        {
            screenPosition = default;
            if (camera == null || !camera.isActiveAndEnabled) return false;
            var point = camera.WorldToViewportPoint(position);
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z) ||
                point.z < camera.nearClipPlane || point.z > camera.farClipPlane ||
                point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return false;
            var pixel = camera.ViewportToScreenPoint(point);
            screenPosition = new Vector2(pixel.x / Mathf.Max(1, Screen.width), pixel.y / Mathf.Max(1, Screen.height));
            return true;
        }

        /// <summary>Converts normalized screen coordinates to the authored Canvas space without rebuilding its layout.</summary>
        public bool ApplyPlacement(Vector2 anchor, Vector2 offset, bool pointer)
        {
            if (_canvas == null || _placementRoot == null) return false;
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                if (_canvas.worldCamera == null) _canvas.worldCamera = Camera.main;
                if (_canvas.worldCamera == null) return false;
            }
            var root = (RectTransform)_canvas.transform;
            var size = _placementRoot.rect.size;
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            var screenPoint = Vector2.Scale(anchor, new Vector2(Screen.width, Screen.height));
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, camera, out var local)) return false;

            _placementRoot.anchorMin = _placementRoot.anchorMax = root.pivot;
            _placementRoot.sizeDelta = size;
            if (pointer) _placementRoot.pivot = new Vector2(0, 1);
            _placementRoot.anchoredPosition = local + offset;
            if (!pointer) return true;

            _placementRoot.GetWorldCorners(_corners);
            var min = (Vector2)root.InverseTransformPoint(_corners[0]);
            var max = min;
            for (int i = 1; i < _corners.Length; i++)
            {
                var point = (Vector2)root.InverseTransformPoint(_corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            var bounds = root.rect;
            var correction = new Vector2(
                Mathf.Max(0, bounds.xMin - min.x) - Mathf.Max(0, max.x - bounds.xMax),
                Mathf.Max(0, bounds.yMin - min.y) - Mathf.Max(0, max.y - bounds.yMax));
            _placementRoot.anchoredPosition += correction;
            return true;
        }
    }
}
