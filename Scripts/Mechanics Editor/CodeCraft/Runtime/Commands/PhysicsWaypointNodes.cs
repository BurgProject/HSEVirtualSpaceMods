using System;
using GraphProcessor;
using System.Collections.Generic;
using HSE.Mechanics.Components;
using UnityEngine;

namespace HSE.CodeCraft
{
    [Serializable, NodeMenuItem("Data/Physics/Waypoint Follower")]
    public sealed class PhysicsWaypointFollowerNode : MechanicsDataNode<PhysicsWaypointFollower>
    {
        [Input, ShowAsDrawer] public float Speed = 2;
        public float ReachedDistance = .01f;
        public bool Loop;

        protected override ComponentDataValue[] CreateComponents()
        {
            if (!float.IsFinite(Speed) || Speed < 0 || !float.IsFinite(ReachedDistance) || ReachedDistance < 0)
            {
                AddMessage("Speed and reached distance must be finite and non-negative.", NodeMessageType.Error);
                return null;
            }
            return new ComponentDataValue[] { new ComponentValue<PhysicsWaypointFollower>(new PhysicsWaypointFollower
            {
                Speed = Speed, ReachedDistance = ReachedDistance, Loop = Loop
            }) };
        }
    }

    [Serializable, NodeMenuItem("Data/Physics/Waypoint Buffer")]
    public sealed class PhysicsWaypointBufferNode : ComponentSetNode<BufferDataValue>, ICommandDataNode
    {
        [Input(allowMultiple = false), NonSerialized] public Vector3[] Points = Array.Empty<Vector3>();
        public Type ComponentType => typeof(PhysicsWaypointElement);

        [CustomPortInput(nameof(Points), typeof(Vector3[]))]
        public void PullPoints(List<SerializableEdge> edges)
        {
            var values = edges.Count == 0 ? null : edges[0].passThroughBuffer as Vector3[];
            Points = values == null ? Array.Empty<Vector3>() : (Vector3[])values.Clone();
        }

        protected override BufferDataValue[] CreateComponents()
        {
            if (GetPort(nameof(Points), null)?.GetEdges().Count != 1)
            {
                Points = Array.Empty<Vector3>();
                AddMessage("Connect a Vector3 array to Points.", NodeMessageType.Error);
                return null;
            }
            var points = new PhysicsWaypointElement[Points?.Length ?? 0];
            for (int i = 0; i < points.Length; i++)
            {
                var point = Points[i];
                if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                {
                    AddMessage("Waypoint positions must be finite world coordinates.", NodeMessageType.Error);
                    return null;
                }
                points[i] = new PhysicsWaypointElement { Position = point };
            }
            return new BufferDataValue[] { new BufferValue<PhysicsWaypointElement>(points) };
        }
    }
}
