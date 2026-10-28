#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using HSE.Editor.Attributes;
using Unity.EditorCoroutines.Editor;
using Unity.Entities;
using Unity.Entities.Build;
using Unity.Entities.Content;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HSE_MAIN.Editor.Mods
{
    [CreateAssetMenu(fileName = "PackageExporter", menuName = "HSEMods/Exporter", order = 1)]
    public class PackageExporterSO : ScriptableObject
    {
        [ReadOnly] public string mainSceneName = "ContentScene";

        [SerializeField][ReadOnly] private string subScenePath = "Assets/Scenes/Main/ContentScene/ContSub.unity";
        [SerializeField][ReadOnly] private string contentScenePath = "Assets/Scenes/Main/ContentScene.unity";

        [HideInInspector][Delayed] private string guid;

        [Header("Post Publish")]
        [SerializeField] private bool _createZipAfterPublish = true;

        [SerializeField] private bool _debugOptions = false;

        [Header("Button Labels")]
        private string _btnSetBuildPathLabel = "Set Build Path";
        private string _btnSelectPackagesLabel = "Select .unitypackage Folder";
        private string _btnProcessAllLabel = "Process Packages + Build All Platforms";
        private string _btnBuildAllWithPkgLabel = "Build All Platforms + Export Subscene Package";
        private string _btnBuildAllDebugLabel = "Build All Platforms (Debug)";
        private string _btnBuildAllLabel = "Build All Platforms";
        private string _btnExportSubsceneLabel = "Export Subscene Package";
        private string _btnBuildCurrentLabel = "Build Current Platform";
        private string _btnBuildAndroidLabel = "Build Android";
        private string _btnBuildWindowsLabel = "Build Windows";
        private string _btnBuildMacLabel = "Build macOS";
        private string _btnOpenLogLabel = "Open Build Log Folder";

        [Space(12)]
        [ReadOnly][SerializeField][Delayed] private string buildPath;

        [NamedButton(nameof(SelectBuildPath), LabelMemberName = nameof(_btnSetBuildPathLabel))]
        public bool setBuildPath;

        [Space(12)]
        [ReadOnly][SerializeField][Delayed] private string packagesFolderPath;

        [NamedButton(nameof(SelectUnitypackagesFolder), LabelMemberName = nameof(_btnSelectPackagesLabel))]
        public bool selectPackagesFolder;

        [Space(12)]
        [NamedButton(nameof(BuildForCurrentPlatform), LabelMemberName = nameof(_btnBuildCurrentLabel))]
        public bool buildForCurrentPlatform;

        [NamedButton(nameof(BuildForAndroid), LabelMemberName = nameof(_btnBuildAndroidLabel))]
        public bool buildForAndroid;

        [NamedButton(nameof(BuildForWindows), LabelMemberName = nameof(_btnBuildWindowsLabel))]
        public bool buildForWindows;

        [NamedButton(nameof(BuildForMac), LabelMemberName = nameof(_btnBuildMacLabel))]
        public bool buildForMac;

        [NamedButton(nameof(BuildAllPlatforms), LabelMemberName = nameof(_btnBuildAllLabel))]
        public bool buildAllPlatforms;

        [Space(12)]
        [NamedButton(nameof(ExportSubscenePackageToBuildRoot), LabelMemberName = nameof(_btnExportSubsceneLabel))]
        public bool exportSubscenePackage;

        [Space(12)]
        [NamedButton(nameof(BuildAllPlatformsAndExportSubscenePackage), LabelMemberName = nameof(_btnBuildAllWithPkgLabel))]
        public bool buildForAllPlatformsWithPackage;

        [NamedButton(nameof(BuildAllPlatformsDebug), LabelMemberName = nameof(_btnBuildAllDebugLabel))]
        public bool buildForAllPlatformsNoPackage;

        [NamedButton(nameof(OpenBuildLogFolder), LabelMemberName = nameof(_btnOpenLogLabel))]
        public bool openBuildLog;

        [NamedButton(nameof(ProcessPackagesAndBuildAllPlatforms), LabelMemberName = nameof(_btnProcessAllLabel))]
        public bool processPackagesAndBuild;

        private const string PREF_PREFIX = "HSE_PackageExporterSO_";

        private readonly Queue<string> _packageQueue = new Queue<string>();
        private bool _isProcessing;
        private EditorCoroutine _processingCoroutine;

        private AssetDatabase.ImportPackageCallback _onImportCompleted;
        private AssetDatabase.ImportPackageFailedCallback _onImportFailed;

        public bool DebugOptions => _debugOptions;

        private void OnEnable()
        {
            RestorePrefsIfNeeded();

            if (!string.IsNullOrWhiteSpace(buildPath) || !string.IsNullOrWhiteSpace(packagesFolderPath))
                EditorUtility.SetDirty(this);
        }

        public void SelectBuildPath()
        {
            var folder = EditorUtility.OpenFolderPanel(
                "Select Build To Publish",
                Path.GetDirectoryName(Application.dataPath),
                "Builds");

            if (string.IsNullOrEmpty(folder))
                return;

            buildPath = Path.GetFullPath(folder);

            PersistPrefs();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public void SelectUnitypackagesFolder()
        {
            var folder = EditorUtility.OpenFolderPanel(
                "Select Packages Folder",
                Path.GetDirectoryName(Application.dataPath),
                "");

            if (string.IsNullOrEmpty(folder))
                return;

            packagesFolderPath = Path.GetFullPath(folder);

            PersistPrefs();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        // Every button routes through BuildForAllPlatforms so that preflight, the settings snapshot, the run log and
        // the per-platform isolation apply to a one-platform build exactly as they do to a full publish.
        public void BuildForCurrentPlatform() => BuildOne(EditorUserBuildSettings.activeBuildTarget);
        public void BuildForAndroid() => BuildOne(BuildTarget.Android);
        public void BuildForWindows() => BuildOne(BuildTarget.StandaloneWindows64);
        public void BuildForMac() => BuildOne(BuildTarget.StandaloneOSX);

        private void BuildOne(BuildTarget target) =>
            BuildForAllPlatforms(exportSubscenePackage: false, outputPath: null, exportPackageName: null, platforms: new[] { target });

        public void BuildAllPlatforms()
        {
            BuildForAllPlatforms(
                exportSubscenePackage: false,
                outputPath: null,
                exportPackageName: null,
                platforms: new[] { BuildTarget.Android, BuildTarget.StandaloneWindows64, BuildTarget.StandaloneOSX }
            );
        }

        private void BuildAllPlatformsAndExportSubscenePackage()
        {
            var root = ResolvePublishRootFolder();
            BuildForAllPlatforms(
                exportSubscenePackage: true,
                outputPath: null,
                exportPackageName: GetFolderName(root),
                platforms: new[] { BuildTarget.Android, BuildTarget.StandaloneWindows64, BuildTarget.StandaloneOSX }
            );
        }

        private void BuildAllPlatformsDebug()
        {
            BuildForAllPlatforms(
                exportSubscenePackage: false,
                outputPath: null,
                exportPackageName: null,
                platforms: new[] { BuildTarget.Android, BuildTarget.StandaloneWindows64, BuildTarget.StandaloneOSX }
            );
        }

        private void ExportSubscenePackageToBuildRoot()
        {
            var root = ResolvePublishRootFolder();
            ExportSubscenePackage(root, exportName: GetFolderName(root));
        }

        public void ProcessPackagesAndBuildAllPlatforms()
        {
            RestorePrefsIfNeeded();

            if (_isProcessing)
            {
                Debug.LogWarning("[PackageExporter] Already processing.");
                return;
            }

            if (string.IsNullOrWhiteSpace(buildPath))
            {
                Debug.LogError("BuildPath is empty. Use Set Build Path first.");
                return;
            }

            if (string.IsNullOrWhiteSpace(packagesFolderPath) || !Directory.Exists(packagesFolderPath))
            {
                Debug.LogError("No packages folder selected or folder does not exist.");
                return;
            }

            var packageFiles = Directory.GetFiles(packagesFolderPath, "*.unitypackage");
            if (packageFiles == null || packageFiles.Length == 0)
            {
                Debug.LogError("No .unitypackage files found in selected folder");
                return;
            }

            Array.Sort(packageFiles, StringComparer.OrdinalIgnoreCase);

            _packageQueue.Clear();
            for (int i = 0; i < packageFiles.Length; i++)
                _packageQueue.Enqueue(packageFiles[i]);

            Debug.Log($"[PackageExporter] Queued {_packageQueue.Count} packages from: {packagesFolderPath}");

            _processingCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(ProcessQueueCoroutine());
        }

        private IEnumerator ProcessQueueCoroutine()
        {
            _isProcessing = true;

            string restoreScenePath = "";
            try
            {
                var active = SceneManager.GetActiveScene();
                restoreScenePath = active.IsValid() ? active.path : "";
            }
            catch { }

            var originalTarget = EditorUserBuildSettings.activeBuildTarget;

            bool assembliesLocked = false;

            try
            {
                EditorApplication.LockReloadAssemblies();
                assembliesLocked = true;

                var outputRoot = ResolvePublishRootFolder();
                int total = _packageQueue.Count;
                int idx = 0;

                while (_packageQueue.Count > 0)
                {
                    idx++;
                    var packagePath = _packageQueue.Dequeue();

                    if (!Application.isBatchMode)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Package Exporter",
                            $"Processing {idx}/{total}\n{Path.GetFileName(packagePath)}",
                            total <= 0 ? 0f : (idx - 1f) / total);
                    }

                    yield return RunSafe(ProcessSinglePackageCoroutine(
                        packagePath: packagePath,
                        outputRoot: outputRoot,
                        platforms: null,
                        logPath: null
                    ));

                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    yield return null;
                }

                Debug.Log("[PackageExporter] Finished processing all packages");
            }
            finally
            {
                if (!Application.isBatchMode)
                    EditorUtility.ClearProgressBar();

                // Verified restore: a silently failed switch used to leave the editor on another platform's target.
                try { EnsureActiveBuildTarget(originalTarget, throwOnFailure: false); }
                catch (Exception ex) { Debug.LogWarning("[PackageExporter] could not restore the build target: " + ex.Message); }

                if (assembliesLocked)
                {
                    try { EditorApplication.UnlockReloadAssemblies(); } catch { }
                }

                if (!Application.isBatchMode)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(restoreScenePath) && File.Exists(restoreScenePath))
                            EditorSceneManager.OpenScene(restoreScenePath, OpenSceneMode.Single);
                    }
                    catch { }
                }

                _isProcessing = false;
                _processingCoroutine = null;
            }
        }

        private static IEnumerator RunSafe(IEnumerator inner)
        {
            if (inner == null)
                yield break;

            while (true)
            {
                bool moved = false;
                object cur = null;

                try
                {
                    moved = inner.MoveNext();
                    if (moved) cur = inner.Current;
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    yield break;
                }

                if (!moved)
                    yield break;

                yield return cur;
            }
        }

        private IEnumerator ProcessSinglePackageCoroutine(
            string packagePath,
            string outputRoot,
            BuildTarget[] platforms,
            string logPath)
        {
            RestorePrefsIfNeeded();

            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                yield break;

            string packageName = Path.GetFileNameWithoutExtension(packagePath);

            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                Debug.LogError("Publish root path invalid.");
                WriteLogLine(logPath, packageName, "ALL", false, "Invalid output root");
                yield break;
            }

            using (new ScopedLiveConversionDisabled())
            {
                var packageOut = Path.Combine(outputRoot, packageName);
                RecreateDirectory(packageOut);

                Debug.Log($"[PackageExporter] Importing: {packageName}");

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                yield return null;

                bool importComplete = false;
                string importError = null;

                _onImportCompleted = _ => importComplete = true;
                _onImportFailed = (_, e) => { importError = e; importComplete = true; };

                AssetDatabase.importPackageCompleted += _onImportCompleted;
                AssetDatabase.importPackageFailed += _onImportFailed;

                try
                {
                    AssetDatabase.ImportPackage(packagePath, false);

                    float timeout = 300f;
                    float start = Time.realtimeSinceStartup;

                    while (!importComplete && (Time.realtimeSinceStartup - start) < timeout)
                        yield return null;

                    if (!importComplete)
                    {
                        Debug.LogError($"Package '{packageName}' import timed out");
                        WriteLogLine(logPath, packageName, "IMPORT", false, "Timed out");
                        yield break;
                    }

                    if (!string.IsNullOrEmpty(importError))
                    {
                        Debug.LogError($"Package '{packageName}' import failed: {importError}");
                        WriteLogLine(logPath, packageName, "IMPORT", false, importError);
                        yield break;
                    }
                }
                finally
                {
                    AssetDatabase.importPackageCompleted -= _onImportCompleted;
                    AssetDatabase.importPackageFailed -= _onImportFailed;
                    _onImportCompleted = null;
                    _onImportFailed = null;
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                float waitStart = Time.realtimeSinceStartup;
                while (EditorApplication.isCompiling || EditorApplication.isUpdating || AssetDatabase.IsAssetImportWorkerProcess())
                {
                    if (Time.realtimeSinceStartup - waitStart > 600f)
                        break;
                    yield return null;
                }

                var contentScene = EditorSceneManager.OpenScene(contentScenePath, OpenSceneMode.Single);
                if (!contentScene.IsValid())
                {
                    Debug.LogError($"Failed to open content scene at '{contentScenePath}' for package '{packageName}'");
                    WriteLogLine(logPath, packageName, "SCENE", false, "Failed to open content scene");
                    yield break;
                }

                var subScene = FindSubSceneInLoadedScene();
                if (subScene == null)
                {
                    Debug.LogError($"SubScene not found after importing '{packageName}'. Expected '{subScenePath}' or a SubScene named 'ContSub'.");
                    WriteLogLine(logPath, packageName, "SUBSCENE", false, "SubScene not found");
                    yield break;
                }

                SubSceneUtility.EditScene(subScene);
                yield return null;

                EditorSceneManager.SaveScene(contentScene);
                AssetDatabase.SaveAssets();

                var targets = platforms != null && platforms.Length > 0
                    ? platforms
                    : new[] { BuildTarget.Android, BuildTarget.StandaloneWindows64, BuildTarget.StandaloneOSX };

                bool allOk = true;

                for (int i = 0; i < targets.Length; i++)
                {
                    var t = targets[i];
                    bool ok = false;
                    string msg;

                    try
                    {
                        BuildForPlatform(t, packageOut);
                        ok = true;
                        msg = "OK";
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        msg = ex.GetType().Name + ": " + ex.Message;
                        allOk = false;
                        Debug.LogError(ex);
                    }

                    WriteLogLine(logPath, packageName, GetPlatformFolderName(t), ok, msg);
                    yield return null;
                }

                WriteLogLine(logPath, packageName, "ALL", allOk, allOk ? "OK" : "FAILED");
                Debug.Log($"[PackageExporter] Done: {packageName} ({(allOk ? "OK" : "FAILED")})");
            }
        }

        private SubScene FindSubSceneInLoadedScene()
        {
            var subs = UnityEngine.Object.FindObjectsOfType<SubScene>(true);
            if (subs == null || subs.Length == 0)
                return null;

            for (int i = 0; i < subs.Length; i++)
            {
                var s = subs[i];
                if (s == null) continue;

#if UNITY_2022_2_OR_NEWER
                var sceneAsset = s.SceneAsset;
                if (sceneAsset != null)
                {
                    var p = AssetDatabase.GetAssetPath(sceneAsset);
                    if (string.Equals(p, subScenePath, StringComparison.OrdinalIgnoreCase))
                        return s;
                }
#endif
                if (string.Equals(s.name, "ContSub", StringComparison.OrdinalIgnoreCase))
                    return s;
            }

            return subs[0];
        }

        public void BuildForAllPlatforms(bool exportSubscenePackage, string outputPath, string exportPackageName, BuildTarget[] platforms)
        {
            var originalTarget = EditorUserBuildSettings.activeBuildTarget;

            bool assembliesLocked = false;
            var failures = new List<string>();
            ProjectSettingsSnapshot snapshot = default;
            bool snapshotTaken = false;

            BeginRunLog(outputPath, platforms);
            var runClock = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                Preflight(platforms, outputPath);

                snapshot = ProjectSettingsSnapshot.Capture();
                snapshotTaken = true;

                EditorApplication.LockReloadAssemblies();
                assembliesLocked = true;

                for (int i = 0; i < platforms.Length; i++)
                {
                    // One platform failing must not cancel the others: record it and carry on.
                    var platformClock = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        BuildForPlatform(platforms[i], outputPath);
                        platformClock.Stop();
                        LogStep(platforms[i].ToString(), true, $"ok in {FormatSpan(platformClock)}");
                    }
                    catch (Exception ex)
                    {
                        platformClock.Stop();
                        failures.Add($"{platforms[i]}: {ex.Message}");
                        LogStep(platforms[i].ToString(), false, $"FAILED after {FormatSpan(platformClock)} - {ex.Message}");
                        Debug.LogError($"[PackageExporter] {platforms[i]} failed: {ex}");
                    }
                }

                if (exportSubscenePackage)
                {
                    try
                    {
                        ExportSubscenePackage(outputPath ?? ResolvePublishRootFolder(), exportPackageName);
                        LogStep("subscene-package", true, "exported");
                    }
                    catch (Exception ex)
                    {
                        failures.Add("subscene package: " + ex.Message);
                        LogStep("subscene-package", false, ex.Message);
                        Debug.LogError("[PackageExporter] subscene package export failed: " + ex);
                    }
                }
            }
            catch (Exception ex)
            {
                failures.Add(ex.Message);
                LogStep("run", false, ex.Message);
                Debug.LogError("[PackageExporter] " + ex);
            }
            finally
            {
                if (snapshotTaken)
                {
                    // Restores build target, colour space, scripting backends, architecture, macOS version and the
                    // scripting defines this exporter adds, so a publish does not leave the author's project altered.
                    try { snapshot.Restore(); }
                    catch (Exception ex) { Debug.LogWarning("[PackageExporter] could not restore project settings: " + ex.Message); }
                }
                else
                {
                    try { EnsureActiveBuildTarget(originalTarget, throwOnFailure: false); } catch { }
                }

                if (assembliesLocked)
                {
                    try { EditorApplication.UnlockReloadAssemblies(); } catch { }
                }

                runClock.Stop();
                if (failures.Count == 0)
                {
                    LogStep("ALL", true, $"finished in {FormatSpan(runClock)}");
                    Debug.Log($"Finished building rooms in {FormatSpan(runClock)}");
                }
                else
                {
                    LogStep("ALL", false, $"finished in {FormatSpan(runClock)} with {failures.Count} failure(s): {string.Join(" | ", failures)}");
                    Debug.LogError($"[PackageExporter] finished with {failures.Count} failure(s): {string.Join(" | ", failures)}");
                }
                LastRunFailed = failures.Count > 0;
                EndRunLog();
            }
        }

        /// <summary>True when the last <see cref="BuildForAllPlatforms"/> recorded at least one failure. Read by the
        /// headless job to pick its exit code.</summary>
        public bool LastRunFailed { get; private set; }

        public void ExportSubscenePackage(string outputFolder, string exportName)
        {
#if !HSE_BUILD
            SceneSettingsSync.SyncForExport(contentScenePath);
#endif
            if (!File.Exists(subScenePath))
            {
                Debug.LogError($"Subscene not found at path: {subScenePath}");
                return;
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                Debug.LogError("Export output folder is empty.");
                return;
            }

            Directory.CreateDirectory(outputFolder);

            string[] dependencies = AssetDatabase.GetDependencies(subScenePath, true);

            string safeName = string.IsNullOrWhiteSpace(exportName) ? GetFolderName(outputFolder) : exportName.Trim();
            var packagePath = Path.Combine(outputFolder, $"{safeName}.unitypackage");

            AssetDatabase.ExportPackage(dependencies, packagePath, ExportPackageOptions.Default);
            Debug.Log($"Subscene package exported to: {packagePath}");

            AssetDatabase.Refresh();
        }

        private void BuildForPlatform(BuildTarget target, string outPutFolder)
        {
            using (new ScopedActiveInputHandlingNewOnly())
            {
                EnsureActiveBuildTarget(target, throwOnFailure: true);

                ApplyCommonBuildSettings();

                switch (target)
                {
                    case BuildTarget.Android: ApplyAndroidBuildSettings(); break;
                    case BuildTarget.StandaloneWindows64: ApplyWindowsBuildSettings(); break;
                    case BuildTarget.StandaloneOSX: ApplyMacBuildSettings(); break;
                }

                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var tmpBuildFolder = Path.Combine(projectRoot, "Library", "ContentUpdateBuildDir", PlayerSettings.productName);

                var instance = DotsGlobalSettings.Instance;
                if (instance == null)
                    throw new Exception("DotsGlobalSettings.Instance is null");

                var playerGuid = instance.GetPlayerType() == DotsGlobalSettings.PlayerType.Client
                    ? instance.GetClientGUID()
                    : instance.GetServerGUID();

                if (!playerGuid.IsValid)
                    throw new Exception("Invalid Player GUID");

                var subSceneGuids = new HashSet<Unity.Entities.Hash128>();
                AddSubSceneGuids( subSceneGuids, contentScenePath);

                _graphNodeErrors.Clear();
                RemoteContentCatalogBuildUtility.BuildContent(subSceneGuids, playerGuid, target, tmpBuildFolder);
                if (_graphNodeErrors.Count > 0)
                    throw new Exception("Code graph failed to process: " + string.Join(", ", _graphNodeErrors) +
                                        ". Fix the graph and build again.");

                string publishRoot = string.IsNullOrWhiteSpace(outPutFolder) ? ResolvePublishRootFolder() : Path.GetFullPath(outPutFolder);
                if (string.IsNullOrWhiteSpace(publishRoot))
                    throw new Exception("Publish root folder is empty.");

                var platformFolderName = GetPlatformFolderName(target);
                var publishPlatformFolder = Path.Combine(publishRoot, platformFolderName);

                RecreateDirectory(publishPlatformFolder);

                RemoteContentCatalogBuildUtility.PublishContent(tmpBuildFolder, publishPlatformFolder, f => new[] { "root" }, true);

                if (_createZipAfterPublish)
                    CreateOrReplaceZipForPlatformFolder(publishPlatformFolder);

                Debug.Log($"Successfully built for platform: {target}");
            }
        }

        private static string GetPlatformFolderName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android: return "Android";
                case BuildTarget.StandaloneWindows64: return "Windows";
                case BuildTarget.StandaloneOSX: return "MacOS";
                default: return target.ToString();
            }
        }

        private void ApplyCommonBuildSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Gamma;

            PlayerSettings.GetScriptingDefineSymbols(
                NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)),
                out var defines);

            var list = defines.ToList();
            list.Add("ENTITY_STORE_V1");
            list.Add("UNITY_BURST_EXPERIMENTAL_ATOMIC_INTRINSICS");
            list.Add("LATIOS_TRANSFORMS_UNITY");

            PlayerSettings.SetScriptingDefineSymbols(
                NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)),
                list.Distinct().ToArray());
        }

        private static void ApplyAndroidBuildSettings()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        }

        private static void ApplyWindowsBuildSettings()
        {
            // packages are built with Mono (as macOS already was), not IL2CPP
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        private static void ApplyMacBuildSettings()
        {
            PlayerSettings.macOS.targetOSVersion = "11.0";
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 2);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        private static void AddSubSceneGuids(HashSet<Unity.Entities.Hash128> subSceneGuids, string scenePath)
        {
            var sceneGuid = AssetDatabase.GUIDFromAssetPath(scenePath);
            var subScenesHashes = EditorEntityScenes.GetSubScenes(sceneGuid);

            Debug.Log("ge " + subScenesHashes.Length + " " + scenePath);
            foreach (var hash in subScenesHashes)
                subSceneGuids.Add(hash);
        }

        private string ResolvePublishRootFolder()
        {
            RestorePrefsIfNeeded();

            var p = (buildPath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(p))
                return "";

            // A path saved on Windows ("G:\\...") is not rooted on macOS / Linux: it used to be appended to <project>/Builds
            // and became a folder literally named "G:\\..." that no room loader can read. Treat it as "not set".
            if (Application.platform != RuntimePlatform.WindowsEditor &&
                p.Length >= 3 && char.IsLetter(p[0]) && p[1] == ':' && (p[2] == '\\' || p[2] == '/'))
            {
                Debug.LogWarning("[PackageExporter] The build path \"" + p + "\" was set on Windows - press \"Set Build Path\" on this machine.");
                return "";
            }

            if (Path.IsPathRooted(p))
                return Path.GetFullPath(p);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, "Builds", p);
        }

        private static string GetFolderName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "Build";

            var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? "Build" : name;
        }

        private static void RecreateDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch { }

            Directory.CreateDirectory(path);
        }

        private static void CreateOrReplaceZipForPlatformFolder(string platformFolderPath)
        {
            if (string.IsNullOrWhiteSpace(platformFolderPath) || !Directory.Exists(platformFolderPath))
                return;

            var parent = Directory.GetParent(platformFolderPath)?.FullName;
            if (string.IsNullOrWhiteSpace(parent))
                return;

            var folderName = GetFolderName(platformFolderPath);
            var zipPath = Path.Combine(parent, $"{folderName}.zip");

            try
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                ZipFile.CreateFromDirectory(platformFolderPath, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to create zip for '{platformFolderPath}': {ex}");
            }
        }

        private void PersistPrefs()
        {
            try
            {
                EditorPrefs.SetString(GetPrefsKey("buildPath"), buildPath ?? "");
                EditorPrefs.SetString(GetPrefsKey("packagesFolderPath"), packagesFolderPath ?? "");
            }
            catch { }
        }

        private void RestorePrefsIfNeeded()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(buildPath) && !string.IsNullOrWhiteSpace(packagesFolderPath))
                    return;

                if (string.IsNullOrWhiteSpace(buildPath))
                    buildPath = EditorPrefs.GetString(GetPrefsKey("buildPath"), buildPath ?? "");

                if (string.IsNullOrWhiteSpace(packagesFolderPath))
                    packagesFolderPath = EditorPrefs.GetString(GetPrefsKey("packagesFolderPath"), packagesFolderPath ?? "");
            }
            catch { }
        }

        private string GetPrefsKey(string suffix)
        {
            var assetPath = AssetDatabase.GetAssetPath(this);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(assetGuid))
                assetGuid = name;

            return $"{PREF_PREFIX}{assetGuid}_{suffix}";
        }

        // ---------------------------------------------------------------- build target
        /// <summary>Switch to <paramref name="target"/> and verify it actually happened. The plain call returns a bool
        /// that the old code discarded, and the switch is deferred while assemblies are locked, so the active target is
        /// re-read afterwards: without this an author missing a platform module silently published one platform's
        /// content into another platform's folder.</summary>
        private static void EnsureActiveBuildTarget(BuildTarget target, bool throwOnFailure)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target)
                return;

            var group = BuildPipeline.GetBuildTargetGroup(target);

            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                string msg = $"Build target {target} is not installed in this editor. Install the module in Unity Hub, or build the other platforms only.";
                if (throwOnFailure) throw new Exception(msg);
                Debug.LogWarning("[PackageExporter] " + msg);
                return;
            }

            bool requested = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            if (!requested)
            {
                string msg = $"Unity refused to switch the active build target to {target}.";
                if (throwOnFailure) throw new Exception(msg);
                Debug.LogWarning("[PackageExporter] " + msg);
                return;
            }

            // The switch reports success but the property is read back too, because that is what silently produced a
            // package with one platform's content in another platform's folder. Only a warning: the switch runs while
            // assemblies are locked, and a read-back that lags must not fail a build that is otherwise fine.
            if (EditorUserBuildSettings.activeBuildTarget != target)
                Debug.LogWarning($"[PackageExporter] asked for build target {target} but the editor reports {EditorUserBuildSettings.activeBuildTarget}; the published content for {target} may be wrong.");
        }

        // ---------------------------------------------------------------- project settings
        /// <summary>Everything a publish writes into the author's project. Captured before the run and put back after,
        /// including after a failure: the run used to leave the project on another build target, in Gamma colour space,
        /// with a swapped scripting backend and three extra defines.</summary>
        private struct ProjectSettingsSnapshot
        {
            private BuildTarget _target;
            private ColorSpace _colorSpace;
            private ScriptingImplementation _standaloneBackend, _androidBackend;
            private int _standaloneArchitecture;
            private string _macOsVersion;
            private string[] _standaloneDefines, _androidDefines;

            public static ProjectSettingsSnapshot Capture()
            {
                var snap = new ProjectSettingsSnapshot
                {
                    _target = EditorUserBuildSettings.activeBuildTarget,
                    _colorSpace = PlayerSettings.colorSpace,
                    _standaloneBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone),
                    _androidBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
                    _standaloneArchitecture = PlayerSettings.GetArchitecture(NamedBuildTarget.Standalone),
                    _macOsVersion = PlayerSettings.macOS.targetOSVersion,
                };
                PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone, out snap._standaloneDefines);
                PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out snap._androidDefines);
                return snap;
            }

            public void Restore()
            {
                PlayerSettings.colorSpace = _colorSpace;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, _standaloneBackend);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, _androidBackend);
                PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, _standaloneArchitecture);
                PlayerSettings.macOS.targetOSVersion = _macOsVersion;
                if (_standaloneDefines != null) PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, _standaloneDefines);
                if (_androidDefines != null) PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, _androidDefines);
                EnsureActiveBuildTarget(_target, throwOnFailure: false);
            }
        }

        // ---------------------------------------------------------------- preflight
        /// <summary>Everything that can be checked in a second, checked before a build that costs minutes: an author
        /// used to wait through a full publish to learn that a path was unset or the subscene was never saved.</summary>
        private void Preflight(BuildTarget[] platforms, string outputPath)
        {
            #if !HSE_BUILD
            SceneSettingsSync.SyncForExport(contentScenePath); // Lighting window -> the SubScene's scene-settings authorings
            #endif
            var problems = new List<string>();

            if (platforms == null || platforms.Length == 0)
                problems.Add("No platforms selected.");

            string publishRoot = string.IsNullOrWhiteSpace(outputPath) ? ResolvePublishRootFolder() : outputPath;
            if (string.IsNullOrWhiteSpace(publishRoot))
                problems.Add("Build path is not set - press \"Set Build Path\" first.");
            else
            {
                try { Directory.CreateDirectory(publishRoot); }
                catch (Exception ex) { problems.Add($"Build path is not writable ({publishRoot}): {ex.Message}"); }
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(contentScenePath) == null)
                problems.Add($"Content scene not found at {contentScenePath}.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath) == null)
                problems.Add($"Subscene not found at {subScenePath}.");

            // The bake reads the subscene from DISK, so anything unsaved would silently not ship.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty && !string.IsNullOrEmpty(scene.path))
                    problems.Add($"Scene \"{scene.name}\" has unsaved changes - save it before building.");
            }

            if (problems.Count == 0)
            {
                var guids = new HashSet<Unity.Entities.Hash128>();
                try { AddSubSceneGuids(guids, contentScenePath); }
                catch (Exception ex) { problems.Add("Could not read the subscenes of the content scene: " + ex.Message); }
                if (guids.Count == 0)
                    problems.Add("The content scene contains no subscene to build.");
            }

            if (problems.Count > 0)
            {
                string msg = "Cannot start the build:\n  - " + string.Join("\n  - ", problems);
                LogStep("preflight", false, string.Join(" | ", problems));
                throw new Exception(msg);
            }

            LogStep("preflight", true, "ok");
        }

        // ---------------------------------------------------------------- run log
        private string _runLogPath;
        private static readonly List<string> _graphNodeErrors = new List<string>();
        private const string GraphNodeFailureMarker = "node is not processed";

        /// <summary>Opens the per-run log and starts capturing the console into it, so an author can send one file
        /// instead of a screenshot. The log lives next to the published output.</summary>
        private void BeginRunLog(string outputPath, BuildTarget[] platforms)
        {
            try
            {
                string root = string.IsNullOrWhiteSpace(outputPath) ? ResolvePublishRootFolder() : outputPath;
                if (string.IsNullOrWhiteSpace(root))
                    root = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "BuildLogs");
                string dir = Path.Combine(root, "_buildlogs");
                Directory.CreateDirectory(dir);
                _runLogPath = Path.Combine(dir, $"build_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                File.AppendAllText(_runLogPath,
                    $"HSE content build{Environment.NewLine}" +
                    $"started   {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
                    $"unity     {Application.unityVersion}{Environment.NewLine}" +
                    $"project   {Application.dataPath}{Environment.NewLine}" +
                    $"scene     {contentScenePath}{Environment.NewLine}" +
                    $"output    {root}{Environment.NewLine}" +
                    $"platforms {string.Join(", ", platforms ?? new BuildTarget[0])}{Environment.NewLine}" +
                    new string('-', 72) + Environment.NewLine);
            }
            catch { _runLogPath = null; }

            _graphNodeErrors.Clear();
            Application.logMessageReceived -= OnConsoleMessage;
            Application.logMessageReceived += OnConsoleMessage;
        }

        private void EndRunLog()
        {
            Application.logMessageReceived -= OnConsoleMessage;
            if (string.IsNullOrEmpty(_runLogPath))
                return;
            try
            {
                File.AppendAllText(_runLogPath, new string('-', 72) + Environment.NewLine +
                                                $"ended     {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
                Debug.Log("[PackageExporter] build log: " + _runLogPath);
            }
            catch { }
        }

        /// <summary>Console capture. Never logs from inside the handler (that would recurse) and records the code
        /// graph's own node failures, which are only reported as console errors from inside the bake.</summary>
        private void OnConsoleMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                if (condition != null && condition.Contains(GraphNodeFailureMarker) && !_graphNodeErrors.Contains(condition))
                    _graphNodeErrors.Add(condition.Trim());
            }
            if (string.IsNullOrEmpty(_runLogPath))
                return;
            if (type == LogType.Log)
                return;
            try
            {
                File.AppendAllText(_runLogPath, $"{DateTime.Now:HH:mm:ss} [{type}] {condition}{Environment.NewLine}");
            }
            catch { }
        }

        private void LogStep(string stage, bool ok, string message)
        {
            if (string.IsNullOrEmpty(_runLogPath))
                return;
            try
            {
                File.AppendAllText(_runLogPath,
                    $"{DateTime.Now:HH:mm:ss} [{(ok ? "OK  " : "FAIL")}] {stage,-18} {message}{Environment.NewLine}");
            }
            catch { }
        }

        private static string FormatSpan(System.Diagnostics.Stopwatch sw) =>
            sw.Elapsed.TotalMinutes >= 1.0 ? $"{(int)sw.Elapsed.TotalMinutes}m {sw.Elapsed.Seconds}s" : $"{sw.Elapsed.TotalSeconds:0.0}s";

        /// <summary>Opens Explorer/Finder on the newest build log, selected. <see cref="_runLogPath"/> does not survive
        /// a domain reload (a platform switch causes one), so the log folders are searched as well - the same two
        /// places <see cref="BeginRunLog"/> writes to.</summary>
        private void OpenBuildLogFolder()
        {
            string newest = !string.IsNullOrEmpty(_runLogPath) && File.Exists(_runLogPath) ? _runLogPath : null;
            DateTime newestTime = newest != null ? File.GetLastWriteTime(newest) : DateTime.MinValue;
            string root = ResolvePublishRootFolder();
            var dirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(root)) dirs.Add(Path.Combine(root, "_buildlogs"));
            dirs.Add(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "BuildLogs", "_buildlogs"));
            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "build_*.log"))
                {
                    var time = File.GetLastWriteTime(file);
                    if (time <= newestTime) continue;
                    newest = file;
                    newestTime = time;
                }
            }
            if (newest != null)
                EditorUtility.RevealInFinder(newest);
            else
                EditorUtility.DisplayDialog("Build log", "No build log yet. Every build button writes one; run a build first.", "OK");
        }

        private static void WriteLogLine(string logPath, string packageName, string platform, bool ok, string message)
        {
            if (string.IsNullOrWhiteSpace(logPath))
                return;

            try
            {
                var dir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                var line = $"{DateTime.UtcNow:O}\t{packageName}\t{platform}\t{(ok ? "OK" : "FAIL")}\t{message}";
                File.AppendAllText(logPath, line + Environment.NewLine);
            }
            catch { }
        }

        private sealed class ScopedLiveConversionDisabled : IDisposable
        {
            private readonly List<(ComponentSystemBase sys, bool enabled)> _changed = new();

            public ScopedLiveConversionDisabled()
            {
                TryDisable("Unity.Scenes.Editor.LiveConversionEditorSystemGroup");
                TryDisable("Unity.Scenes.Editor.EditorSubSceneLiveConversionSystem");
            }

            private void TryDisable(string fullTypeName)
            {
                var t = FindType(fullTypeName);
                if (t == null)
                    return;

                var all = World.All;
                int count = 0;

                try { count = all.Count; } catch { count = 0; }

                for (int i = 0; i < count; i++)
                {
                    World w = null;
                    try { w = all[i]; } catch { w = null; }

                    if (w == null || !w.IsCreated)
                        continue;

                    var sys = GetExistingSystemManagedByType(w, t);
                    if (sys == null)
                        continue;

                    try
                    {
                        var prev = sys.Enabled;
                        if (prev)
                        {
                            sys.Enabled = false;
                            _changed.Add((sys, prev));
                        }
                    }
                    catch { }
                }
            }

            private static ComponentSystemBase GetExistingSystemManagedByType(World w, Type t)
            {
                if (w == null || t == null)
                    return null;

                try
                {
                    var mi = typeof(World).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        .FirstOrDefault(m =>
                            m.Name == "GetExistingSystemManaged" &&
                            m.GetParameters().Length == 1 &&
                            m.GetParameters()[0].ParameterType == typeof(Type));

                    if (mi == null)
                        return null;

                    return mi.Invoke(w, new object[] { t }) as ComponentSystemBase;
                }
                catch
                {
                    return null;
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < _changed.Count; i++)
                {
                    var pair = _changed[i];
                    if (pair.sys == null) continue;
                    try { pair.sys.Enabled = pair.enabled; } catch { }
                }
                _changed.Clear();
            }

            private static Type FindType(string fullName)
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    try
                    {
                        var tt = asms[i].GetType(fullName, throwOnError: false);
                        if (tt != null) return tt;
                    }
                    catch { }
                }
                return null;
            }
        }

        private sealed class ScopedActiveInputHandlingNewOnly : IDisposable
        {
            private const int NewOnly = 1;

            private bool _changed;
            private Action _restore;

            public ScopedActiveInputHandlingNewOnly()
            {
                if (TrySetViaReflection(NewOnly, out var restore1))
                {
                    _changed = true;
                    _restore = restore1;
                    return;
                }

                if (TrySetViaSerializedObject(NewOnly, out var restore2))
                {
                    _changed = true;
                    _restore = restore2;
                    return;
                }

                if (TryPatchProjectSettingsAsset(NewOnly, out var restore3))
                {
                    _changed = true;
                    _restore = restore3;
                    return;
                }
            }

            public void Dispose()
            {
                if (_changed)
                {
                    try { _restore?.Invoke(); } catch { }
                }
            }

            private static bool TrySetViaReflection(int newValue, out Action restore)
            {
                restore = null;

                try
                {
                    var t = typeof(PlayerSettings);
                    var prop = t.GetProperty("activeInputHandler",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);

                    if (prop == null || !prop.CanRead || !prop.CanWrite)
                        return false;

                    var oldObj = prop.GetValue(null, null);
                    if (oldObj == null)
                        return false;

                    int oldInt = Convert.ToInt32(oldObj);

                    if (oldInt == newValue)
                    {
                        restore = () => { };
                        return true;
                    }

                    object newObj = prop.PropertyType.IsEnum
                        ? Enum.ToObject(prop.PropertyType, newValue)
                        : (object)newValue;

                    object oldRestoreObj = prop.PropertyType.IsEnum
                        ? Enum.ToObject(prop.PropertyType, oldInt)
                        : (object)oldInt;

                    prop.SetValue(null, newObj, null);

                    restore = () => prop.SetValue(null, oldRestoreObj, null);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TrySetViaSerializedObject(int newValue, out Action restore)
            {
                restore = null;

                try
                {
                    var ps = Resources.FindObjectsOfTypeAll<PlayerSettings>().FirstOrDefault();
                    if (ps == null)
                        return false;

                    var so = new SerializedObject(ps);

                    SerializedProperty p =
                        so.FindProperty("activeInputHandler") ??
                        so.FindProperty("m_ActiveInputHandler") ??
                        so.FindProperty("activeInputHandling") ??
                        so.FindProperty("m_ActiveInputHandling");

                    if (p == null || p.propertyType != SerializedPropertyType.Integer)
                        return false;

                    int old = p.intValue;
                    if (old == newValue)
                    {
                        restore = () => { };
                        return true;
                    }

                    p.intValue = newValue;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();

                    restore = () =>
                    {
                        try
                        {
                            var so2 = new SerializedObject(ps);
                            var p2 = so2.FindProperty(p.propertyPath);
                            if (p2 != null && p2.propertyType == SerializedPropertyType.Integer)
                            {
                                p2.intValue = old;
                                so2.ApplyModifiedPropertiesWithoutUndo();
                                AssetDatabase.SaveAssets();
                            }
                        }
                        catch { }
                    };

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryPatchProjectSettingsAsset(int newValue, out Action restore)
            {
                restore = null;

                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrWhiteSpace(projectRoot))
                    return false;

                string path = Path.Combine(projectRoot, "ProjectSettings", "ProjectSettings.asset");
                if (!File.Exists(path))
                    return false;

                string original;
                try { original = File.ReadAllText(path); }
                catch { return false; }

                string patched = PatchIntYaml(original, "activeInputHandler", newValue, out bool did1);
                patched = PatchIntYaml(patched, "m_ActiveInputHandler", newValue, out bool did2);

                if (!did1 && !did2)
                    return false;

                try
                {
                    File.WriteAllText(path, patched);
                }
                catch
                {
                    return false;
                }

                AssetDatabase.Refresh();

                restore = () =>
                {
                    try
                    {
                        File.WriteAllText(path, original);
                        AssetDatabase.Refresh();
                    }
                    catch { }
                };

                return true;
            }

            private static string PatchIntYaml(string text, string key, int newValue, out bool changed)
            {
                changed = false;
                if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(key))
                    return text;

                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var ln = lines[i];
                    int idx = ln.IndexOf(key + ":", StringComparison.Ordinal);
                    if (idx < 0)
                        continue;

                    bool okKey = true;
                    for (int k = 0; k < idx; k++)
                    {
                        if (!char.IsWhiteSpace(ln[k])) { okKey = false; break; }
                    }
                    if (!okKey) continue;

                    string prefix = ln.Substring(0, idx + key.Length + 1);
                    lines[i] = prefix + " " + newValue.ToString();
                    changed = true;
                    break;
                }

                return string.Join("\n", lines);
            }
        }

        public IEnumerator RunBatchSinglePackage(string packagePath, string outputRoot, string logPath, BuildTarget[] platforms)
        {
            yield return ProcessSinglePackageCoroutine(packagePath, outputRoot, platforms, logPath);
        }

    }
}
#endif