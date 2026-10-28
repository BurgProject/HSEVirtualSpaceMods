using System;
using GraphProcessor;
using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace HSE.CodeCraft
{
    /// <summary>Настройки обычного authoring. Применяются в редакторе до baking, не являются runtime-командой.</summary>
    public abstract class AuthoringAdapterNode : BaseHSENode
    {
        [Input] public CraftInstance Target;

        public GameObject TargetObject => Target?.CraftObj != null ? Target.CraftObj.gameObject : null;
        public abstract Type AuthoringType { get; }
        public abstract bool IsApplied { get; }
        internal abstract void Apply();
        internal abstract void RegisterDependencies(IBaker baker);
        protected abstract bool ValidateSettings();

        /// <summary>Записывает runtime-команды после проверки authoring. Не добавляет ECS-компоненты в baking-мир.</summary>
        protected virtual void Craft(GraphBaker baker) { }

        internal void CraftCommands(GraphBaker baker) => Craft(baker);

        internal bool ValidateAdapter()
        {
            if (TargetObject == null || !TargetObject.scene.IsValid() || !TargetObject.scene.isLoaded)
                return Fail("Connect Target to a CraftObject in a loaded scene.");
            var authorings = TargetObject.GetComponents(AuthoringType);
            if (authorings.Length > 1)
                return Fail("Target has multiple " + AuthoringType.Name + " components.");
            if (authorings.Length == 1 && !((Behaviour)authorings[0]).enabled)
                return Fail("Enable the target authoring before applying graph settings.");
            return ValidateSettings();
        }

        protected bool Fail(string message)
        {
            AddMessage(message, NodeMessageType.Error);
            return false;
        }

        protected override bool Process()
        {
            try
            {
                return ValidateAdapter();
            }
            catch (Exception exception)
            {
                AddMessage(exception.Message, NodeMessageType.Error);
                return false;
            }
        }
    }

    public abstract class AuthoringAdapterNode<T> : AuthoringAdapterNode where T : MonoBehaviour
    {
        public override Type AuthoringType => typeof(T);
        public override bool IsApplied => TargetObject != null && TargetObject.TryGetComponent<T>(out var authoring) && Matches(authoring);

        protected abstract bool Matches(T authoring);
        protected abstract void CopyTo(T authoring);

        internal override void RegisterDependencies(IBaker baker)
        {
            if (baker != null) baker.GetComponent<T>(TargetObject);
        }

        internal override void Apply()
        {
#if UNITY_EDITOR
            var authoring = TargetObject.GetComponent<T>();
            if (authoring == null) authoring = Target.CraftObj.AddOwnedComponent<T>();
            else Undo.RecordObject(authoring, "Apply graph authoring");

            CopyTo(authoring);
            PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);
            EditorUtility.SetDirty(authoring);
            EditorSceneManager.MarkSceneDirty(TargetObject.scene);
#else
            throw new InvalidOperationException("Authoring adapters can only be applied in the editor.");
#endif
        }
    }
}
