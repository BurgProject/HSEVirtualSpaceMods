using System;
using Unity.Entities;
using UnityEngine;

namespace HSE.Core.Components
{
    public sealed class UiCanvasPrefab : IComponentData
    {
        public GameObject Prefab;
        public PointerHintSettings HintSettings = PointerHintSettings.Default;
    }

    /// <summary>
    /// Room-wide pointer-hint rules, set on the UI Canvas node. A hint is anchored at its object and shown while the
    /// object is within <see cref="MaxDistance"/> of the camera and within <see cref="ScreenCenterRadius"/> of the
    /// screen centre; it shrinks toward <see cref="FarScale"/> with distance and with distance from the centre, and the
    /// hint nearest the centre is multiplied by <see cref="FocusScale"/>.
    /// </summary>
    [Serializable]
    public struct PointerHintSettings
    {
        [Tooltip("Hints farther than this from the camera (metres) are hidden.")]
        public float MaxDistance;
        [Tooltip("Hints farther than this from the screen centre are hidden, as a fraction of the screen height (0.5 = top edge).")]
        public float ScreenCenterRadius;
        [Tooltip("Up to this distance (metres) a hint keeps Near Scale.")]
        public float NearDistance;
        [Tooltip("Scale of a hint that is close and at the screen centre.")]
        public float NearScale;
        [Tooltip("Scale at Max Distance or at the edge of Screen Center Radius.")]
        public float FarScale;
        [Tooltip("Extra multiplier for the hint closest to the screen centre.")]
        public float FocusScale;

        public static PointerHintSettings Default => new PointerHintSettings
        {
            MaxDistance = 15f,
            ScreenCenterRadius = 0.35f,
            NearDistance = 2f,
            NearScale = 1f,
            FarScale = 0.5f,
            FocusScale = 1.2f,
        };
    }
}
