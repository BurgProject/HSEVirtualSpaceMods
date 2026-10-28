#if !HSE_BUILD
using Unity.NetCode;
using UnityEngine.Scripting;

namespace HSE.Playtest
{
    /// <summary>
    /// Play mode in a mod project: one local world (Entities + physics, no NetCode client/server). NetCode's default
    /// bootstrap would make a client and a server world that never connect, and a client world steps physics only
    /// while connected, so the walk-test character would hang in the air. Being derived from ClientServerBootstrap,
    /// this replaces NetCode's default. Not compiled in the metaverse client (HSE_BUILD), which has its own bootstrap.
    /// </summary>
    [Preserve]
    public sealed class PlaytestWorldBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            AutoConnectPort = 0;
            CreateLocalWorld(defaultWorldName);
            return true;
        }
    }
}
#endif
