using System;
using Unity.Entities;

namespace HSE.Core.Components
{
    public struct EventedObjectElement : IBufferElementData, IEquatable<EventedObjectElement>
    {
        public int InstanceID;
        public EventedObjectElement(int instanceID) => InstanceID = instanceID;

        public bool Equals(EventedObjectElement other) => other.InstanceID == this.InstanceID;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + InstanceID.GetHashCode();
                return hash;
            }
        }
    }
}
