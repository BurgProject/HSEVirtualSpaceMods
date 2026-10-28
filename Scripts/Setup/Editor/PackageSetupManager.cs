#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Project setup for HSE Metaverse mod projects: adds the scripting defines, imports the TMP and HSEverse essentials,
/// then applies every project setting the metaverse client is built with (render pipeline + Forward+, tags / layers /
/// rendering layers, player settings). Runs by itself once per project, as soon as the package is added; the
/// HSEverse/Setup window runs it again on demand.
///
/// Defines come first: Latios and every package assembly that uses it are define-constrained on ENTITY_STORE_V1 and
/// UNITY_BURST_EXPERIMENTAL_ATOMIC_INTRINSICS, so in a fresh project they are skipped instead of failing, this
/// assembly (no Latios dependency) loads, adds the defines, and the recompile brings the rest in.
///
/// Package import is asynchronous and both the defines and the imports trigger a domain reload, so the flow is a
/// small stage machine kept in SessionState (survives reloads, not editor restarts): the [InitializeOnLoad]
/// constructor resumes whatever stage was running, and each stage first checks for its result on disk - a callback
/// lost to a reload can never stall it.
///
/// Not compiled in the metaverse client itself (HSE_BUILD): its settings are its own.
/// </summary>
[InitializeOnLoad]
public class PackageSetupManager
{
    private const string PACKAGE_ID = "com.hseverse.mods";
    private const string ESSENTIALS_UNITYPACKAGE = "Packages/com.hseverse.mods/CodeCraftModes.unitypackage";
    private const string ESSENTIALS_MARKER = "Assets/Scenes/Main/ContentScene.unity"; // what CodeCraftModes imports
    public const string RENDER_PIPELINE_ASSET = "Assets/Settings/Graphics/URP.asset";
    private const string DEFAULT_SCENE = "Assets/Scenes/Main/ContentScene.unity";
    private const string TMPRO_ESSENTIALS_UNITYPACKAGE = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
    private const string TMPRO_ESSENTIALS_MARKER = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string TAG_MANAGER_ASSET = "ProjectSettings/TagManager.asset";
    private const string PROJECT_SETTINGS_ASSET = "ProjectSettings/ProjectSettings.asset";
    // Per project and committed with it (EditorPrefs would be per machine: a second project never set itself up).
    private const string SETUP_DONE_MARKER = "ProjectSettings/HSEverseSetup.txt";
    private const string PACKAGE_EXPORTER_TYPE = "HSE_MAIN.Editor.Mods.PackageExporterSO, HSE.Mechanics.Editor";
    private const string PACKAGE_EXPORTER_ASSET = "Assets/PackageExporter.asset";

    // ---------------------------------------------------------------- what the metaverse client is built with
    private const string REQUIRED_TAG = "Center";
    private const int MODEL_LAYER_INDEX = 6;
    private const string MODEL_LAYER_NAME = "ModelLayer";
    private static readonly string[] RenderingLayerNames = { "Default", "LoadingModel" };
    private const int ACTIVE_INPUT_HANDLER_BOTH = 2;      // PlayerSettings.activeInputHandler: 0 old, 1 new, 2 both
    private const int RENDERING_MODE_FORWARD_PLUS = 2;    // UnityEngine.Rendering.Universal.RenderingMode.ForwardPlus

    private static readonly NamedBuildTarget[] Il2CppTargets =
        { NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS };

    // Per platform, exactly the lists of the client. Missing ones are ADDED to whatever the mod project already has.
    private static readonly Dictionary<NamedBuildTarget, string[]> RequiredDefines = new Dictionary<NamedBuildTarget, string[]>
    {
        { NamedBuildTarget.Standalone, new[]
            {
                "ENABLE_CONTENT_DELIVERY", "ENTITY_STORE_V1", "LATIOS_TRANSFORMS_UNITY",
                "UNITY_BURST_EXPERIMENTAL_ATOMIC_INTRINSICS", "UNITY_PHYSICS_CUSTOM", "UNITY_POST_PROCESSING_STACK_V2",
                "DOTWEEN", "ENABLE_JSON_CATALOG", "URP_COMPATIBILITY_MODE",
            } },
        { NamedBuildTarget.Android, new[]
            {
                "ENABLE_JSON_CATALOG", "UNITY_POST_PROCESSING_STACK_V2", "UNITY_PHYSICS_CUSTOM", "ENABLE_CONTENT_DELIVERY",
                "ENTITY_STORE_V1", "LATIOS_TRANSFORMS_UNITY", "UNITY_BURST_EXPERIMENTAL_ATOMIC_INTRINSICS", "DOTWEEN",
            } },
        { NamedBuildTarget.iOS, new[] { "ENABLE_JSON_CATALOG", "DOTWEEN" } },
        { NamedBuildTarget.WebGL, new[] { "ENABLE_JSON_CATALOG", "UNITY_POST_PROCESSING_STACK_V2", "DOTWEEN", "UNITY_PHYSICS_CUSTOM" } },
        { NamedBuildTarget.Server, new[] { "ENABLE_JSON_CATALOG" } },
    };

