using Unity.Entities;

namespace HSE.Core.Components
{
    /// <summary>Where a cursor event is detected and runs. Stored in <see cref="ActionOverrideEvent.ActionIndex"/>.</summary>
    public enum CursorTriggerMode : byte
    {
        /// <summary>The client reports its hover target in its input; the server verifies it and runs the event for everyone.</summary>
        Server = 0,
        /// <summary>Only the hovering client runs the event (Client-world commands).</summary>
        Client = 1,
    }

    /// <summary>
    /// One cursor event node on a source CraftObject: fires while a player's view ray hits the source within
    /// <see cref="Distance"/> metres. Present in both worlds: the client needs it to detect hovers, the server to verify them.
    /// </summary>
    [HSESerializableStruct(World = HSEComponentWorld.Both)]
    public struct CursorTriggerRule : IBufferElementData
    {
        public ActionType Event;
        public CursorTriggerMode Mode;
        public int SourceInstanceId;
        public float Distance;
    }
}
