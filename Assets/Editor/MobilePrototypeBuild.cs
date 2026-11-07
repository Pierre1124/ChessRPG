using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

public static class MobilePrototypeBuild
{
    public const string ApkPath = "output/android/ChessRPG-MobilePrototype-v3.apk";
    private const string PreviewKey = "ChessRPG.MobilePrototypePreview";

    [MenuItem("Tools/Chess/Mobile/Toggle Mobile Preview")]
    public static void TogglePreview()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before changing mobile preview.");
        EditorPrefs.SetBool(PreviewKey, !EditorPrefs.GetBool(PreviewKey, false));
        Debug.Log("Mobile preview: " + EditorPrefs.GetBool(PreviewKey));
    }

    [MenuItem("Tools/Chess/Mobile/Restart Editor for Android Modules")]
    public static void RestartEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before restarting.");
        EditorApplication.OpenProject(Directory.GetCurrentDirectory());
    }

    [MenuItem("Tools/Chess/Mobile/Build Android Prototype APK")]
    public static void BuildAndroid()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new InvalidOperationException("Install Android Build Support with SDK, NDK and OpenJDK for Unity " + Application.unityVersion + " using Unity Hub, then restart the Editor.");

        var target = NamedBuildTarget.Android;
        string id = PlayerSettings.GetApplicationIdentifier(target);
        int versionCode = PlayerSettings.Android.bundleVersionCode;
        var backend = PlayerSettings.GetScriptingBackend(target);
        var architectures = PlayerSettings.Android.targetArchitectures;
        var orientation = PlayerSettings.defaultInterfaceOrientation;
        bool portrait = PlayerSettings.allowedAutorotateToPortrait, upsideDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown;
        bool left = PlayerSettings.allowedAutorotateToLandscapeLeft, right = PlayerSettings.allowedAutorotateToLandscapeRight;
        bool bundle = EditorUserBuildSettings.buildAppBundle;
        bool customKeystore = PlayerSettings.Android.useCustomKeystore;
        var previousTarget = EditorUserBuildSettings.activeBuildTarget;
        var previousGroup = BuildPipeline.GetBuildTargetGroup(previousTarget);
        Directory.CreateDirectory("output/android");
        try
        {
            PlayerSettings.SetApplicationIdentifier(target, "com.chessrpg.mobileprototype");
            PlayerSettings.Android.bundleVersionCode = 3;
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Unable to activate Android build target.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/StartScene.unity", "Assets/Scenes/ChessScene.unity" },
                target = BuildTarget.Android,
                locationPathName = ApkPath,
                options = BuildOptions.Development
            });
            File.WriteAllText("output/android/build-result.txt", report.summary.result + "\n" +
                report.summary.totalErrors + " errors\n" + report.summary.platform + "\n" + report.summary.outputPath + "\n" + report.summary.totalSize + " bytes\n" + report.summary.totalTime);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Android build failed. Inspect the Console and output/android/build-result.txt.");
            if (!File.Exists(ApkPath) || new FileInfo(ApkPath).Length == 0) throw new InvalidOperationException("Build reported success but APK is missing. Restart the Editor after installing Android modules, then rebuild.");
            Debug.Log("Android prototype: " + Path.GetFullPath(ApkPath));
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(target, id);
            PlayerSettings.Android.bundleVersionCode = versionCode;
            PlayerSettings.SetScriptingBackend(target, backend);
            PlayerSettings.Android.targetArchitectures = architectures;
            PlayerSettings.Android.useCustomKeystore = customKeystore;
            PlayerSettings.defaultInterfaceOrientation = orientation;
            PlayerSettings.allowedAutorotateToPortrait = portrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = upsideDown;
            PlayerSettings.allowedAutorotateToLandscapeLeft = left;
            PlayerSettings.allowedAutorotateToLandscapeRight = right;
            EditorUserBuildSettings.buildAppBundle = bundle;
            if (EditorUserBuildSettings.activeBuildTarget != previousTarget)
                EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previousTarget);
        }
    }
}
