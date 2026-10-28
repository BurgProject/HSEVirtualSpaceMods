using System;
using Unity.Entities;

namespace HSE.Core.Components
{
    public enum ActionType : byte
    {
        None = 0,
        Initialization = 1,
        OnStart = 2,
        Raycast = 3,
        OnTriggerEnter = 4,
        OnTriggerStay = 5,
        OnTriggerExit = 6,
        // Cursor (hover) events; ActionIndex carries the CursorTriggerMode (0 server, 1 client)
        OnCursorEnter = 7,
        OnCursorStay = 8,
        OnCursorExit = 9
    }
    [Serializable]
    public struct ActionOverrideEvent : IBufferElementData, IEquatable<ActionOverrideEvent>
    {
        public ActionType ActionType;
        public uint ActionIndex;
        public int InstanceID;

        public ActionOverrideEvent(ActionType type, uint actionIndex, int instanceID)
        {
            ActionType = type;
            ActionIndex = actionIndex;
            InstanceID = instanceID;
        }
        public bool Equals(ActionOverrideEvent other) =>
             ActionType == other.ActionType &&
            ActionIndex == other.ActionIndex &&
             InstanceID == other.InstanceID;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (int)ActionType;
                hash = hash * 23 + ActionIndex.GetHashCode();
                hash = hash * 23 + InstanceID.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return "Type:" + ActionType + " :imdex " + ActionIndex + " :ID " + InstanceID;
        }
    }
    public struct ActionOverrideEventForInput : IBufferElementData, IEquatable<ActionOverrideEventForInput>
    {
        public ActionType ActionType;
        public uint ActionIndex;
        public int InstanceID;

        public ActionOverrideEventForInput(ActionType type, uint actionIndex, int instanceID)
        {
            ActionType = type;
            ActionIndex = actionIndex;
            InstanceID = instanceID;
        }
        public bool Equals(ActionOverrideEventForInput other) =>
             ActionType == other.ActionType &&
            ActionIndex == other.ActionIndex &&
             InstanceID == other.InstanceID;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (int)ActionType;
                hash = hash * 23 + ActionIndex.GetHashCode();
                hash = hash * 23 + InstanceID.GetHashCode();
                return hash;
            }
        }
        public override string ToString()
        {
            return "Type:" + ActionType + " :imdex " + ActionIndex + " :ID " + InstanceID;
        }
        public ActionOverrideEvent GetOriginalEvent() => new ActionOverrideEvent(ActionType, ActionIndex, InstanceID);
        public ActionOverrideEventCopy GetCopyEvent() => new ActionOverrideEventCopy(ActionType, ActionIndex, InstanceID);
    }
    public struct ActionOverrideEventCopy : IBufferElementData, IEquatable<ActionOverrideEventCopy>
    {
        public ActionType ActionType;
        public uint ActionIndex;
        public int InstanceID;

        public ActionOverrideEventCopy(ActionType type, uint actionIndex, int instanceID)
        {
            ActionType = type;
            ActionIndex = actionIndex;
            InstanceID = instanceID;
        }

        public bool Equals(ActionOverrideEventCopy other) =>
             ActionType == other.ActionType &&
            ActionIndex == other.ActionIndex &&
             InstanceID == other.InstanceID;

        public ActionOverrideEventForInput GetOriginalEvent() => new ActionOverrideEventForInput(ActionType, ActionIndex, InstanceID);
    }
}
