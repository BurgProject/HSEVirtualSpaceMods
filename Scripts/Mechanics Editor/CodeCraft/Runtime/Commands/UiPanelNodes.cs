using System;
using GraphProcessor;
using HSE.Mechanics.Authorings;
using HSE.Mechanics.Components;
using HSE.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HSE.CodeCraft
{
    [Serializable, NodeMenuItem("Hybrid/Client/UI/Canvas")]
    public sealed class UiCanvasAuthoringNode : AuthoringAdapterNode<UiCanvasAuthoring>
    {
        public GameObject Prefab;

        protected override bool ValidateSettings()
        {
            if (Prefab == null) return true;
            return !Prefab.scene.IsValid() && UiCanvasUtility.Validate(Prefab) ||
                Fail("Assign a prefab with one enabled Screen Space Canvas on its root, or leave it empty.");
        }

        protected override bool Matches(UiCanvasAuthoring authoring) => authoring.Prefab == Prefab;

        protected override void CopyTo(UiCanvasAuthoring authoring) => authoring.Prefab = Prefab;
    }

    [Serializable, NodeMenuItem("Hybrid/Client/UI/Panel Prefab")]
    public sealed class UiPanelAuthoringNode : AuthoringAdapterNode<UiPanelAuthoring>
    {
        public GameObject Prefab;

        protected override bool ValidateSettings()
        {
            if (Prefab == null || Prefab.scene.IsValid() || !Prefab.TryGetComponent<UiPanelView>(out var view))
                return Fail("Assign a UI prefab asset with UiPanelView on its root.");
            return view.Validate(out var error) || Fail(error);
        }

        protected override bool Matches(UiPanelAuthoring authoring) => authoring.Prefab == Prefab;

        protected override void CopyTo(UiPanelAuthoring authoring) => authoring.Prefab = Prefab;
    }

    [Serializable, NodeMenuItem("Data/Client/UI/Screen Panel")]
    public sealed class UiPanelDataNode : MechanicsDataNode<UiPanelState>
    {
        [Input, ShowAsDrawer] public string Text = "";
        [Tooltip("World-space offset from the entity position, in metres.")]
        public Vector3 WorldOffset = new Vector3(0, 2, 0);
        [UnityEngine.Serialization.FormerlySerializedAs("Offset")]
        [Tooltip("Additional offset in Canvas units after camera projection.")]
        public Vector2 ScreenOffset;

        protected override ComponentDataValue[] CreateComponents()
        {
            FixedString128Bytes text = default;
            if (text.CopyFromTruncated(Text ?? "") != CopyError.None)
            {
                AddMessage("Panel text exceeds FixedString128Bytes capacity.", NodeMessageType.Error);
                return null;
            }
            return new ComponentDataValue[] { new ComponentValue<UiPanelState>(new UiPanelState
            {
                Text = text, WorldOffset = WorldOffset, ScreenOffset = ScreenOffset
            }) };
        }
    }

    [Serializable, NodeMenuItem("Data/Client/UI/Pointer Hint")]
    public sealed class PointerHintDataNode : MechanicsDataNode<PointerHintState>
    {
        [Input, ShowAsDrawer] public string Text = "";
        public Vector2 Offset = new Vector2(24, -24);

        protected override ComponentDataValue[] CreateComponents()
        {
            FixedString128Bytes text = default;
            if (text.CopyFromTruncated(Text ?? "") != CopyError.None)
            {
                AddMessage("Hint text exceeds FixedString128Bytes capacity.", NodeMessageType.Error);
                return null;
            }
            return new ComponentDataValue[] { new ComponentValue<PointerHintState>(new PointerHintState
            {
                Text = text, Offset = Offset
            }) };
        }
    }

    [Serializable, NodeMenuItem("Hybrid/Client/UI/Pointer Hint Prefab")]
    public sealed class PointerHintAuthoringNode : AuthoringAdapterNode<PointerHintAuthoring>
    {
        public GameObject Prefab;

        protected override bool ValidateSettings()
        {
            if (Prefab == null || Prefab.scene.IsValid() || !Prefab.TryGetComponent<UiPanelView>(out var view))
                return Fail("Assign a UI prefab asset with UiPanelView on its root.");
            return view.Validate(out var error) || Fail(error);
        }

        protected override bool Matches(PointerHintAuthoring authoring) => authoring.Prefab == Prefab;

        protected override void CopyTo(PointerHintAuthoring authoring) => authoring.Prefab = Prefab;
    }
}
