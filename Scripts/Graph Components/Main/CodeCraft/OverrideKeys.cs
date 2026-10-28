
using System;
using HSE.Core.Components;

namespace HSE.Mechanics.Components
{
    public interface IOverride { }

    [Serializable]
    public struct OverrideKeyState: IOverride, IEquatable<OverrideKeyState>
    {
        public uint TypeId;
        public bool ActivState;

        public bool Equals(OverrideKeyState other) =>
               TypeId == other.TypeId &&
            ActivState == other.ActivState;
        public override string ToString()
        {
            return "TypeId: " + TypeId + ", state key: " + ActivState;
        }
    }
    public struct OverrideKey : IOverride, IEquatable<OverrideKey>
    {
        public uint TypeId;
        public uint Version;
        public int DataOffset;
        public int DataSize;
        public int ElementCount;

        public bool Equals(OverrideKey other) =>
                  TypeId == other.TypeId &&
                 Version == other.Version &&
              DataOffset == other.DataOffset &&
                DataSize == other.DataSize &&
            ElementCount == other.ElementCount;
    }

    public struct OverrideStateElement : IEquatable<OverrideStateElement>
    {
        public ActionOverrideEvent Event;
        public OverrideKeyState OverrideState;

        public bool Equals(OverrideStateElement other) =>
            other.Event.Equals(Event);
    }
    public struct OverrideKeyElement : IEquatable<OverrideKeyElement>
    {
        public ActionOverrideEvent Event;
        public OverrideKey OverrideKey;

        public bool Equals(OverrideKeyElement other) =>
            other.Event.Equals(Event);
    }
}
