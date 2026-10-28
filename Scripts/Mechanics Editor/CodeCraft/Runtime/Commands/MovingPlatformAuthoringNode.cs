using System;
using GraphProcessor;
using HSE.Core.Components;
using HSE.Core.Components.Network;
using Unity.Physics;
using Unity.Physics.Authoring;

namespace HSE.CodeCraft
{
    [Serializable, NodeMenuItem("Hybrid/Physics/Moving Platform")]
    public sealed class MovingPlatformAuthoringNode : AuthoringAdapterNode<PhysicsBodyAuthoring>
    {
        protected override bool ValidateSettings()
        {
            if (!Target.IsPhysics || Target.Source.Mode != GhostType.Predicted)
                return Fail("MovingPlatform requires an IsPhysics CraftObject with Predicted ghost mode.");
            if (!TargetObject.TryGetComponent<PhysicsShapeAuthoring>(out var shape) || !shape.enabled ||
                shape.CollisionResponse == CollisionResponsePolicy.RaiseTriggerEvents || shape.CollisionResponse == CollisionResponsePolicy.None)
                return Fail("MovingPlatform requires an enabled solid PhysicsShapeAuthoring.");
            return true;
        }

        protected override bool Matches(PhysicsBodyAuthoring authoring) => authoring.MotionType == BodyMotionType.Kinematic;

        protected override void CopyTo(PhysicsBodyAuthoring authoring) => authoring.MotionType = BodyMotionType.Kinematic;

        protected override void Craft(GraphBaker baker)
        {
            baker.AddComponent<MovingPlatformTag>(baker.GetEntity(Target.InstanceID),
                new ActionOverrideEvent(ActionType.Initialization, 0, Target.InstanceID), HSEComponentWorld.Both);
        }
    }
}
