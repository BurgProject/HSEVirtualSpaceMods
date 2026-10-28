using System;
using GraphProcessor;
using HSE.Core.Components;
using HSE.Mechanics.Components;
using UnityEngine;

namespace HSE.CodeCraft
{
    [Serializable, NodeMenuItem("Data/PickUp")]
    public sealed class PickUpDataNode : MechanicsDataNode<PickUpSettings>
    {
        [Input(name = "Bounds")] public CraftInstance Source;
        [Input, ShowAsDrawer] public float SurfaceOffset = .005f;

        protected override bool Process()
        {
            bool singleWorld = false;
            foreach (var edge in GetPort(nameof(Value), null).GetEdges())
            {
                if (edge.inputNode is AddComponentNode command && command.World != HSEComponentWorld.Both)
                {
                    singleWorld = true;
                    break;
                }
            }
            AddStateMessage("PickUp needs AddComponent in Both worlds for server pickup and client previews.",
                singleWorld, NodeMessageType.Warning);
            return base.Process();
        }

        protected override ComponentDataValue[] CreateComponents()
        {
            if (Source?.CraftObj == null)
            {
                AddMessage("Connect Bounds to the CraftObject being picked up.", NodeMessageType.Error);
                return null;
            }
            if (!float.IsFinite(SurfaceOffset) || SurfaceOffset < 0f)
            {
                AddMessage("Surface offset must be finite and non-negative.", NodeMessageType.Error);
                return null;
            }
            if (!TryGetBounds(Source.CraftObj.transform, out var bounds))
            {
                AddMessage("PickUp requires a renderer on the object or its children.", NodeMessageType.Error);
                return null;
            }

            Source.CraftObj.AddPhysics();
            return new ComponentDataValue[]
            {
                new StateComponent<GrabbableTag>(),
                new StateComponent<RaycastData>(),
                new ComponentValue<PickUpSettings>(new PickUpSettings
                {
                    BoundsCenter = bounds.center,
                    BoundsExtents = bounds.extents,
                    SurfaceOffset = SurfaceOffset
                })
            };
        }

        private static bool TryGetBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var local = renderer.localBounds;
                var matrix = root.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var signs = new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, signs));
                    if (!found) bounds = new Bounds(point, Vector3.zero);
                    else bounds.Encapsulate(point);
                    found = true;
                }
            }
            return found;
        }
    }
}
