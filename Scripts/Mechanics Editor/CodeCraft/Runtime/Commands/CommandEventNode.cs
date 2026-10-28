using System;
using System.Collections.Generic;
using GraphProcessor;
using HSE.Core.Components;
using UnityEngine;

namespace HSE.CodeCraft
{
    public abstract class CommandEventNode : BaseHSENode
    {
        [Input] public CraftInstance Source;
        [Output(allowMultiple = false)] public ConditionalLink Exec;
        public abstract ActionType EventType { get; }

        public override Color color => Color.yellow;
 
        protected override bool Process()
        {
            bool valid = Source != null && Source.InstanceID != 0;
            AddStateMessage("Connect Source to CraftObject.", !valid);
            Exec = valid ? new ConditionalLink { OverrideEvent = new ActionOverrideEvent(EventType, 0, Source.InstanceID) } : default;
            return valid;
        }

        internal virtual void Prepare(GraphBaker baker) { }
    }

    [Serializable, NodeMenuItem("Events/Initialization")]
    public sealed class InitializationEventNode : CommandEventNode { public override ActionType EventType => ActionType.Initialization; }

    [Serializable, NodeMenuItem("Events/OnStart")]
    public sealed class StartEventNode : CommandEventNode { public override ActionType EventType => ActionType.OnStart; }

    [Serializable, NodeMenuItem("Events/Raycast")]
    public sealed class RaycastEventNode : CommandEventNode, IPlusMinusButtonNode
    {
        [SerializeField, HideInInspector] private uint countClick = 1;
        public uint ClickCount => countClick;
        public override ActionType EventType => ActionType.Raycast;

        public void OnClickPlusButton()
        {
            if (countClick < uint.MaxValue) countClick++;
        }

        public void OnClickMinusButton()
        {
            if (countClick > 1) countClick--;
        }

        [CustomPortBehavior(nameof(Exec))]
        public IEnumerable<PortData> ClickPorts(List<SerializableEdge> edges)
        {
            for (uint i = 0; i < countClick; i++)
                yield return new PortData
                {
                    displayName = "Click " + (i + 1),
                    displayType = typeof(ConditionalLink),
                    identifier = i == 0 ? null : i.ToString(),
                    acceptMultipleEdges = false
                };
        }

        [CustomPortOutput(nameof(Exec), typeof(ConditionalLink))]
        public void PushClicks(List<SerializableEdge> edges)
        {
            foreach (var edge in edges)
            {
                string identifier = edge.outputPort.portData.identifier;
                uint index = string.IsNullOrEmpty(identifier) ? 0 : uint.Parse(identifier);
                edge.passThroughBuffer = new ConditionalLink
                {
                    OverrideEvent = new ActionOverrideEvent(ActionType.Raycast, index, Source?.InstanceID ?? 0)
                };
            }
        }

        protected override bool Process()
        {
            if (!base.Process()) return false;
            if (Source.CraftObj) Source.CraftObj.AddPhysics();
            return true;
        }

        internal override void Prepare(GraphBaker baker)
        {
            var entity = baker.GetEntity(Source.InstanceID);
            var action = new ActionOverrideEvent(ActionType.OnStart, 0, Source.InstanceID);
            baker.AddComponent<RaycastData>(entity, action, HSEComponentWorld.Server);
            baker.SetEnabled<RaycastData>(entity, action, false, HSEComponentWorld.Server);
            baker.AddComponent(entity, action, new RayInteractable { CountClick = countClick }, HSEComponentWorld.Server);
        }
    }
}
