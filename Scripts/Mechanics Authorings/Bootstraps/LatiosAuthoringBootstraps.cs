using Latios;
using Latios.Authoring;
using Unity.Entities;

namespace HSE.Bootstrap
{
    /// <summary>
    /// Baking and editor-world bootstraps. Live in the package so a creator project that has only the package bakes
    /// skeletons, skinned meshes and clips (SingleClipAuthoring) with Kinemation, exactly as the client does. Latios
    /// finds them in any assembly; there must be no second copy in the project (it would log "Multiple custom ...").
    /// The runtime world bootstraps stay in the client: they depend on client-only code.
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class LatiosBakingBootstrap : ICustomBakingBootstrap
    {
        public void InitializeBakingForAllWorlds(ref CustomBakingBootstrapContext context)
        {
            Latios.Kinemation.Authoring.KinemationBakingBootstrap.InstallKinemation(ref context);
        }
    }

    [UnityEngine.Scripting.Preserve]
    public class LatiosEditorBootstrap : ICustomEditorBootstrap
    {
        public World Initialize(string defaultEditorWorldName)
        {
            var world = new LatiosWorld(defaultEditorWorldName, WorldFlags.Editor);

            var systems = DefaultWorldInitialization.GetAllSystemTypeIndices(WorldSystemFilterFlags.Default, true);
            BootstrapTools.InjectUnitySystems(systems, world, world.simulationSystemGroup);

            Latios.Kinemation.KinemationBootstrap.InstallKinemation(world);

            BootstrapTools.InjectUserSystems(systems, world, world.simulationSystemGroup);

            return world;
        }
    }
}
