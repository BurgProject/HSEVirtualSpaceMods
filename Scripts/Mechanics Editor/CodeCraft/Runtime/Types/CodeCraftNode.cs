
using GraphProcessor;
using HSE.Core.Components;
using HSE.Core.Components.Network;
using System;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HSE.CodeCraft
{
    [Flags]
    public enum TransformUsageInterpret: int
    {
        None = 0,
        Renderable = 1,
        Dynamic = 1 << 1,
    }
    [System.Serializable, NodeMenuItem("CraftObject")]
    public class CodeCraftNode : BaseHSENode, ICreateNodeFrom<GameObject>, ICraftObjectSettings
    {
        [Input, ShowAsDrawer]
        [Tooltip("Референс GameObject со сцены")]
        public GameObject Reference;

        [Output(allowMultiple = true)]
        [Tooltip("Получаемая из GameObject редактируемая игровая сущность")]
        public CraftInstance Entity;

        public TransformUsageInterpret TransformUsage = TransformUsageInterpret.Dynamic;

        [Tooltip("Основа имени сетевого префаба. Если пусто, используется имя GameObject; уникальный индекс добавляется в рантайме.")]
        public string UniqueName = "";
        public GhostType Mode = GhostType.Interpolated;
        [UnityEngine.Serialization.FormerlySerializedAs("MovingPlatform")]
        public bool IsPhysics;

        GameObject ICraftObjectSettings.Reference => Reference;
        TransformUsageInterpret ICraftObjectSettings.TransformUsage => TransformUsage;
        string ICraftObjectSettings.UniqueName => UniqueName;
        GhostType ICraftObjectSettings.Mode => Mode;
        bool ICraftObjectSettings.IsPhysics => IsPhysics;

        private CraftObject marker;

        public override Color color => Color.red;
        public override string name => "CraftObject";

        private const string REFERENCE_NULL_ERROR = "GameObject Reference не задан!";
        private const string REFERENCE_SAME_ERROR = 
            "GameObject Reference был выбран для другого CraftObject, " +
            "выберите другой объект или удалите ноду. \n " +
            "Выполнение цепочки графа будет приостановлено для этой ноды!";

        public bool InitializeNodeFromObject(GameObject value)
        {
            Reference = value;
            if (!Reference) return false;
            bool isOwner = EnsureMarker();
            AddStateMessage(REFERENCE_SAME_ERROR, !isOwner);
            Entity = isOwner ? new CraftInstance(marker) { Source = this } : null;
            return true;
        }

        private bool EnsureMarker()
        {
            if (!Reference.TryGetComponent(out marker))
            {
#if UNITY_EDITOR
                marker = Application.isPlaying ? Reference.AddComponent<CraftObject>() : Undo.AddComponent<CraftObject>(Reference);
#else
                marker = Reference.AddComponent<CraftObject>();
#endif
            }
            return marker.TrySetCraftGUID(GUID) || graph != null && graph.exposedParameters
                .OfType<CraftObjectParameter>().Any(parameter => parameter.guid == marker.OwnerGUID && parameter.Data.Reference == Reference);
        }

        protected override bool Process()
        {
            Entity = null;
            bool isRefNull = Reference == null;
            AddStateMessage(REFERENCE_NULL_ERROR, isRefNull);
            if (isRefNull) return false;

            bool isOwner = EnsureMarker();
            AddStateMessage(REFERENCE_SAME_ERROR, !isOwner);
            if (isOwner)
                Entity = new CraftInstance(marker) { Source = this };
            else Entity = null;
            if (isOwner && !IsPhysics) marker.DisablePhysics();
            bool validPhysics = !IsPhysics || Mode == GhostType.Predicted && (TransformUsage & TransformUsageInterpret.Dynamic) != 0;
            AddStateMessage("IsPhysics requires Predicted ghost mode and Dynamic transform usage.", !validPhysics);
            if (isOwner && validPhysics && IsPhysics && !marker.ConfigurePhysics(out var error))
            {
                AddMessage(error, NodeMessageType.Error);
                return false;
            }
            return isOwner && validPhysics;
        }

        internal void Prepare(GraphBaker baker)
        {
            Entity?.Prepare(baker);
        }
        protected override void Destroy()
        {
            base.Destroy();
            if (!marker && Reference) Reference.TryGetComponent(out marker);
            if (!marker || marker.OwnerGUID != GUID) return;

            var parameter = graph?.exposedParameters.OfType<CraftObjectParameter>()
                .FirstOrDefault(candidate => candidate.Data.Reference == marker.gameObject);
            if (parameter != null)
            {
#if UNITY_EDITOR
                Undo.RecordObject(marker, "Transfer CraftObject owner");
#endif
                marker.ResetGUID();
                marker.TrySetCraftGUID(parameter.guid);
                Entity = null;
                return;
            }

            marker.RemoveOwnedComponents();
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.DestroyObjectImmediate(marker);
            else UnityEngine.Object.Destroy(marker);
#else
            UnityEngine.Object.Destroy(marker);
#endif
            Entity = null;
        }
    }

    [System.Serializable, NodeMenuItem("Conditional/Comparison")]
    public class Comparison : BaseNode
    {
        [Input(name = "In A")]
        public float inA;

        [Input(name = "In B")]
        public float inB;

        [Output(name = "Out")]
        public bool compared;

        public CompareFunction compareFunction = CompareFunction.LessEqual;

        public override string name => "Comparison";

        protected override bool Process()
        {
            switch (compareFunction)
            {
                default:
                case CompareFunction.Disabled:
                case CompareFunction.Never: compared = false; break;
                case CompareFunction.Always: compared = true; break;
                case CompareFunction.Equal: compared = inA == inB; break;
                case CompareFunction.Greater: compared = inA > inB; break;
                case CompareFunction.GreaterEqual: compared = inA >= inB; break;
                case CompareFunction.Less: compared = inA < inB; break;
                case CompareFunction.LessEqual: compared = inA <= inB; break;
                case CompareFunction.NotEqual: compared = inA != inB; break;
            }
            return true;
        }
    }
}
