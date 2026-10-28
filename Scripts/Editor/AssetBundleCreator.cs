#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public class AssetBundleCreator: EditorWindow
{
    private GameObject targetObject;

    [MenuItem("Tools/Create AssetBundle")]
    public static void ShowWindow()
    {
        GetWindow<AssetBundleCreator>("Asset Bundle Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Asset Bundle Creation", EditorStyles.boldLabel);

        targetObject = (GameObject)EditorGUILayout.ObjectField("3D Model", targetObject, typeof(GameObject), false);

        if (GUILayout.Button("Build AssetBundle"))
        {
            if (targetObject != null)
            {
                BuildBundle(targetObject);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Please select a GameObject", "OK");
            }
        }
    }

    private void BuildBundle(GameObject obj)
    {
        string tempPath = "Assets/TempPrefab.prefab";
        PrefabUtility.SaveAsPrefabAsset(obj, tempPath);

        string bundlePath = "Assets/AssetBundles";
        if (!Directory.Exists(bundlePath))
        {
            Directory.CreateDirectory(bundlePath);
        }

        AssetBundleBuild build = new AssetBundleBuild
        {
            assetBundleName = "modelbundle",
            assetNames = new[] { tempPath }
        };

        BuildPipeline.BuildAssetBundles(bundlePath, new[] { build }, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);

        AssetDatabase.DeleteAsset(tempPath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", $"AssetBundle built at: {Path.GetFullPath(bundlePath)}", "OK");
    }
}
#endif