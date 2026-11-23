using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Girotronica > Build > Web (itch.io): applies web settings that work on itch.io
/// (Gzip + decompression fallback, so no special server headers are needed) and builds to Builds/Web.
/// </summary>
public static class WebBuild
{
    const string OutputDir = "Builds/Web";

    [MenuItem("Girotronica/Build/Web (itch.io)")]
    public static void BuildWeb()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            Debug.LogError("[WebBuild] Could not switch to the Web platform (is the Web Build Support module installed?)");
            return;
        }

        ApplyWebSettings();

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        // Empty the folder instead of deleting it (a folder open in Explorer/a server can't be deleted)
        if (Directory.Exists(OutputDir))
        {
            foreach (var d in Directory.GetDirectories(OutputDir)) Directory.Delete(d, true);
            foreach (var f in Directory.GetFiles(OutputDir)) File.Delete(f);
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;
        string msg = $"[WebBuild] {s.result}: {scenes.Length} scenes, {s.totalSize / (1024f * 1024f):0.0} MB, " +
                     $"{s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalSeconds:0}s -> {Path.GetFullPath(OutputDir)}";
        if (s.result == BuildResult.Succeeded) Debug.Log(msg); else Debug.LogError(msg);
        File.WriteAllText(Path.Combine("Builds", "web_build_result.txt"), msg);
    }

    public static void ApplyWebSettings()
    {
        // itch.io serves files without Content-Encoding headers: Gzip + fallback decompresses in JS
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.template = "PROJECT:Responsive";   // fills the itch.io iframe / fullscreen at any size
        PlayerSettings.WebGL.showDiagnostics = false;

        // 16:9 canvas like the UI (CanvasScaler 1920x1080)
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = false;

        EditorUserBuildSettings.development = false;
        AssetDatabase.SaveAssets();
    }
}