    // ---------------------------------------------------------------- stage machine
    private enum Stage { Idle = 0, ImportingTmp = 1, ImportingEssentials = 2, ApplyingSettings = 3, ApplyingDefines = 4 }
    private const string STAGE_KEY = PACKAGE_ID + "_SetupStage";
    private const string RESTART_KEY = PACKAGE_ID + "_RestartPending"; // Active Input Handling changed this session

    private static Stage CurrentStage
    {
        get => (Stage)SessionState.GetInt(STAGE_KEY, 0);
        set => SessionState.SetInt(STAGE_KEY, (int)value);
    }

    public static bool SetupRunning => CurrentStage != Stage.Idle;

    public static string StageLabel
    {
        get
        {
            switch (CurrentStage)
            {
                case Stage.ApplyingDefines: return "Adding scripting defines...";
                case Stage.ImportingTmp: return "Importing TMP essentials...";
                case Stage.ImportingEssentials: return "Importing HSEverse essentials...";
                case Stage.ApplyingSettings: return "Applying project settings...";
                default: return "";
            }
        }
    }

    private static bool s_Subscribed;

    static PackageSetupManager()
    {
        EditorApplication.delayCall += Initialize;
    }

    private static void Initialize()
    {
        if (Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess())
            return;

        // A setup interrupted by the domain reload a define change or an import causes picks up where it was.
        if (SetupRunning)
        {
            Resume();
            return;
        }

        OfferRestartIfPending();
        if (File.Exists(SETUP_DONE_MARKER))
            return;
        // A project set up before the marker existed: the old essentials package brought this asset.
        if (File.Exists(RENDER_PIPELINE_ASSET))
        {
            MarkSetupComplete();
            return;
        }
        Debug.Log("[Package Setup] first load in this project - setting it up (HSEverse/Setup runs it again)");
        ApplyRecommendedSettings();
    }

