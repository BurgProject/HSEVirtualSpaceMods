using System;
using HSE.Core.Components;
using HSE.Core.Components.Network;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HSE.CodeCraft
{
    public interface ICraftObjectSettings
    {
        GameObject Reference { get; }
        TransformUsageInterpret TransformUsage { get; }
        string UniqueName { get; }
        GhostType Mode { get; }
        bool IsPhysics { get; }
    }

    [Serializable]
    public sealed class CraftObjectSettings : ICraftObjectSettings
    {
        public GameObject Reference;
        public TransformUsageInterpret TransformUsage = TransformUsageInterpret.Dynamic;
        public string UniqueName = "";
        public GhostType Mode = GhostType.Interpolated;
        public bool IsPhysics;

        GameObject ICraftObjectSettings.Reference => Reference;
        TransformUsageInterpret ICraftObjectSettings.TransformUsage => TransformUsage;
        string ICraftObjectSettings.UniqueName => UniqueName;
        GhostType ICraftObjectSettings.Mode => Mode;
        bool ICraftObjectSettings.IsPhysics => IsPhysics;

        public static CraftObjectSettings Copy(ICraftObjectSettings source) => new CraftObjectSettings
        {
            Reference = source.Reference, TransformUsage = source.TransformUsage,
            UniqueName = source.UniqueName, Mode = source.Mode, IsPhysics = source.IsPhysics
        };

        public bool Matches(ICraftObjectSettings other) => other != null && Reference == other.Reference &&
            TransformUsage == other.TransformUsage && UniqueName == other.UniqueName && Mode == other.Mode && IsPhysics == other.IsPhysics;
    }

    [Serializable]
    public class CraftInstance
    {
        public CraftObject CraftObj;
        public int InstanceID;
        [NonSerialized] public ICraftObjectSettings Source;

        public bool IsPhysics => Source != null && Source.IsPhysics;

        public CraftInstance(CraftObject marker)
        {
            CraftObj = marker;
            InstanceID = marker.GetInstanceID();
        }

        internal void Prepare(GraphBaker baker)
        {
            if (Source?.Reference == null || CraftObj == null) return;
            var usage = (TransformUsageFlags)(int)Source.TransformUsage;
            if (Source.IsPhysics) usage |= TransformUsageFlags.WorldSpace;
            baker.AddOverrideEntity(Source.Reference, InstanceID, usage);

            FixedString32Bytes uniqueName = default;
            uniqueName.CopyFromTruncated(string.IsNullOrWhiteSpace(Source.UniqueName) ? Source.Reference.name : Source.UniqueName);
            baker.AddComponent(baker.GetEntity(InstanceID), new ActionOverrideEvent(ActionType.Initialization, 0, InstanceID),
                new NetworkObjectIdentity { UniqueName = uniqueName, GhostType = Source.Mode }, HSEComponentWorld.Both);
            baker.AddComponent(baker.GetEntity(InstanceID), new ActionOverrideEvent(ActionType.OnStart, 0, InstanceID),
                new ObjectIdentity(InstanceID), HSEComponentWorld.Server);
        }
    }
}
