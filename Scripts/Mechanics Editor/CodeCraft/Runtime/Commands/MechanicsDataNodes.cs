using GraphProcessor;
using HSE.Mechanics.Components;
using HSE.Mechanics.Components.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace HSE.CodeCraft
{
    public abstract class MechanicsDataNode<T> : ComponentSetNode<ComponentDataValue>, ICommandDataNode where T : unmanaged, IComponentData
    {
        public override Color color => Color.royalBlue;

        public override string EditorNodeTitleIconName => "ComponentNode";
        public Type ComponentType => typeof(T);
    }

    [Serializable, NodeMenuItem("Data/RotateAroundPoint")]
    public sealed class RotateAroundPointDataNode : MechanicsDataNode<RotateAroundPoint>
    {
        [Input, ShowAsDrawer] public Vector3 CenterPoint;
        [Input, ShowAsDrawer] public Vector3 Axis = Vector3.up;
        [Input, ShowAsDrawer] public float Radius;
        [Input, ShowAsDrawer] public float Speed;
        [Input, ShowAsDrawer] public float Angle;

        protected override ComponentDataValue[] CreateComponents() => new ComponentDataValue[]
        {
            new ComponentValue<RotateAroundPoint>(new RotateAroundPoint
            {
                CenterPoint = CenterPoint, 
                Axis = Axis, 
                Radius = Radius, 
                Speed = Speed, 
                Angle = Angle
            })
        };
    }

    [Serializable, NodeMenuItem("Data/RotateToEuler")]
    public sealed class RotateToEulerDataNode : MechanicsDataNode<RotateToEuler>
    {
        [Input, ShowAsDrawer] public Vector3 TargetEuler;
        [Input, ShowAsDrawer] public Vector3 Threshold = new Vector3(.001f, .001f, .001f);
        [Input, ShowAsDrawer] public float Speed;

        protected override ComponentDataValue[] CreateComponents() => new ComponentDataValue[]
        {
            new StateComponent<CurrentEulerData>(),
            new ComponentValue<RotateToEuler>(new RotateToEuler
            {
                TargetEuler = TargetEuler, 
                Threshold = Threshold,
                Speed = Speed
            })
        };
    }

    [Serializable, NodeMenuItem("Data/RotateToPosition")]
    public sealed class RotateToPositionDataNode : MechanicsDataNode<RotateToPosition>
    {
        [Input, ShowAsDrawer] public Vector3 TargetPosition;
        [Input, ShowAsDrawer] public float RotationSpeed;
        public bool SmoothRotation;
        public bool StopOnCompletion = true;

        protected override ComponentDataValue[] CreateComponents() => new ComponentDataValue[]
        {
            new ComponentValue<RotateToPosition>(new RotateToPosition
            {
                TargetPos = TargetPosition,
                RotationSpeed = RotationSpeed,
                SmoothRotation = SmoothRotation, 
                ShoodStopOnRotationComplete = StopOnCompletion
            })
        };
    }

    [Serializable, NodeMenuItem("Data/MoveAlongVector")]
    public sealed class MoveAlongVectorDataNode : MechanicsDataNode<MoveAlongVectorData>
    {
        [Input, ShowAsDrawer] public Vector3 Direction;
        [Input, ShowAsDrawer] public float Speed;
        [Input, ShowAsDrawer] public float MaxDistance;

        protected override ComponentDataValue[] CreateComponents() => new ComponentDataValue[]
        {
            new ComponentValue<MoveAlongVectorData>(new MoveAlongVectorData
            {
                Direction = Direction, Speed = Speed, MaxDistance = MaxDistance
            })
        };
    }

    [Serializable, NodeMenuItem("Data/WaypointFollower")]
    public sealed class WaypointFollowerDataNode : MechanicsDataNode<WaypointFollower>
    {
        [Input, ShowAsDrawer] public float MovementSpeed;
        [Input, ShowAsDrawer] public float WaypointReachedDistance;
        public bool StopOnEnd;

        protected override ComponentDataValue[] CreateComponents()
        {
            var components = new List<ComponentDataValue>
            {
                new StateComponent<WaypointIndex>(),
                new ComponentValue<WaypointFollower>(new WaypointFollower
                {
                    MovementSpeed = MovementSpeed, WaypointReachedDistance = WaypointReachedDistance,
                    ShoodStopOnEnd = StopOnEnd
                })
            };
            return components.ToArray();
        }

    }

    [Serializable, NodeMenuItem("Data/WaypointBuffer")]
    public sealed class WaypointBufferDataNode : ComponentSetNode<BufferDataValue>, ICommandDataNode
    {
        [Input(allowMultiple = false), NonSerialized] public Vector3[] Waypoints = Array.Empty<Vector3>();
        public Type ComponentType => typeof(WaypointBuffer);

        [CustomPortInput(nameof(Waypoints), typeof(Vector3[]))]
        public void PullWaypoints(List<SerializableEdge> edges)
        {
            var values = edges.Count == 0 ? null : edges[0].passThroughBuffer as Vector3[];
            Waypoints = values == null ? Array.Empty<Vector3>() : (Vector3[])values.Clone();
        }

        protected override BufferDataValue[] CreateComponents()
        {
            if (GetPort(nameof(Waypoints), null)?.GetEdges().Count != 1)
            {
                Waypoints = Array.Empty<Vector3>();
                AddMessage("Connect a Vector3 array to Waypoints.", NodeMessageType.Error);
                return null;
            }
            if (Waypoints == null || Waypoints.Length == 0)
            {
                AddMessage("Specify at least one waypoint.", NodeMessageType.Error);
                return null;
            }

            var elements = new WaypointBuffer[Waypoints.Length];
            for (int i = 0; i < elements.Length; i++) elements[i].Position = Waypoints[i];
            return new BufferDataValue[] { new BufferValue<WaypointBuffer>(elements) };
        }
    }

    [Serializable, NodeMenuItem("Data/Network/MaterialChangerCounter")]
    public sealed class MaterialChangerCounterDataNode : MechanicsDataNode<MaterialSwitcherCounter>
    {
        // Shown as "Index" (1-based: 1 = first material, 0 = none); the field keeps its name so saved graphs and edges load
        [Input("Index"), ShowAsDrawer, Tooltip("1-based material/texture index; 0 keeps the original material.")]
        public uint Count;
        [Input, ShowAsDrawer] public uint MaxCount;

        protected override ComponentDataValue[] CreateComponents() => new ComponentDataValue[]
        {
            new ComponentValue<MaterialSwitcherCounter>(new MaterialSwitcherCounter { count = Count, maxCount = MaxCount })
        };
    }
}
