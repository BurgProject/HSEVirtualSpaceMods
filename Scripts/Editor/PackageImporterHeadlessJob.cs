using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HSE_MAIN.Editor.Mods
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class PackageImporterHeadlessJob
    {
        static PackageImporterHeadlessJob()
        {
            EditorApplication.delayCall += TryStartFromCommandLine;
        }

        private static void TryStartFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var pkg = GetArgValue(args, "-hsePackage");

                if (string.IsNullOrWhiteSpace(pkg))
                    return; // normal editor usage

                // [InitializeOnLoad] fires again after every domain reload, so without a guard a package that causes a
                // reload restarts its own build forever. SessionState survives reloads and dies with the process.
                const string AttemptKey = "HSE_HeadlessAttempt";
                int attempt = SessionState.GetInt(AttemptKey, 0) + 1;
                SessionState.SetInt(AttemptKey, attempt);
                if (attempt > 1)
                {
                    AppendLog(GetArgValue(args, "-hseLog"), new LogLine
                    {
                        Package = pkg,
                        Status = "failed",
                        Stage = "restart",
                        Message = $"Job re-entered after a domain reload (attempt {attempt}); aborting instead of restarting the build."
                    });
                    EditorApplication.Exit(12);
                    return;
                }

                var outRoot = GetArgValue(args, "-hseOutput");
                var logPath = GetArgValue(args, "-hseLog");

                bool buildAndroid = GetBoolArg(args, "-hseAndroid", defaultValue: true);
                bool buildWindows = GetBoolArg(args, "-hseWindows", defaultValue: true);
                bool buildMac = GetBoolArg(args, "-hseMac", defaultValue: true);

                bool createZip = GetBoolArg(args, "-hseZip", defaultValue: true);
                bool exportPkg = GetBoolArg(args, "-hseExportSubscenePackage", defaultValue: false);

                if (!File.Exists(pkg))
                {
                    AppendLog(logPath, new LogLine
                    {
                        Package = pkg,
                        Status = "failed",
                        Stage = "args",
                        Message = "Package file not found"
                    });
                    EditorApplication.Exit(2);
                    return;
                }

                if (string.IsNullOrWhiteSpace(outRoot))
                {
                    AppendLog(logPath, new LogLine
                    {
                        Package = pkg,
                        Status = "failed",
                        Stage = "args",
                        Message = "Output folder not provided (-hseOutput)"
                    });
                    EditorApplication.Exit(3);
                    return;
                }

                Directory.CreateDirectory(outRoot);
                if (!string.IsNullOrWhiteSpace(logPath))
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));

                var exporter = FindExporterAsset();
                if (exporter == null)
                {
                    AppendLog(logPath, new LogLine
                    {
                        Package = pkg,
                        Status = "failed",
                        Stage = "startup",
                        Message = "No PackageExporterSO asset found in project (AssetDatabase.FindAssets(\"t:PackageExporterSO\") returned empty)."
                    });
                    EditorApplication.Exit(4);
                    return;
                }

                SetPrivateBool(exporter, "_createZipAfterPublish", createZip);
                SetPrivateBool(exporter, "_exportUnityPackageAfterBuild", exportPkg);

                RunOne(exporter, pkg, outRoot, logPath, buildAndroid, buildWindows, buildMac);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(ex);
                EditorApplication.Exit(100);
            }
        }

        private static void RunOne(PackageExporterSO exporter, string packagePath, string outputRoot, string logPath, bool android, bool windows, bool mac)
        {
            string packageName = Path.GetFileNameWithoutExtension(packagePath);
            string perPackageOut = Path.Combine(outputRoot, packageName);
            Directory.CreateDirectory(perPackageOut);

            // NOTE: this clock starts AFTER editor boot and package import, so it under-reports. The wall clock the
            // build farm should trust is the one PowerShell measures around the Unity process.
            var swAll = Stopwatch.StartNew();

            int loggedErrors = 0;
            void OnLog(string condition, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    loggedErrors++;
            }
            Application.logMessageReceived += OnLog;

            AppendLog(logPath, new LogLine
            {
                Package = packagePath,
                PackageName = packageName,
                Output = perPackageOut,
                Status = "started",
                Stage = "begin"
            });

            var platforms = new[] { BuildTarget.Android, BuildTarget.StandaloneWindows64, BuildTarget.StandaloneOSX };

            Unity.EditorCoroutines.Editor.EditorCoroutineUtility.StartCoroutineOwnerless(RunCoroutine());

            System.Collections.IEnumerator RunCoroutine()
            {
                var enumerator = exporter.RunBatchSinglePackage(packagePath, outputRoot, logPath, platforms);

                bool crashed = false;
                string crashMessage = null;

                while (true)
                {
                    bool moved;
                    try { moved = enumerator.MoveNext(); }
                    catch (Exception ex)
                    {
                        crashed = true;
                        crashMessage = ex.ToString();
                        break;
                    }

                    if (!moved)
                        break;

                    yield return enumerator.Current;
                }

                swAll.Stop();
                Application.logMessageReceived -= OnLog;

                bool failed = crashed || loggedErrors > 0 || exporter.LastRunFailed;

                AppendLog(logPath, new LogLine
                {
                    Package = packagePath,
                    PackageName = packageName,
                    Output = perPackageOut,
                    Status = failed ? "failed" : "success",
                    Stage = crashed ? "exception" : "done",
                    DurationMs = (long)swAll.Elapsed.TotalMilliseconds,
                    Message = crashed
                        ? crashMessage
                        : $"{(failed ? "Completed with errors" : "Completed")} (logged errors: {loggedErrors}; builds all 3 platforms, the selective flags are not wired yet)."
                });

                EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        private static PackageExporterSO FindExporterAsset()
        {
            var guids = AssetDatabase.FindAssets("t:PackageExporterSO");
            if (guids == null || guids.Length == 0)
                return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<PackageExporterSO>(path);
        }

        private static void SetPrivateBool(object obj, string fieldName, bool value)
        {
            try
            {
                var f = obj.GetType().GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(bool))
                    f.SetValue(obj, value);
            }
            catch { }
        }

        private static string GetArgValue(string[] args, string name)
        {
            if (args == null) return null;

            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                        return args[i + 1];
                    return null;
                }

                if (a != null && a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return a.Substring(name.Length + 1);
            }

            return null;
        }

        private static bool GetBoolArg(string[] args, string name, bool defaultValue)
        {
            var v = GetArgValue(args, name);
            if (string.IsNullOrWhiteSpace(v))
                return defaultValue;

            if (bool.TryParse(v, out var b))
                return b;

            if (int.TryParse(v, out var n))
                return n != 0;

            return defaultValue;
        }

        [Serializable]
        private struct LogLine
        {
            public string Package;
            public string PackageName;
            public string Output;
            public string Status;   // started/success/failed
            public string Stage;    // begin/startup/exception/done/etc
            public string Message;
            public long DurationMs;
            public string Utc;
        }

        private static void AppendLog(string logPath, LogLine line)
        {
            try
            {
                line.Utc = DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(line);

                if (string.IsNullOrWhiteSpace(logPath))
                {
                    UnityEngine.Debug.LogError(json);
                    return;
                }

                File.AppendAllText(logPath, json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(ex);
            }
        }
    }
#endif
}