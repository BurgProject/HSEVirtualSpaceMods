using GraphProcessor;
using HSE.Editor.Attributes;
using HSE.Core.Components;
using HSE.Mechanics.Components;
using Latios.Authoring;
using Latios.Kinemation;
using System.Collections.Generic;
using Unity.Entities;


#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HSE.CodeCraft
{
    public class CodeCraftScenario : MonoBehaviour
    {
        [ShowIf(nameof(createFromGraphAsset))]
        [SerializeField] private BaseGraph graphAsset;
        [SerializeField, HideInInspector] private BaseGraph graphAssetCopy;

        [SerializeField, ReadOnly] private BaseGraph _graphInstance;

        [SerializeField] private bool createFromGraphAsset;

        public BaseGraph GraphInstance => _graphInstance;
        
        public GraphBaker GraphBaker;


#if UNITY_EDITOR
        void OnValidate()
        {
            InitializeGraphInstance();
        }
        private void InitializeGraphInstance()
        {
            if (graphAsset == null)
            {
                UnityEngine.Debug.LogError($"CodeGraphAsset reference is null in {this}");
                graphAsset = ScriptableObject.CreateInstance<BaseGraph>();
                graphAssetCopy = graphAsset;
                // return;
            }
            if(_graphInstance == null)
                _graphInstance = Instantiate(graphAsset);
            else if(graphAssetCopy != graphAsset && createFromGraphAsset)
                _graphInstance = Instantiate(graphAsset);
            
            if (!_graphInstance.IsLinkedToScene()){
                _graphInstance.LinkToSceneMonoBehaviour(this);
            }

            graphAssetCopy = graphAsset;
            
        }
        public void BakeScene()
        {
            CloseAndAskSaveIfUserWantsTo(gameObject.scene);
        }
        public static void CloseAndAskSaveIfUserWantsTo(params Scene[] subScenes)
        {
            if (!Application.isPlaying)
            {
                var dirtyScenes = new List<Scene>();
                foreach (var scene in subScenes)
                {
                    if (scene.isLoaded && scene.isDirty)
                    {
                        dirtyScenes.Add(scene);
                    }
                }

                if (dirtyScenes.Count != 0)
                {
                    if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(dirtyScenes.ToArray()))
                        return;
                }

                CloseSceneWithoutSaving(subScenes);
            }
            else
            {
                foreach (var scene in subScenes)
                {
                    if (scene.isLoaded)
                        EditorSceneManager.UnloadSceneAsync(scene);
                }
            }
        }
         
        private static void CloseSceneWithoutSaving(Scene[] scenes)
        {
            foreach (var scene in scenes)
                EditorSceneManager.CloseScene(scene, true);
        }

        //class Baker : Baker<CodeCraftObject>
        //{
        //    public override void Bake(CodeCraftObject authoring)
        //    {
        //        DependsOn(authoring.GraphInstance);
        //        if (authoring.GraphInstance == null)
        //            return;
        //        var entity = GetEntity(authoring.UsageFlags);
        //        GraphBaker graphBaker = new GraphBaker(authoring.GraphInstance, this, entity);
        //        AddBuffer<ActionOverrideEvent>(entity);

        //        authoring.GraphInstance.TraverseGraph<CraftNode>(graphBaker);
        //        graphBaker.ConstructBlob();
        //    }
        //}

        [TemporaryBakingType]
        struct CodeCraftObjectBakeItem : ISmartBakeItem<CodeCraftScenario>
        {
            SmartBlobberHandle<SkeletonClipSetBlob> BlobAnimation;
            public bool Bake(CodeCraftScenario authoring, IBaker baker)
            {
                if (authoring.GraphInstance == null)
                    return false;

                baker.DependsOn(authoring.GraphInstance);
                GraphBaker graphBaker = new GraphBaker(authoring, baker);
                ConditionalProcessor processor = new ConditionalProcessor(authoring.GraphInstance, graphBaker);
                authoring.GraphBaker = graphBaker;
                processor.Run();
                if (processor.IsGraphWithErrors)
                {
                    authoring.GraphBaker = null;
                    Debug.LogError("Graph validation failed; baking skipped.", authoring);
                    return false;
                }
                graphBaker.Complete();
                // authoring.fac = false;
                //if (graphBaker.AnimationClips != null)
                //{
                //    var clips = new NativeArray<SkeletonClipConfig>(graphBaker.AnimationClips.Count, Allocator.Temp);
                //    for (int i = 0; i < graphBaker.AnimationClips.Count; i++)
                //    {

                //        clips[i] = new SkeletonClipConfig { clip = graphBaker.AnimationClips[i], settings = SkeletonClipCompressionSettings.kDefaultSettings };
                //    }
                //    if (!authoring.TryGetComponent(out Animator animator))
                //        animator = authoring.gameObject.AddComponent<Animator>();
                //    BlobAnimation = graphBaker.Baker.RequestCreateBlobAsset(animator, clips);
                //}
                return true;
            }

            public void PostProcessBlobRequests(EntityManager entityManager, Entity entity)
            {
                if (entityManager.HasComponent<ClipSetStorage>(entity))
                    entityManager.SetComponentData(entity, new ClipSetStorage { blob = BlobAnimation.Resolve(entityManager) });
            }
        }

        class CodeCraftObjectBaker : SmartBaker<CodeCraftScenario, CodeCraftObjectBakeItem>
        {
        }
#endif

    }
}
