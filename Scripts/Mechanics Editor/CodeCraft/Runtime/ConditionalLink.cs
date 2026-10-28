
using HSE.Mechanics.Components;
using HSE.Core.Components;
using System;

namespace HSE.CodeCraft
{
    [Serializable]
    public struct ConditionalLink : IEquatable<ConditionalLink>
    {
        public ActionOverrideEvent OverrideEvent;
        public GraphOpcode Opcode;

        public bool Equals(ConditionalLink other) =>
            other.OverrideEvent.Equals(this.OverrideEvent);
    }
}
