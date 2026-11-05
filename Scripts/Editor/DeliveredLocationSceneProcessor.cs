#if UNITY_EDITOR
using HSE.Mechanics.Authorings;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HSE_MAIN.Editor.Mods
{
    /// <summary>В контентном архиве Mono-сцена не владеет ECS-подсценами. Исходная сцена и сцены Player build не изменяются.</summary>
    public sealed class DeliveredLocationSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report != null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var roots = scene.GetRootGameObjects();
            bool delivered = false;

            foreach (var root in roots)
                delivered |= root.GetComponentInChildren<DeliveredLocationScene>(true) != null;

            if (!delivered)
                return;

            foreach (var root in roots)
            {
                foreach (var subScene in root.GetComponentsInChildren<SubScene>(true))
                    Object.DestroyImmediate(subScene);
            }
        }
    }
}
#endif
