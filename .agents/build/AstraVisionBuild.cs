// Isolated-project build entry for the ASTRA Academy Vision Aura (iPhone headset) macOS apps.
// Copied into <project>/Assets/Editor/ by build_vision_mac.py; not part of the canonical project.
using System;
using System.IO;
using Astra.UI.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VisionAura.XR.Editor;

public static class AstraVisionBuild
{
    public static void Prepare()
    {
        var importer = (PluginImporter)AssetImporter.GetAtPath("Packages/com.visionaura.xr/Plugins/macOS/VisionAuraXR.dylib");
        importer.SetCompatibleWithAnyPlatform(false);
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
        importer.SetCompatibleWithEditor(false);
        importer.SaveAndReimport();

        if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset))
        {
            var renderer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset"));
            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.CreateAsset(renderer, "Assets/Settings/AstraRenderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1f;
            AssetDatabase.CreateAsset(pipeline, "Assets/Settings/AstraPipeline.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new[] { GraphicsDeviceType.Metal });
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.ARM64;
        PlayerSettings.companyName = "ASTRA Academy";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1440;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.visibleInBackground = true;
        AssetDatabase.SaveAssets();
    }

    public static void BuildScenes()
    {
        P1SceneBuilder.BuildAll();
        EditorBuildSettings.scenes = Array.ConvertAll(P1SceneBuilder.Scenes, s => new EditorBuildSettingsScene(s, true));
        AssetDatabase.SaveAssets();
    }

    public static void BuildPlayers()
    {
        var output = Environment.GetEnvironmentVariable("ASTRA_BUILD_OUTPUT");
        if (string.IsNullOrEmpty(output))
            throw new Exception("Set ASTRA_BUILD_OUTPUT.");
        var modes = (Environment.GetEnvironmentVariable("ASTRA_BUILD_MODES") ?? "ar,vr").Split(',');
        foreach (var raw in modes)
        {
            var mode = raw.Trim().ToLowerInvariant();
            if (mode != "ar" && mode != "vr")
                throw new Exception("Unknown mode " + mode);
            PlayerSettings.productName = "ASTRA Academy " + mode.ToUpperInvariant();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.astraacademy.training." + mode);
            BuildIntegration.BuildMode = mode;
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = P1SceneBuilder.Scenes,
                target = BuildTarget.StandaloneOSX,
                locationPathName = Path.Combine(output, PlayerSettings.productName + ".app"),
                options = BuildOptions.None
            });
            if (result.summary.result != BuildResult.Succeeded)
                throw new Exception("ASTRA build failed: " + mode + " " + result.summary.result);
            Debug.Log($"[AstraVisionBuild] {mode}: {result.summary.outputPath} {result.summary.totalSize / (1024 * 1024)} MB");
        }
    }
}
