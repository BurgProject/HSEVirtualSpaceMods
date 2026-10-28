using System;
using System.Linq;
using GraphProcessor;
using HSE.Core.Components.Network;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HSE.CodeCraft
{
    [Serializable]
    public sealed class CraftObjectParameter : ExposedParameter
    {
        [SerializeField, HideInInspector] private CraftObject val;
        [SerializeField] private CraftObjectSettings data = new();
        [NonSerialized] private CraftInstance cachedValue;

        public CraftObjectSettings Data
        {
            get
            {
                data ??= new CraftObjectSettings();
                if (val != null)
                {
                    data.Reference = val.gameObject;
                    val = null;
                }
                return data;
            }
        }
        public override string shortType => "Entity";

        public override object value
        {
            get
            {
                var settings = Data;
                if (!settings.Reference || !settings.Reference.TryGetComponent<CraftObject>(out var marker)) return null;
                if (cachedValue == null || cachedValue.CraftObj != marker || !ReferenceEquals(cachedValue.Source, settings))
                    cachedValue = new CraftInstance(marker) { Source = settings };
                return cachedValue;
            }
            set
            {
                val = null;
                data = value is CraftInstance instance && instance.Source != null
                    ? CraftObjectSettings.Copy(instance.Source)
                    : new CraftObjectSettings { Reference = value is CraftObject marker ? marker.gameObject : value as GameObject };
                cachedValue = null;
            }
        }

        public override Type GetValueType() => typeof(CraftInstance);

        public override bool TryReadValue(BaseGraph graph, out object result, out string error)
        {
            result = null;
            error = null;
            var settings = Data;
            if (!settings.Reference || !settings.Reference.scene.IsValid() || !settings.Reference.scene.isLoaded)
                error = "Assign a CraftObject in a loaded scene.";
            else if (settings.IsPhysics && (settings.Mode != GhostType.Predicted ||
                (settings.TransformUsage & TransformUsageInterpret.Dynamic) == 0))
                error = "IsPhysics requires Predicted mode and Dynamic transform usage.";
            if (error != null) return false;

            if (!settings.Reference.TryGetComponent<CraftObject>(out var marker))
            {
#if UNITY_EDITOR
                marker = Application.isPlaying ? settings.Reference.AddComponent<CraftObject>() : Undo.AddComponent<CraftObject>(settings.Reference);
#else
                marker = settings.Reference.AddComponent<CraftObject>();
#endif
            }
            var owner = graph.nodes.OfType<CodeCraftNode>().FirstOrDefault(node => node.GUID == marker.OwnerGUID && node.Reference == settings.Reference);
            bool sharedOwner = owner != null;
            if (sharedOwner && !settings.Matches(owner))
            {
                error = "Parameter settings conflict with the CraftObject node for this object. Connect Set to copy its settings.";
                return false;
            }
            bool parameterOwner = graph.exposedParameters.OfType<CraftObjectParameter>().Any(parameter =>
                parameter.guid == marker.OwnerGUID && parameter.Data.Reference == settings.Reference);
            if (!sharedOwner && !parameterOwner && !marker.TrySetCraftGUID(guid))
            {
                error = "CraftObject belongs to another graph.";
                return false;
            }
            if (settings.IsPhysics && !marker.ConfigurePhysics(out error)) return false;
            if (!settings.IsPhysics) marker.DisablePhysics();
            result = value;
            return true;
        }

        public override bool TryWriteValue(BaseGraph graph, object input, out string error)
        {
            error = null;
            if (input is not CraftInstance instance || instance.Source == null || instance.CraftObj == null)
            {
                error = "Connect Set to a valid CraftObject Entity output.";
                return false;
            }
            if (Data.Matches(instance.Source)) return true;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(graph, "Set CraftObject parameter");
#endif
            graph.UpdateExposedParameter(guid, instance);
#if UNITY_EDITOR
            EditorUtility.SetDirty(graph);
#endif
            return true;
        }
    }
}
