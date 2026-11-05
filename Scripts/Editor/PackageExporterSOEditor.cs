#if UNITY_EDITOR
using HSE_MAIN.Editor.Mods;
using UnityEditor;

[CustomEditor(typeof(PackageExporterSO))]
public sealed class PackageExporterSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        Draw("mainSceneName");
        Draw("subScenePath");
        EditorGUI.BeginChangeCheck();
        Draw("contentScene");
        if (EditorGUI.EndChangeCheck())
            serializedObject.FindProperty("legacyContentScenePath").stringValue = string.Empty;
        Draw("guid");

        EditorGUILayout.Space(8);
        Draw("_createZipAfterPublish");
        Draw("_debugOptions");

        EditorGUILayout.Space(10);
        Draw("_btnSetBuildPathLabel");
        Draw("_btnSelectPackagesLabel");
        Draw("_btnProcessAllLabel");
        Draw("_btnBuildAllWithPkgLabel");
        Draw("_btnBuildAllDebugLabel");
        Draw("_btnBuildAllLabel");
        Draw("_btnExportSubsceneLabel");
        Draw("_btnBuildCurrentLabel");
        Draw("_btnBuildAndroidLabel");
        Draw("_btnBuildWindowsLabel");
        Draw("_btnBuildMacLabel");

        EditorGUILayout.Space(10);
        Draw("buildPath");
        Draw("setBuildPath");

        EditorGUILayout.Space(8);
        Draw("buildForCurrentPlatform");
        Draw("buildForAndroid");
        Draw("buildForWindows");
        Draw("buildForMac");
        Draw("buildAllPlatforms");

        EditorGUILayout.Space(8);
        Draw("exportSubscenePackage");

        EditorGUILayout.Space(8);
        Draw("openBuildLog");

        var so = (PackageExporterSO)target;
        if (so != null && so.DebugOptions)
        {
            EditorGUILayout.Space(12);
            Draw("packagesFolderPath");
            Draw("selectPackagesFolder");

            EditorGUILayout.Space(8);
            Draw("buildForAllPlatformsWithPackage");
            Draw("buildForAllPlatformsNoPackage");
            Draw("processPackagesAndBuild");
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string prop)
    {
        var p = serializedObject.FindProperty(prop);
        if (p != null)
            EditorGUILayout.PropertyField(p, includeChildren: true);
    }
}
#endif