    [MenuItem("HSEverse/Setup")]
    private static void ShowSetupWindow()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var window = EditorWindow.GetWindow<PackageSetupWindow>(true, "Package Setup", true);
                window.minSize = new Vector2(450, 400);
                window.maxSize = new Vector2(450, 400);
            }
        };
    }

    [System.Serializable]
    public class PackageVersion
    {
        public string version;
    }

    // ---------------------------------------------------------------- the one button
    /// <summary>Add the defines, import both packages, then apply every setting. Safe to call again after a failure.</summary>
    public static void ApplyRecommendedSettings()
    {
        if (SetupRunning)
            return;
        CurrentStage = Stage.ApplyingDefines;
        Resume();
    }

    private static void Resume()
    {
        Subscribe();
        Advance();
    }

    /// <summary>
    /// Run the current stage. A stage whose result already exists on disk is skipped straight away, so a completion
    /// callback lost to a domain reload, or a package imported earlier by hand, never stalls the flow.
    /// </summary>
    private static void Advance()
    {
        switch (CurrentStage)
        {
            case Stage.ApplyingDefines:
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += Advance;
                    return;
                }
                // The next stage is recorded first: a define change recompiles and reloads the domain, and the
                // constructor resumes from it with Latios and the package assemblies compiled in.
                CurrentStage = Stage.ImportingTmp;
                if (ApplyScriptingDefines())
                    return;
                Advance();
                return;

            case Stage.ImportingTmp:
                if (File.Exists(TMPRO_ESSENTIALS_MARKER) || !File.Exists(TMPRO_ESSENTIALS_UNITYPACKAGE))
                {
                    CurrentStage = Stage.ImportingEssentials;
                    Advance();
                    return;
                }
                AssetDatabase.ImportPackage(TMPRO_ESSENTIALS_UNITYPACKAGE, false);
                return;

            case Stage.ImportingEssentials:
                // "Packages/<name>/..." is a virtual path (the folder on disk is named differently, or lives in the
                // package cache): GetFullPath resolves it, System.IO alone never finds it.
                string essentials = Path.GetFullPath(ESSENTIALS_UNITYPACKAGE);
                if (File.Exists(ESSENTIALS_MARKER) || !File.Exists(essentials))
                {
                    CurrentStage = Stage.ApplyingSettings;
                    Advance();
                    return;
                }
                AssetDatabase.ImportPackage(essentials, false);
                return;

            case Stage.ApplyingSettings:
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    // The reload that follows the import brings us back here through the constructor.
                    EditorApplication.delayCall += Advance;
                    return;
                }
                ApplySettings();
                return;
        }
    }

    private static void Subscribe()
    {
        if (s_Subscribed)
            return;
        s_Subscribed = true;
        AssetDatabase.importPackageCompleted += OnImportCompleted;
        AssetDatabase.importPackageFailed += OnImportFailed;
        AssetDatabase.importPackageCancelled += OnImportCancelled;
    }

    private static void OnImportCompleted(string packageName)
    {
        switch (CurrentStage)
        {
            case Stage.ImportingTmp: CurrentStage = Stage.ImportingEssentials; break;
            case Stage.ImportingEssentials: CurrentStage = Stage.ApplyingSettings; break;
            default: return;
        }
        // Not from inside the import callback: the next import (or the settings pass) starts a tick later.
        EditorApplication.delayCall += Advance;
    }

    private static void OnImportFailed(string packageName, string errorMessage) => Abort("import of " + packageName + " failed: " + errorMessage);
    private static void OnImportCancelled(string packageName) => Abort("import of " + packageName + " was cancelled");

    private static void Abort(string why)
    {
        if (!SetupRunning)
            return;
        CurrentStage = Stage.Idle;
        EditorUtility.ClearProgressBar();
        Debug.LogError("[Package Setup] " + why);
    }

    // ---------------------------------------------------------------- the settings pass
    private static void ApplySettings()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Package Setup", "Configuring render pipeline...", 0.3f);
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(RENDER_PIPELINE_ASSET);
            if (pipelineAsset != null)
            {
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;
                int activeLevel = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i);
                    QualitySettings.renderPipeline = pipelineAsset;
                }
                QualitySettings.SetQualityLevel(activeLevel);

                EditorUtility.DisplayProgressBar("Package Setup", "Setting Forward+ rendering...", 0.4f);
                SetForwardPlusRendering(pipelineAsset);
            }
            else
            {
                Debug.LogWarning("[Package Setup] " + RENDER_PIPELINE_ASSET + " not found - render pipeline left as is");
            }

            EditorUtility.DisplayProgressBar("Package Setup", "Tags, layers, rendering layers...", 0.55f);
            ApplyTagsAndLayers();

            EditorUtility.DisplayProgressBar("Package Setup", "Player settings...", 0.7f);
            if (ApplyPlayerSettings())
                SessionState.SetBool(RESTART_KEY, true);

            EditorUtility.DisplayProgressBar("Package Setup", "Package exporter...", 0.8f);
            CreatePackageExporter();

            EditorUtility.DisplayProgressBar("Package Setup", "Loading default scene...", 0.85f);
            if (File.Exists(DEFAULT_SCENE))
            {
                EditorSceneManager.OpenScene(DEFAULT_SCENE);
            }

            AssetDatabase.SaveAssets();
            MarkSetupComplete();
            CurrentStage = Stage.Idle;

            // Last on purpose: a define change queues a recompile + domain reload, and nothing above may be cut short.
            EditorUtility.DisplayProgressBar("Package Setup", "Scripting defines...", 0.95f);
            ApplyScriptingDefines();
            Debug.Log("[Package Setup] done");
            // After the progress bar is gone; a define reload in between is caught by Initialize.
            EditorApplication.delayCall += OfferRestartIfPending;
        }
        catch (Exception e)
        {
            CurrentStage = Stage.Idle;
            Debug.LogError($"Package setup failed: {e}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Forward+ lives on the renderer data (UniversalRendererData.renderingMode), not on the pipeline asset. Both are
    /// reached through their serialized fields, so this needs no URP API and matches the on-disk value the client
    /// ships (m_RenderingMode: 2).
    /// </summary>
    private static void SetForwardPlusRendering(RenderPipelineAsset pipelineAsset)
    {
        var so = new SerializedObject(pipelineAsset);
        var list = so.FindProperty("m_RendererDataList");
        if (list == null || !list.isArray)
        {
            Debug.LogWarning("[Package Setup] no renderer list on " + pipelineAsset.name + " - Forward+ not set");
            return;
        }

        int done = 0;
        for (int i = 0; i < list.arraySize; i++)
        {
            var renderer = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (renderer == null)
                continue;
            var rso = new SerializedObject(renderer);
            var mode = rso.FindProperty("m_RenderingMode");
            if (mode == null)
                continue; // a 2D renderer or similar - no rendering mode to set
            if (mode.intValue != RENDERING_MODE_FORWARD_PLUS)
            {
                mode.intValue = RENDERING_MODE_FORWARD_PLUS;
                rso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
            }
            done++;
        }
        Debug.Log("[Package Setup] Forward+ on " + done + " renderer(s)");
    }

    /// <summary>Tag "Center", layer 6 "ModelLayer", and the rendering-layer names of the client, all via TagManager.</summary>
    private static void ApplyTagsAndLayers()
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath(TAG_MANAGER_ASSET).FirstOrDefault();
        if (asset == null)
        {
            Debug.LogWarning("[Package Setup] " + TAG_MANAGER_ASSET + " not found");
            return;
        }

        var so = new SerializedObject(asset);

        var tags = so.FindProperty("tags");
        bool hasTag = false;
        for (int i = 0; i < tags.arraySize && !hasTag; i++)
            hasTag = tags.GetArrayElementAtIndex(i).stringValue == REQUIRED_TAG;
        if (!hasTag)
        {
            tags.arraySize++;
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = REQUIRED_TAG;
        }

        var layers = so.FindProperty("layers");
        var slot = layers.GetArrayElementAtIndex(MODEL_LAYER_INDEX);
        if (slot.stringValue != MODEL_LAYER_NAME)
        {
            if (!string.IsNullOrEmpty(slot.stringValue))
                Debug.LogWarning("[Package Setup] layer " + MODEL_LAYER_INDEX + " was " + slot.stringValue + ", renamed to " + MODEL_LAYER_NAME);
            slot.stringValue = MODEL_LAYER_NAME;
        }

        // Rendering layers (Unity 6: Tags and Layers > Rendering Layers). Exactly the list of the client.
        var renderingLayers = so.FindProperty("m_RenderingLayers");
        if (renderingLayers != null)
        {
            renderingLayers.arraySize = RenderingLayerNames.Length;
            for (int i = 0; i < RenderingLayerNames.Length; i++)
                renderingLayers.GetArrayElementAtIndex(i).stringValue = RenderingLayerNames[i];
        }
        else
        {
            Debug.LogWarning("[Package Setup] this Unity version keeps rendering layers elsewhere - not set");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>True when Active Input Handling was changed: Unity applies that only after an editor restart.</summary>
    private static bool ApplyPlayerSettings()
    {
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        foreach (var target in Il2CppTargets)
        {
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(target, ApiCompatibilityLevel.NET_Standard);
        }
        PlayerSettings.allowUnsafeCode = true;   // Entities / Burst code
        PlayerSettings.gcIncremental = true;

        // Active Input Handling has no public setter. "Both" keeps the old- and new-input code of the client working.
        var asset = AssetDatabase.LoadAllAssetsAtPath(PROJECT_SETTINGS_ASSET).FirstOrDefault();
        if (asset == null)
            return false;
        var so = new SerializedObject(asset);
        var handler = so.FindProperty("activeInputHandler");
        if (handler == null || handler.intValue == ACTIVE_INPUT_HANDLER_BOTH)
            return false;
        handler.intValue = ACTIVE_INPUT_HANDLER_BOTH;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.LogWarning("[Package Setup] Active Input Handling set to Both - takes effect after an editor restart");
        return true;
    }

    /// <summary>Until the restart the old input reads nothing, so the walk test cannot move or look.</summary>
    private static void OfferRestartIfPending()
    {
        if (!SessionState.GetBool(RESTART_KEY, false) || SetupRunning)
            return;
        SessionState.EraseBool(RESTART_KEY);
        if (!EditorUtility.DisplayDialog("HSEverse setup",
                "Active Input Handling was changed to \"Both\". Unity applies it only after an editor restart; until then " +
                "the walk test in Play mode cannot read the keyboard or the mouse.\n\nRestart the editor now?",
                "Restart now", "Later"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        // What Unity's own Player Settings dialog for this option calls. OpenProject on the open project only closed
        // the editor (seen with a Hub-created project) - it stays as the fallback if the internal call is gone.
        var restart = typeof(EditorApplication).GetMethod("RestartEditorAndRecompileScripts",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, Type.EmptyTypes, null);
        if (restart != null)
            restart.Invoke(null, null);
        else
            EditorApplication.OpenProject(Directory.GetCurrentDirectory());
    }

    /// <summary>
    /// The exporter asset (the same one Create > HSEMods/Exporter makes), unless the project already has one. Found by
    /// name: its assembly uses Latios, and this one must not, or it would be skipped along with it before the defines.
    /// </summary>
    private static void CreatePackageExporter()
    {
        var type = Type.GetType(PACKAGE_EXPORTER_TYPE);
        if (type == null)
        {
            Debug.LogWarning("[Package Setup] " + PACKAGE_EXPORTER_TYPE + " not loaded - package exporter not created");
            return;
        }
        if (AssetDatabase.FindAssets("t:" + type.Name).Length > 0)
            return;
        var exporter = ScriptableObject.CreateInstance(type);
        AssetDatabase.CreateAsset(exporter, PACKAGE_EXPORTER_ASSET);
        Debug.Log("[Package Setup] package exporter created: " + PACKAGE_EXPORTER_ASSET);
    }

    /// <summary>Adds the missing ones; true when anything changed (a recompile and domain reload follow).</summary>
    private static bool ApplyScriptingDefines()
    {
        bool changed = false;
        foreach (var pair in RequiredDefines)
        {
            var current = PlayerSettings.GetScriptingDefineSymbols(pair.Key)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => d.Length > 0)
                .ToList();
            var missing = pair.Value.Where(d => !current.Contains(d)).ToList();
            if (missing.Count == 0)
                continue;
            current.AddRange(missing);
            PlayerSettings.SetScriptingDefineSymbols(pair.Key, string.Join(";", current));
            Debug.Log("[Package Setup] " + pair.Key.TargetName + " defines added: " + string.Join(", ", missing));
            changed = true;
        }
        return changed;
    }

    public static void MarkSetupComplete()
    {
        try
        {
            File.WriteAllText(SETUP_DONE_MARKER,
                "HSEverse project setup done " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + Environment.NewLine +
                "Delete this file to run the setup again on the next editor load." + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Package Setup] could not write " + SETUP_DONE_MARKER + ": " + e.Message);
        }
    }
}

public class PackageSetupWindow : EditorWindow
{
    private Texture2D m_Logo;
    private Vector2 m_ScrollPosition;
    [SerializeField] private bool m_Started; // survives the domain reload the import causes

    void OnEnable()
    {
        m_Logo = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Packages/com.hseverse.mods/Resources/Logo.png");
    }

    void OnInspectorUpdate()
    {
        if (PackageSetupManager.SetupRunning)
        {
            Repaint();
            return;
        }
        if (m_Started)
        {
            m_Started = false;
            Close();
        }
    }

    void OnGUI()
    {
        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

        if (m_Logo != null)
        {
            GUILayout.Label(m_Logo, GUILayout.MaxHeight(100), GUILayout.ExpandWidth(true));
        }

        EditorGUILayout.LabelField("Package Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("The setup runs by itself once, when the package is added to a project. Apply Settings runs it again: the scripting defines are added, the TMP and HSEverse essentials are imported (if missing), then the project settings the metaverse needs are applied. The editor may reload scripts along the way; the window closes when everything is done.", MessageType.Info);

        EditorGUILayout.Space(20);

        bool running = PackageSetupManager.SetupRunning;
        using (new EditorGUI.DisabledScope(running))
        {
            if (GUILayout.Button("Apply Settings", GUILayout.Height(40)))
            {
                m_Started = true;
                PackageSetupManager.ApplyRecommendedSettings();
            }
        }

        if (running)
            EditorGUILayout.LabelField(PackageSetupManager.StageLabel, EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(running))
        {
            if (GUILayout.Button("Close Setup", GUILayout.Height(30)))
            {
                PackageSetupManager.MarkSetupComplete();
                Close();
            }
        }

        EditorGUILayout.EndScrollView();
    }
}
#endif
