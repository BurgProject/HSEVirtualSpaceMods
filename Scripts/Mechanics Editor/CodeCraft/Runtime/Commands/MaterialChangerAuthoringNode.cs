using System;
using System.Collections.Generic;
using System.Linq;
using GraphProcessor;
using HSE.Mechanics.Authorings;
using HSE.Core.Components;
using HSE.Mechanics.Components.Network;
using UnityEngine;

namespace HSE.CodeCraft
{
    [Serializable, NodeMenuItem("Hybrid/Material Changer")]
    public sealed class MaterialChangerAuthoringNode : AuthoringAdapterNode<TableSwitcherAuthoring>
    {
        [Input(allowMultiple = false), ShowAsDrawer] public Material[] Materials = Array.Empty<Material>();
        [Input(allowMultiple = false), ShowAsDrawer] public Texture2D[] Textures = Array.Empty<Texture2D>();
        public bool SwitchByTexture;

        [CustomPortInput(nameof(Materials), typeof(Material[]))]
        public void PullMaterials(List<SerializableEdge> edges)
        {
            if (edges.Count == 0) return;
            var values = edges[0].passThroughBuffer as Material[];
            Materials = values == null ? Array.Empty<Material>() : (Material[])values.Clone();
        }

        [CustomPortInput(nameof(Textures), typeof(Texture2D[]))]
        public void PullTextures(List<SerializableEdge> edges)
        {
            if (edges.Count == 0) return;
            var values = edges[0].passThroughBuffer as Texture2D[];
            Textures = values == null ? Array.Empty<Texture2D>() : (Texture2D[])values.Clone();
        }

        protected override bool ValidateSettings()
        {
            UnityEngine.Object[] selected = SwitchByTexture ? (UnityEngine.Object[])Textures : Materials;
            if (selected == null || selected.Length == 0 || selected.Any(item => item == null))
                return Fail("Select at least one valid material or texture for the active mode.");
            if (Materials != null && Materials.Any(item => item == null) ||
                Textures != null && Textures.Any(item => item == null))
                return Fail("Material and texture lists must not contain empty entries.");
            return true;
        }

        protected override bool Matches(TableSwitcherAuthoring authoring) =>
            authoring.switchByTex == SwitchByTexture &&
            authoring.materials != null && authoring.materials.SequenceEqual(Materials ?? Array.Empty<Material>()) &&
            authoring.texSwitches != null && authoring.texSwitches.SequenceEqual(Textures ?? Array.Empty<Texture2D>());

        protected override void CopyTo(TableSwitcherAuthoring authoring)
        {
            authoring.materials = Materials == null ? Array.Empty<Material>() : (Material[])Materials.Clone();
            authoring.texSwitches = Textures == null ? Array.Empty<Texture2D>() : (Texture2D[])Textures.Clone();
            authoring.switchByTex = SwitchByTexture;
        }

        protected override void Craft(GraphBaker baker)
        {
            var entity = baker.GetEntity(Target.InstanceID);
            var initialization = new ActionOverrideEvent(ActionType.Initialization, 0, Target.InstanceID);
            uint maxCount = (uint)(SwitchByTexture ? Textures.Length : Materials.Length);

            baker.AddComponent(entity, initialization, new MaterialSwitcherCounter { maxCount = maxCount }, HSEComponentWorld.Both);
        }
    }
}
