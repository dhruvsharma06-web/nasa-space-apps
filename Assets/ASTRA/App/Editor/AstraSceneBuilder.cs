using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Astra.App;
using Astra.Core.Assessment;
using Astra.Core.Mission;
using Astra.DesktopTest;
using Astra.UI;
using Astra.XR.Phone;
using UnityEditor.Events;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Astra.App.EditorTools
{
    /// <summary>
    /// LEVEL FACTORY (CONTRACT §2.1): builds a playable greybox scene for every
    /// Assets/ASTRA/Content/Layouts/l&lt;N&gt;-layout.json + its mission file, then the WebGL test build.
    /// Menu: ASTRA/…  ·  Batch: -executeMethod Astra.App.EditorTools.AstraSceneBuilder.BuildWebGLBatch
    /// A level script named "&lt;Scene without _&gt;View" (e.g. L3CabinView) is attached automatically; its serialized
    /// fields "level", "cabinLights" and one per indicator (ready_light -> readyLight) are filled in.
    /// OWNER: Integration Lead.
    /// </summary>
    public static class AstraSceneBuilder
    {
        private const string MissionsDir = "Assets/ASTRA/Content/Missions";
        private const string LayoutsDir = "Assets/ASTRA/Content/Layouts";
        private const string LevelsDir = "Assets/ASTRA/Levels";
        private const string SharedDir = LevelsDir + "/_Shared";
        private const string MatDir = SharedDir + "/Materials";
        private const string WebGLOut = "Builds/WebGL";
        private const string TmpFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private static readonly Vector3 Eye = new Vector3(0f, 1.2f, 0f);
        private static readonly Vector3 SunDir = new Vector3(0.75f, 0.35f, -0.2f).normalized;   // towards the sun: right and slightly behind: a half-lit Earth with the day/night line in the window
        /// <summary>Coach proxy base URL baked into levels; unreachable = offline coach (no error shown).
        /// Hosted proxy on Render (free plan: sleeps when idle; open {url}/health to wake it before a demo).
        /// For a local proxy, set "http://127.0.0.1:8787" and rebuild the scenes.</summary>
        public const string CoachProxyUrl = "https://nasa-space-apps-s7jq.onrender.com";
        private const string NasaDir = "Assets/ASTRA/Levels/_Shared/Textures/NASA";

        private static TMP_FontAsset _font;
        private static Dictionary<string, string> _events;
        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        // ------------------------------------------------------------------ layout file model (JsonUtility)
        [Serializable] private class LayoutFile { public string missionId; public string scene; public PanelDef[] panels = new PanelDef[0]; public ControlDef[] controls = new ControlDef[0]; public ToolDef[] tools = new ToolDef[0]; public ControlDef[] decoys = new ControlDef[0]; public IndicatorDef[] indicators = new IndicatorDef[0]; }
        [Serializable] private class PanelDef { public string id; public string title; public float[] position; public float[] size; public float tilt = 15f; public bool target; }
        [Serializable] private class ControlDef { public string targetId; public string type; public string label; public string panel; public float[] at; public string[] positions; public string accepts; public string trigger; }
        [Serializable] private class ToolDef { public string targetId; public string label; public float[] position; }
        [Serializable] private class IndicatorDef { public string id; public string label; public string panel; public float[] at; }

        // ------------------------------------------------------------------ entry points

        [MenuItem("ASTRA/Build all level scenes from layouts")]
        public static void BuildAllLevels()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFont);
            if (_font == null) throw new Exception("TextMeshPro essentials missing: import 'TMP Essential Resources' first.");
            EnsureFolder(LevelsDir, "_Shared");
            if (AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.DeleteAsset(MatDir);
            AssetDatabase.CreateFolder(SharedDir, "Materials");
            Mats.Clear();

            var scenes = new List<string>();
            foreach (var path in Directory.GetFiles(LayoutsDir, "l*-layout.json").OrderBy(p => p))
            {
                string scenePath = BuildLevel(path.Replace('\\', '/'));
                if (scenePath != null) scenes.Add(scenePath);
            }
            var build = new List<EditorBuildSettingsScene>();
            const string hub = "Assets/ASTRA/Scenes/Hub.unity";
            if (File.Exists(hub)) build.Add(new EditorBuildSettingsScene(hub, true));   // Hub first when P1 adds it
            build.AddRange(scenes.Select(s => new EditorBuildSettingsScene(s, true)));
            EditorBuildSettings.scenes = build.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[AstraSceneBuilder] Built {scenes.Count} level scene(s): {string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}");
        }

        [MenuItem("ASTRA/Build WebGL test build")]
        public static void BuildWebGL()
        {
            BuildAllLevels();
            PlayerSettings.productName = "ASTRA Academy - Test";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;   // any static server can host it
            SetWebGLCodeOptimization("BuildTimes");   // test builds: much faster wasm build, slightly slower runtime
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),   // Hub first
                locationPathName = WebGLOut,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            Debug.Log($"[AstraSceneBuilder] WebGL build {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("WebGL build failed");
        }

        /// <summary>Batch entry point: exits 0 on success, 1 on failure.</summary>
        public static void BuildWebGLBatch()
        {
            try { BuildWebGL(); EditorApplication.Exit(0); }
            catch (Exception ex) { Debug.LogError(ex); EditorApplication.Exit(1); }
        }

        [MenuItem("ASTRA/Build Android APK (phone)")]
        public static void BuildAndroid()
        {
            BuildAllLevels();
            PlayerSettings.productName = "ASTRA Academy";
            PlayerSettings.bundleVersion = "1.0." + DateTime.Now.ToString("MMddHHmm");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.astra.academy");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.optimizedFramePacing = false;   // Cardboard requirement (see XR/Phone/README.md)
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "Builds/Android/ASTRA-Academy.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Debug.Log($"[AstraSceneBuilder] Android build {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed");
        }

        /// <summary>Batch: -buildTarget Android -executeMethod Astra.App.EditorTools.AstraSceneBuilder.BuildAndroidBatch</summary>
        public static void BuildAndroidBatch()
        {
            try { BuildAndroid(); EditorApplication.Exit(0); }
            catch (Exception ex) { Debug.LogError(ex); EditorApplication.Exit(1); }
        }

        /// <summary>Batch entry point for scenes only (no player build).</summary>
        public static void BuildLevelsBatch()
        {
            try { BuildAllLevels(); EditorApplication.Exit(0); }
            catch (Exception ex) { Debug.LogError(ex); EditorApplication.Exit(1); }
        }

        // ------------------------------------------------------------------ one level

        private static string BuildLevel(string layoutPath)
        {
            var layout = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(layoutPath));
            string missionPath = $"{MissionsDir}/{layout.missionId}.json";
            var missionAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(missionPath);
            if (missionAsset == null) { Debug.LogError($"[AstraSceneBuilder] {layoutPath}: mission {missionPath} not found; skipped."); return null; }
            var mission = JsonUtility.FromJson<MissionDefinition>(missionAsset.text);
            _events = new Dictionary<string, string>();
            foreach (var s in mission.steps) _events[s.targetId] = s.expectedEvent;

            string folder = Directory.GetDirectories(LevelsDir, $"L{mission.level}_*").Select(d => d.Replace('\\', '/')).FirstOrDefault()
                            ?? EnsureFolder(LevelsDir, layout.scene);
            string scenePath = $"{folder}/{layout.scene}.unity";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;               // soft sky / wall / floor bounce
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.45f, 0.50f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.30f, 0.31f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.07f, 0.08f);

            BuildRoom(mission.title);
            var cabinLights = BuildLights();
            var panels = new Dictionary<string, Transform>();
            foreach (var p in layout.panels) panels[p.id] = Panel(p);
            foreach (var c in layout.controls) Control(c, panels, decoy: false);
            foreach (var c in layout.decoys) Control(c, panels, decoy: true);
            foreach (var t in layout.tools) Tool(t);
            var indicators = new Dictionary<string, Renderer>();
            foreach (var i in layout.indicators) indicators[i.id] = Indicator(i, panels);

            BuildSpaceView();
            var (cam, rig) = BuildCamera();
            BuildPostProcessing(cam, folder, layout.scene);

            var lcGo = new GameObject("LevelController");
            var lc = lcGo.AddComponent<LevelController>();
            lcGo.AddComponent<PlatformQuality>();   // phone tier trims effects; browser keeps full quality
            var coach = lcGo.AddComponent<Astra.Integrations.Coach.CoachProvider>();   // offline coach always; AI when the proxy answers
            var cso = new SerializedObject(coach);
            cso.FindProperty("proxyUrl").stringValue = CoachProxyUrl;
            cso.ApplyModifiedPropertiesWithoutUndo();
            var so = new SerializedObject(lc);
            so.FindProperty("coachProvider").objectReferenceValue = coach;
            so.FindProperty("missionJson").objectReferenceValue = missionAsset;
            bool realUi = PlaceRealUi(so);   // XR & Interaction's world-space panels, when present
            so.ApplyModifiedPropertiesWithoutUndo();
            lcGo.AddComponent<DesktopHud>().Configure(lc, rig, realUi);
            AttachLevelView(lcGo, layout.scene, lc, cabinLights, indicators);

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[AstraSceneBuilder] Saved {scenePath}");
            return scenePath;
        }

        private static void AttachLevelView(GameObject host, string sceneName, LevelController lc, List<Light> lights, Dictionary<string, Renderer> indicators)
        {
            var type = Type.GetType(sceneName.Replace("_", "") + "View, Assembly-CSharp");
            if (type == null) return;
            var view = host.AddComponent(type);
            var so = new SerializedObject(view);
            var level = so.FindProperty("level"); if (level != null) level.objectReferenceValue = lc;
            var lightsProp = so.FindProperty("cabinLights");
            if (lightsProp != null && lightsProp.isArray)
            {
                lightsProp.arraySize = lights.Count;
                for (int i = 0; i < lights.Count; i++) lightsProp.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            }
            foreach (var kv in indicators)
            {
                var prop = so.FindProperty(CamelCase(kv.Key));
                if (prop != null) prop.objectReferenceValue = kv.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ room

        private static void BuildRoom(string missionTitle)
        {
            var root = new GameObject("Cabin_Shell").transform;
            var wall = Surface("Wall_Panel", "panel", new Color(0.90f, 0.89f, 0.86f), 0.25f, 0f, new Vector2(2f, 2f), 0.6f);
            var floor = Surface("Floor_DiamondPlate", "diamond", new Color(0.30f, 0.31f, 0.33f), 0.55f, 0.8f, new Vector2(8f, 7f), 1.0f);
            var trim = Mat("Trim_DarkMetal", new Color(0.22f, 0.23f, 0.25f), 0.55f, 0.6f);
            var fabric = Mat("Stowage_Fabric", new Color(0.78f, 0.76f, 0.70f), 0.05f);
            var seatMat = Mat("Seat_Cushion", new Color(0.12f, 0.14f, 0.18f), 0.2f);

            Box("Floor", root, new Vector3(0, -0.01f, 0.025f), new Vector3(2.6f, 0.02f, 2.05f), floor);     // ends at the front wall (z 1.05)
            Box("Ceiling", root, new Vector3(0, 2.2f, 0.025f), new Vector3(2.6f, 0.02f, 2.05f), wall);
            // front wall with a large viewport window (x -0.55..0.55, y 1.62..2.12): the Earth fills its lower part
            Box("Wall_Front_Left", root, new Vector3(-0.925f, 1.1f, 1.05f), new Vector3(0.75f, 2.2f, 0.04f), wall);
            Box("Wall_Front_Right", root, new Vector3(0.925f, 1.1f, 1.05f), new Vector3(0.75f, 2.2f, 0.04f), wall);
            Box("Wall_Front_Below", root, new Vector3(0f, 0.81f, 1.05f), new Vector3(1.1f, 1.62f, 0.04f), wall);
            Box("Wall_Front_Above", root, new Vector3(0f, 2.16f, 1.05f), new Vector3(1.1f, 0.08f, 0.04f), wall);
            var frame = Mat("Window_Frame", new Color(0.12f, 0.13f, 0.15f), 0.6f, 0.7f);
            Box("Window_Frame_Top", root, new Vector3(0f, 2.135f, 1.03f), new Vector3(1.18f, 0.04f, 0.07f), frame);
            Box("Window_Frame_Bottom", root, new Vector3(0f, 1.605f, 1.03f), new Vector3(1.18f, 0.04f, 0.07f), frame);
            Box("Window_Frame_Left", root, new Vector3(-0.57f, 1.87f, 1.03f), new Vector3(0.04f, 0.54f, 0.07f), frame);
            Box("Window_Frame_Right", root, new Vector3(0.57f, 1.87f, 1.03f), new Vector3(0.04f, 0.54f, 0.07f), frame);
            // wall panel seams and ceiling light fixtures
            var seam = Mat("Seam_Dark", new Color(0.32f, 0.34f, 0.36f), 0.3f);
            for (float z = -0.6f; z <= 0.95f; z += 0.4f)
            {
                Box("Seam_L", root, new Vector3(-1.275f, 1.1f, z), new Vector3(0.008f, 2.2f, 0.01f), seam);
                Box("Seam_R", root, new Vector3(1.275f, 1.1f, z), new Vector3(0.008f, 2.2f, 0.01f), seam);
            }
            var fixture = Mat("Light_Fixture", new Color(0.9f, 0.9f, 0.88f), 0.5f, 0f, new Color(1.6f, 1.55f, 1.45f));
            Box("Fixture_L", root, new Vector3(-0.5f, 2.18f, 0.4f), new Vector3(0.5f, 0.02f, 0.12f), fixture);
            Box("Fixture_R", root, new Vector3(0.5f, 2.18f, 0.4f), new Vector3(0.5f, 0.02f, 0.12f), fixture);
            var led = Mat("LED_Strip", new Color(0.8f, 0.9f, 1f), 0.5f, 0f, new Color(0.9f, 1.3f, 2.0f));
            Box("LED_Strip_L", root, new Vector3(-1.27f, 2.15f, 0.2f), new Vector3(0.01f, 0.012f, 2.2f), led);
            Box("LED_Strip_R", root, new Vector3(1.27f, 2.15f, 0.2f), new Vector3(0.01f, 0.012f, 2.2f), led);
            var cable = Mat("Cable_Bundle", new Color(0.08f, 0.08f, 0.09f), 0.45f);
            var cableOrange = Mat("Cable_Orange", new Color(0.85f, 0.4f, 0.1f), 0.4f);
            for (int i = 0; i < 3; i++)
            {
                Cyl("Cable_L" + i, root, new Vector3(-1.24f, 1.95f - i * 0.03f, 0.2f), 0.022f, 2.2f, i == 1 ? cableOrange : cable);   // runs along z
                Cyl("Cable_R" + i, root, new Vector3(1.24f, 0.55f - i * 0.03f, 0.2f), 0.02f, 2.2f, cable);
            }
            var strap = Mat("Strap_Webbing", new Color(0.12f, 0.16f, 0.24f), 0.2f);
            Box("Strap_1", root, new Vector3(-1.165f, 0.9f, 0.7f), new Vector3(0.005f, 0.32f, 0.03f), strap);
            Box("Strap_2", root, new Vector3(-1.165f, 0.9f, 0.25f), new Vector3(0.005f, 0.27f, 0.03f), strap);
            Box("Wall_Back", root, new Vector3(0, 1.1f, -1.0f), new Vector3(2.6f, 2.2f, 0.04f), wall);
            Box("Wall_Left", root, new Vector3(-1.3f, 1.1f, 0.025f), new Vector3(0.04f, 2.2f, 2.05f), wall);
            Box("Wall_Right", root, new Vector3(1.3f, 1.1f, 0.025f), new Vector3(0.04f, 2.2f, 2.05f), wall);
            Box("Seat_Base", root, new Vector3(0, 0.45f, -0.12f), new Vector3(0.5f, 0.1f, 0.5f), seatMat);
            Box("Seat_Back", root, new Vector3(0, 0.85f, -0.38f), new Vector3(0.5f, 0.75f, 0.08f), seatMat);
            Box("Foot_Restraint", root, new Vector3(0, 0.08f, 0.45f), new Vector3(0.45f, 0.04f, 0.12f), trim);
            Box("Handrail_Left", root, new Vector3(-1.25f, 1.35f, 0.3f), new Vector3(0.03f, 0.03f, 1.1f), trim);
            Box("Handrail_Right", root, new Vector3(1.25f, 1.35f, 0.3f), new Vector3(0.03f, 0.03f, 1.1f), trim);
            Box("Handrail_Ceiling", root, new Vector3(0, 2.12f, 0.2f), new Vector3(1.4f, 0.03f, 0.03f), trim);
            Box("CableRun_Left", root, new Vector3(-1.26f, 1.9f, 0.2f), new Vector3(0.05f, 0.06f, 2.2f), trim);
            Box("Stowage_Bag_1", root, new Vector3(-1.22f, 0.9f, 0.7f), new Vector3(0.12f, 0.3f, 0.35f), fabric);
            Box("Stowage_Bag_2", root, new Vector3(-1.22f, 0.9f, 0.25f), new Vector3(0.12f, 0.25f, 0.3f), fabric);
            Box("Stowage_Bag_3", root, new Vector3(1.22f, 1.7f, 0.0f), new Vector3(0.12f, 0.28f, 0.4f), fabric);
            Label("Placard_Station", root, new Vector3(-0.8f, 1.95f, 1.02f), Quaternion.identity, "ASTRA STATION · CREW CABIN", 0.0022f, new Color(0.1f, 0.12f, 0.15f));
            Label("Placard_Mission", root, new Vector3(-0.8f, 1.89f, 1.02f), Quaternion.identity, missionTitle.ToUpperInvariant(), 0.0016f, new Color(0.18f, 0.2f, 0.24f));
            Label("Placard_Fictional", root, new Vector3(-0.8f, 1.84f, 1.02f), Quaternion.identity, "FICTIONAL TRAINING ENVIRONMENT", 0.0014f, new Color(0.25f, 0.27f, 0.3f));
        }

        private static List<Light> BuildLights()
        {
            var root = new GameObject("Lighting").transform;
            var sun = new GameObject("Sun").AddComponent<Light>();   // shines in through the viewport window
            sun.transform.SetParent(root); sun.type = LightType.Directional; sun.intensity = 1.4f;
            sun.color = new Color(1f, 0.96f, 0.9f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.LookRotation(-SunDir);   // side light: half-lit Earth in the window
            RenderSettings.sun = sun;
            var lights = new List<Light>();
            foreach (var p in new[] { new Vector3(-0.5f, 2.05f, 0.4f), new Vector3(0.5f, 2.05f, 0.4f) })
            {
                var l = new GameObject("Cabin_LED").AddComponent<Light>();
                l.transform.SetParent(root); l.transform.position = p; l.type = LightType.Point;
                l.color = new Color(1f, 0.95f, 0.88f); l.range = 3.5f; l.intensity = 1.5f;   // levels without a view script stay lit
                lights.Add(l);
            }
            return lights;
        }

        // ------------------------------------------------------------------ panels, controls, tools

        private static Transform Panel(PanelDef def)
        {
            var pos = V3(def.position); var size = new Vector2(def.size[0], def.size[1]);
            var root = new GameObject($"Panel_{def.id}").transform;
            root.position = pos;
            var flat = new Vector3(pos.x - Eye.x, 0f, pos.z - Eye.z);
            root.rotation = Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(def.tilt, 0f, 0f);   // faces the seat
            _label = def.title;
            bool screen = def.id.Contains("display");
            var mat = screen ? Mat("Display_Dark", new Color(0.03f, 0.05f, 0.08f), 0.92f, 0f, new Color(0.02f, 0.06f, 0.1f))
                             : Surface("Panel_Brushed", "brushed", new Color(0.36f, 0.37f, 0.38f), 0.55f, 0.6f, Vector2.one, 0.35f);
            var body = Box("Body", root, Vector3.zero, new Vector3(size.x, size.y, 0.03f), mat);
            var bezel = Mat("Panel_Bezel", new Color(0.55f, 0.57f, 0.6f), 0.6f, 0.5f);
            Box("Bezel_Top", root, new Vector3(0, size.y / 2f + 0.006f, -0.004f), new Vector3(size.x + 0.024f, 0.012f, 0.036f), bezel);
            Box("Bezel_Bottom", root, new Vector3(0, -size.y / 2f - 0.006f, -0.004f), new Vector3(size.x + 0.024f, 0.012f, 0.036f), bezel);
            Box("Bezel_Left", root, new Vector3(-size.x / 2f - 0.006f, 0, -0.004f), new Vector3(0.012f, size.y, 0.036f), bezel);
            Box("Bezel_Right", root, new Vector3(size.x / 2f + 0.006f, 0, -0.004f), new Vector3(0.012f, size.y, 0.036f), bezel);
            var screw = Mat("Screw_Steel", new Color(0.7f, 0.72f, 0.75f), 0.8f, 1f);
            foreach (var sx in new[] { -1f, 1f }) foreach (var sy in new[] { -1f, 1f })
                Cyl("Screw", root, new Vector3(sx * (size.x / 2f - 0.014f), sy * (size.y / 2f - 0.014f), -0.016f), 0.009f, 0.003f, screw);
            Box("Title_Backlight", root, new Vector3(0, size.y / 2f - 0.025f, -0.0155f), new Vector3(size.x * 0.7f, 0.022f, 0.002f),
                Mat("Title_Backlight", new Color(0.1f, 0.2f, 0.32f), 0.5f, 0f, new Color(0.15f, 0.35f, 0.6f)));
            Label("Title", root, Face(root, new Vector2(0f, size.y / 2f - 0.025f), 0.018f), root.rotation, def.title, 0.002f, Color.white);
            if (def.target)
            {
                string evt = _events.TryGetValue(def.id, out var e) ? e : EventNames.TargetSelected;
                Target(body.gameObject, def.id, TestTarget.Kind.Panel, evt, null, body.GetComponent<Renderer>(), null, null);
            }
            return root;
        }

        private static void Control(ControlDef c, Dictionary<string, Transform> panels, bool decoy)
        {
            if (!panels.TryGetValue(c.panel ?? "", out var panel)) { Debug.LogError($"[AstraSceneBuilder] {c.targetId}: unknown panel {c.panel}"); return; }
            var at = new Vector2(c.at[0], c.at[1]);
            string evt = !decoy && _events.TryGetValue(c.targetId, out var e) ? e : EventNames.ControlActivated;
            var black = Mat("Control_Black", new Color(0.05f, 0.05f, 0.06f), 0.4f);
            _label = c.label;
            var root = ControlRoot(c.targetId, panel, at);
            switch (c.type)
            {
                case "toggle":
                case "lever":
                {
                    bool lever = c.type == "lever";
                    Box("Collar", root, new Vector3(0, 0, -0.008f), lever ? new Vector3(0.05f, 0.08f, 0.016f) : new Vector3(0.05f, 0.05f, 0.016f), black);
                    var pivot = Pivot(root, -0.016f);
                    var handle = Box(lever ? "Handle" : "Lever", pivot, new Vector3(0, lever ? 0.03f : 0.018f, -0.012f),
                                     lever ? new Vector3(0.02f, 0.065f, 0.02f) : new Vector3(0.012f, 0.04f, 0.012f),
                                     lever ? Mat("Lever_Yellow", new Color(0.95f, 0.75f, 0.1f), 0.4f) : Mat("Control_Chrome", new Color(0.8f, 0.82f, 0.85f), 0.85f, 1f));
                    handle.localRotation = Quaternion.Euler(-30f, 0, 0);
                    var guard = Mat("Toggle_Guard", new Color(0.55f, 0.57f, 0.6f), 0.6f, 0.8f);
                    Box("Guard_L", root, new Vector3(-0.022f, 0.01f, -0.02f), new Vector3(0.006f, lever ? 0.075f : 0.05f, 0.03f), guard);
                    Box("Guard_R", root, new Vector3(0.022f, 0.01f, -0.02f), new Vector3(0.006f, lever ? 0.075f : 0.05f, 0.03f), guard);
                    Label("Label", root, root.position - root.forward * 0.02f + root.up * (lever ? 0.088f : 0.045f), root.rotation, c.label, 0.0013f, Color.white);   // above the handle
                    var state = Label("State", root, root.position - root.forward * 0.02f - root.up * (lever ? 0.055f : 0.04f), root.rotation, "OFF", 0.0011f, new Color(0.7f, 0.72f, 0.75f));
                    Target(root.gameObject, c.targetId, lever ? TestTarget.Kind.Lever : TestTarget.Kind.Toggle, evt, pivot, handle.GetComponent<Renderer>(), state, lever ? new Vector3(0.07f, 0.11f, 0.08f) : new Vector3(0.07f, 0.08f, 0.06f));
                    break;
                }
                case "dial":
                {
                    var pivot = Pivot(root, -0.012f);
                    Cyl("Skirt", root, new Vector3(0, 0, -0.004f), 0.07f, 0.006f, Mat("Dial_Skirt", new Color(0.16f, 0.17f, 0.19f), 0.6f, 0.7f));
                    var knob = Cyl("Knob", pivot, Vector3.zero, 0.05f, 0.022f, Mat("Knob_Rubber", new Color(0.06f, 0.06f, 0.07f), 0.35f));
                    Box("Pointer", pivot, new Vector3(0, 0.016f, -0.012f), new Vector3(0.005f, 0.018f, 0.004f), Mat("Pointer_White", Color.white, 0.3f, 0f, new Color(0.6f, 0.6f, 0.6f)));
                    Label("Label", root, root.position - root.forward * 0.02f + root.up * 0.055f, root.rotation, c.label, 0.0013f, Color.white);
                    var positions = c.positions != null && c.positions.Length > 0 ? c.positions : new[] { "OFF", "ON" };
                    for (int i = 0; i < positions.Length; i++)
                    {
                        float a = Mathf.Deg2Rad * (45f - 45f * i);
                        Label("Pos_" + positions[i], root, root.position - root.forward * 0.02f + root.right * (0.045f * Mathf.Sin(a)) + root.up * (0.045f * Mathf.Cos(a) - 0.005f), root.rotation, positions[i], 0.0011f, new Color(0.85f, 0.87f, 0.9f));
                    }
                    Target(root.gameObject, c.targetId, TestTarget.Kind.Dial, evt, pivot, knob.GetComponent<Renderer>(), null, new Vector3(0.08f, 0.08f, 0.06f));
                    break;
                }
                case "checklist":
                {
                    var tab = Box("Tab", root, new Vector3(0, 0, -0.004f), new Vector3(0.14f, 0.065f, 0.012f), Mat("Display_Tab", new Color(0.12f, 0.32f, 0.55f), 0.6f, 0f, new Color(0.03f, 0.1f, 0.2f)));
                    Label("Label", root, root.position - root.forward * 0.012f, root.rotation, c.label, 0.0013f, Color.white);
                    Target(root.gameObject, c.targetId, TestTarget.Kind.Checklist, evt, null, tab.GetComponent<Renderer>(), null, new Vector3(0.14f, 0.065f, 0.05f));
                    break;
                }
                case "socket":
                {
                    var bay = Box("Bay", root, new Vector3(0, 0, -0.004f), new Vector3(0.09f, 0.09f, 0.01f), Mat("Socket_Bay", new Color(0.1f, 0.12f, 0.16f), 0.3f, 0f, new Color(0.0f, 0.04f, 0.06f)));
                    Box("Rim", root, new Vector3(0, 0.048f, -0.008f), new Vector3(0.1f, 0.006f, 0.01f), Mat("Socket_Rim", new Color(0.95f, 0.75f, 0.1f), 0.4f));
                    Label("Label", root, root.position - root.forward * 0.02f - root.up * 0.065f, root.rotation, c.label, 0.0012f, Color.white);
                    Target(root.gameObject, c.targetId, TestTarget.Kind.Socket, EventNames.ObjectPlaced, null, bay.GetComponent<Renderer>(), null, new Vector3(0.1f, 0.1f, 0.06f), c.accepts);
                    break;
                }
                default: // button
                {
                    Box("Housing", root, new Vector3(0, 0, -0.006f), new Vector3(0.065f, 0.045f, 0.012f), black);
                    var pivot = Pivot(root, -0.016f);
                    Cyl("Backlight_Ring", root, new Vector3(0, 0, -0.012f), 0.05f, 0.004f, Mat("Button_Ring", new Color(0.1f, 0.3f, 0.5f), 0.6f, 0f, new Color(0.12f, 0.45f, 0.9f)));
                    var cap = Cyl("Cap", pivot, Vector3.zero, 0.042f, 0.014f, Mat("Button_Cap", new Color(0.86f, 0.87f, 0.9f), 0.45f));
                    Label("Label", root, root.position - root.forward * 0.02f - root.up * 0.042f, root.rotation, c.label, 0.0012f, Color.white, 0.10f);   // under the button, like switch labels
                    Target(root.gameObject, c.targetId, TestTarget.Kind.Button, evt, pivot, cap.GetComponent<Renderer>(), null, new Vector3(0.075f, 0.06f, 0.06f));
                    break;
                }
            }
            // Mission controls raise their event only in the trigger state; decoys raise on every activation.
            var tt = root.GetComponent<TestTarget>();
            if (tt != null)
            {
                string[] dialPositions = c.type == "dial" ? (c.positions != null && c.positions.Length > 0 ? c.positions : new[] { "OFF", "ON" }) : null;
                tt.SetStateRules(c.trigger, dialPositions, decoy);
            }
        }

        private static void Tool(ToolDef t)
        {
            _label = t.label;
            var root = new GameObject(t.targetId).transform;   // object name == targetId
            root.position = V3(t.position);
            var flat = new Vector3(root.position.x, 0f, root.position.z);
            root.rotation = Quaternion.LookRotation(flat.normalized);
            var body = Box("Body", root, new Vector3(0, 0.035f, 0), new Vector3(0.045f, 0.06f, 0.028f), Mat("Tool_Orange", new Color(0.95f, 0.45f, 0.1f), 0.5f));
            var shaft = Cyl("Shaft", root, new Vector3(0, -0.02f, 0), 0.016f, 0.06f, Mat("Control_Chrome", new Color(0.8f, 0.82f, 0.85f), 0.85f, 1f));
            shaft.localRotation = Quaternion.identity;   // vertical
            var grip = Cyl("Grip", root, new Vector3(0, -0.075f, 0), 0.03f, 0.07f, Mat("Grip_Rubber", new Color(0.07f, 0.07f, 0.08f), 0.3f));
            grip.localRotation = Quaternion.identity;
            Box("Tether_Ring", root, new Vector3(0, -0.115f, 0), new Vector3(0.02f, 0.006f, 0.02f), Mat("Lever_Yellow", new Color(0.95f, 0.75f, 0.1f), 0.4f));
            Label("Label", root, root.position - root.forward * 0.02f + root.up * 0.09f, root.rotation, t.label, 0.0011f, Color.white);
            Target(root.gameObject, t.targetId, TestTarget.Kind.Tool, EventNames.ObjectGrabbed, null, body.GetComponent<Renderer>(), null, new Vector3(0.08f, 0.2f, 0.08f));
        }

        private static Renderer Indicator(IndicatorDef def, Dictionary<string, Transform> panels)
        {
            if (!panels.TryGetValue(def.panel ?? "", out var panel)) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = def.id; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var at = new Vector2(def.at[0], def.at[1]);
            go.transform.position = Face(panel, at, 0.01f); go.transform.localScale = Vector3.one * 0.02f;
            go.transform.SetParent(panel, true);
            go.GetComponent<Renderer>().sharedMaterial = Mat("Indicator", new Color(0.12f, 0.14f, 0.12f), 0.8f, 0f, Color.black);
            if (!string.IsNullOrEmpty(def.label))
                Label(def.id + "_Label", panel, Face(panel, at + new Vector2(0f, -0.025f)), panel.rotation, def.label, 0.0012f, Color.white);
            return go.GetComponent<Renderer>();
        }

        // ------------------------------------------------------------------ space view (Earth + stars) and real UI

        private static void BuildSpaceView()
        {
            var root = new GameObject("Space_View").transform;
            // Earth: a large sphere below the viewport; its limb crosses the window (procedural texture, no downloads)
            var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earth.name = "Earth"; UnityEngine.Object.DestroyImmediate(earth.GetComponent<Collider>());
            earth.transform.SetParent(root);
            earth.transform.position = new Vector3(0f, -19.0f, 70f);   // limb at ~33° elevation: Earth fills the lower window, stars above
            earth.transform.localScale = Vector3.one * 110f;
            earth.transform.rotation = Quaternion.Euler(20f, 140f, 10f);
            var nasaDay = AssetDatabase.LoadAssetAtPath<Texture2D>($"{NasaDir}/earth_day.jpg");
            if (nasaDay != null && !Mats.ContainsKey("Earth")) Mats["Earth"] = NasaEarthMaterial(nasaDay);
            if (!Mats.TryGetValue("Earth", out var mat))   // created once per build, shared by every level
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Earth" };
                mat.SetTexture("_BaseMap", EarthTexture());
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.35f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.02f, 0.05f, 0.1f));
                AssetDatabase.CreateAsset(mat, $"{MatDir}/Earth.mat");
                Mats["Earth"] = mat;
            }
            earth.GetComponent<MeshFilter>().sharedMesh = SmoothSphere();   // the primitive's 24 segments look faceted at this size
            var r = earth.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
            var spin = earth.AddComponent<SlowSpin>(); spin.Configure(new Vector3(0.2f, 1f, 0f), 0.5f);   // orbital drift
            if (nasaDay != null)
            {
                var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);   // atmosphere halo beyond the limb
                shell.name = "Atmosphere"; UnityEngine.Object.DestroyImmediate(shell.GetComponent<Collider>());
                shell.transform.SetParent(root);
                shell.transform.position = earth.transform.position; shell.transform.localScale = Vector3.one * 110f * 1.025f;
                if (!Mats.TryGetValue("Atmosphere", out var atmo))
                {
                    atmo = new Material(Shader.Find("ASTRA/Atmosphere")) { name = "Atmosphere" };
                    AssetDatabase.CreateAsset(atmo, $"{MatDir}/Atmosphere.mat"); Mats["Atmosphere"] = atmo;
                }
                shell.GetComponent<MeshFilter>().sharedMesh = SmoothSphere();
                var sr = shell.GetComponent<Renderer>(); sr.sharedMaterial = atmo; sr.shadowCastingMode = ShadowCastingMode.Off;
            }
            // the sun itself: a small HDR disc far away along the sun direction; bloom turns it into glare
            var sunDisc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sunDisc.name = "Sun_Disc"; UnityEngine.Object.DestroyImmediate(sunDisc.GetComponent<Collider>());
            sunDisc.transform.SetParent(root); sunDisc.transform.position = Eye + SunDir * 180f; sunDisc.transform.localScale = Vector3.one * 5f;
            if (!Mats.TryGetValue("SunDisc", out var sunMat))
            {
                sunMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "SunDisc" };
                sunMat.SetColor("_BaseColor", new Color(24f, 21f, 17f));
                AssetDatabase.CreateAsset(sunMat, $"{MatDir}/SunDisc.mat"); Mats["SunDisc"] = sunMat;
            }
            var sdr = sunDisc.GetComponent<Renderer>(); sdr.sharedMaterial = sunMat; sdr.shadowCastingMode = ShadowCastingMode.Off;
            // NASA star sky (skybox) when available; the particle stars below stay as the fallback
            var starmap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{NasaDir}/starmap.jpg");
            if (starmap != null)
            {
                if (!Mats.TryGetValue("Sky", out var sky))
                {
                    sky = new Material(Shader.Find("Skybox/Panoramic")) { name = "Sky_NASA_Stars" };
                    sky.SetTexture("_MainTex", starmap); sky.SetFloat("_Exposure", 0.55f); sky.SetFloat("_Rotation", 40f);
                    AssetDatabase.CreateAsset(sky, $"{MatDir}/Sky_NASA_Stars.mat"); Mats["Sky"] = sky;
                }
                RenderSettings.skybox = sky;
            }

            // microgravity dust: slow-drifting specks inside the cabin (world motion only; the camera never moves)
            var dust = new GameObject("Microgravity_Dust").AddComponent<ParticleSystem>();
            dust.transform.SetParent(root); dust.transform.position = new Vector3(0f, 1.2f, 0.3f);
            var dm = dust.main;
            dm.loop = true; dm.playOnAwake = true; dm.prewarm = true; dm.startLifetime = 40f;
            dm.startSpeed = new ParticleSystem.MinMaxCurve(0.002f, 0.012f); dm.startSize = new ParticleSystem.MinMaxCurve(0.0025f, 0.006f);
            dm.maxParticles = 220; dm.simulationSpace = ParticleSystemSimulationSpace.World;
            var de = dust.emission; de.rateOverTime = 5f;
            var ds = dust.shape; ds.shapeType = ParticleSystemShapeType.Box; ds.scale = new Vector3(2.2f, 1.8f, 1.8f);
            var dn = dust.noise; dn.enabled = true; dn.strength = 0.01f; dn.frequency = 0.15f; dn.scrollSpeed = 0.05f;
            if (!Mats.TryGetValue("Dust", out var dustMat))
            {
                dustMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Dust" };
                dustMat.SetColor("_BaseColor", new Color(0.55f, 0.57f, 0.6f));
                AssetDatabase.CreateAsset(dustMat, $"{MatDir}/Dust.mat");
                Mats["Dust"] = dustMat;
            }
            dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = dustMat;

            if (starmap != null) return;   // the NASA sky replaces the particle stars
            // Stars: one particle burst on a large sphere, never moving
            var stars = new GameObject("Stars").AddComponent<ParticleSystem>();
            stars.transform.SetParent(root);
            stars.transform.position = new Vector3(0f, 1.2f, 0f);
            var main = stars.main;
            main.loop = false; main.playOnAwake = true; main.startSpeed = 0f; main.startLifetime = 1e6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.9f); main.maxParticles = 1800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = stars.emission; emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1800) });
            var shape = stars.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 180f; shape.radiusThickness = 0f;
            var pr = stars.GetComponent<ParticleSystemRenderer>();
            if (!Mats.TryGetValue("Stars", out var starMat))
            {
                starMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Stars" };
                starMat.SetColor("_BaseColor", new Color(0.9f, 0.93f, 1f));
                AssetDatabase.CreateAsset(starMat, $"{MatDir}/Stars.mat");
                Mats["Stars"] = starMat;
            }
            pr.sharedMaterial = starMat;
        }

        /// <summary>High-resolution UV sphere (radius 0.5 like the primitive; equirectangular UVs, east to the right).</summary>
        private static Mesh SmoothSphere()
        {
            const string path = SharedDir + "/SmoothSphere.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            const int W = 192, H = 96;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i <= H; i++)
            for (int j = 0; j <= W; j++)
            {
                float u = j / (float)W, v = i / (float)H;
                float theta = u * Mathf.PI * 2f, phi = (v - 0.5f) * Mathf.PI;
                var n = new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));
                verts.Add(n * 0.5f); normals.Add(n); uvs.Add(new Vector2(u, v));
            }
            for (int i = 0; i < H; i++)
            for (int j = 0; j < W; j++)
            {
                int a = i * (W + 1) + j, b = a + 1, c = a + W + 1, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
            // make sure the faces point outwards (Unity front faces are clockwise)
            int t0 = (H / 2) * W * 6;
            var e = Vector3.Cross(verts[tris[t0 + 1]] - verts[tris[t0]], verts[tris[t0 + 2]] - verts[tris[t0]]);
            if (Vector3.Dot(e, verts[tris[t0]]) < 0f)
                for (int k = 0; k < tris.Count; k += 3) { int tmp = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = tmp; }
            var mesh = new Mesh { name = "SmoothSphere", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Earth from NASA imagery: Blue Marble day + Black Marble night lights (+ clouds if present).</summary>
        private static Material NasaEarthMaterial(Texture2D day)
        {
            var m = new Material(Shader.Find("ASTRA/EarthDayNight")) { name = "Earth" };
            m.SetTexture("_DayTex", day);
            m.SetFloat("_AtmoStrength", 0.75f); m.SetFloat("_NightIntensity", 1.8f);
            var night = AssetDatabase.LoadAssetAtPath<Texture2D>($"{NasaDir}/earth_night.jpg");
            if (night != null) m.SetTexture("_NightTex", night);
            var clouds = AssetDatabase.LoadAssetAtPath<Texture2D>($"{NasaDir}/earth_clouds.jpg");
            if (clouds == null) clouds = CloudTexture();   // NASA no longer serves a cloud layer: procedural, seamless
            if (clouds != null) m.SetTexture("_CloudTex", clouds);
            m.SetFloat("_CloudSpeed", 0.0006f); m.SetFloat("_CloudStrength", 0.85f);
            AssetDatabase.CreateAsset(m, $"{MatDir}/Earth.mat");
            return m;
        }

        /// <summary>Seamless procedural cloud layer (domain-warped fBm, banded by latitude), cached as a PNG.</summary>
        private static Texture2D CloudTexture()
        {
            string path = $"{SharedDir}/Textures/clouds_procedural.png";
            if (!File.Exists(path))
            {
                const int W = 2048, H = 1024;
                var px = new Color[W * H];
                float Base(float x, float y)
                {
                    float wx = x + 0.35f * Fbm(x * 1.7f + 5.2f, y * 1.7f + 1.3f, 4);   // swirl
                    float wy = y + 0.35f * Fbm(x * 1.7f + 9.1f, y * 1.7f + 4.7f, 4);
                    return Fbm(wx * 2.2f, wy * 2.2f, 6);
                }
                for (int y = 0; y < H; y++)
                {
                    float v = y / (float)H;
                    float lat = Mathf.Abs(v - 0.5f) * 2f;                                       // 0 equator .. 1 pole
                    float belt = 0.55f + 0.35f * Mathf.Exp(-Mathf.Pow((lat - 0.55f) / 0.2f, 2f)) + 0.2f * Mathf.Exp(-Mathf.Pow(lat / 0.12f, 2f));
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (float)W;
                        float n = Base(u * 6f, v * 3f) * (1f - u) + Base((u - 1f) * 6f, v * 3f) * u;   // wraps at u = 0/1
                        float c = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.78f, n * belt + 0.12f));
                        px[y * W + x] = new Color(c, c, c, 1f);
                    }
                }
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.SetPixels(px); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti) { ti.sRGBTexture = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D EarthTexture()
        {
            string path = $"{SharedDir}/Earth_Procedural.png";
            if (!File.Exists(path))
            {
                const int W = 1024, H = 512;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float land = Fbm(u * 6f, v * 3f, 5) - 0.08f * Mathf.Abs(v - 0.5f) * 4f;
                    float cloud = Fbm(u * 10f + 13.7f, v * 5f + 4.1f, 4);
                    Color c = land > 0.55f
                        ? Color.Lerp(new Color(0.18f, 0.32f, 0.14f), new Color(0.52f, 0.45f, 0.3f), Mathf.InverseLerp(0.55f, 0.75f, land))
                        : Color.Lerp(new Color(0.02f, 0.08f, 0.25f), new Color(0.05f, 0.25f, 0.5f), Mathf.InverseLerp(0.2f, 0.55f, land));
                    if (v < 0.07f || v > 0.93f) c = Color.Lerp(c, Color.white, 0.85f);                 // ice caps
                    c = Color.Lerp(c, Color.white, Mathf.Clamp01((cloud - 0.55f) * 2.5f) * 0.85f);      // clouds
                    px[y * W + x] = c;
                }
                tex.SetPixels(px); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++) { sum += amp * Mathf.PerlinNoise(x * freq, y * freq); amp *= 0.5f; freq *= 2f; }
            return sum / (1f - Mathf.Pow(0.5f, octaves));
        }

        private const string UiPrefabs = "Assets/ASTRA/UI/Prefabs";

        /// <summary>
        /// Places XR & Interaction's world-space UI prefabs in the cabin (sized by measuring their real width) and assigns
        /// them to the LevelController. Returns false if the prefabs are not in the project yet.
        /// </summary>
        private static bool PlaceRealUi(SerializedObject levelController)
        {
            // left of the control panels and below the window; the debrief floats in front of everything (only shown in Debrief)
            var phase = PlaceUi("PhaseBanner", new Vector3(-0.56f, 1.56f, 0.56f), 0.34f);
            var instruction = PlaceUi("InstructionPanel", new Vector3(-0.56f, 1.37f, 0.56f), 0.42f);
            var debrief = PlaceUi("DebriefPanel", new Vector3(0.0f, 1.3f, 0.44f), 0.46f);
            PlaceUi("AlarmIndicator", new Vector3(0.62f, 1.58f, 0.6f), 0.28f);
            var pause = PlaceUi("PauseMenu", new Vector3(0.0f, 1.3f, 0.42f), 0.40f, backplate: false);
            var pm = pause != null ? pause.GetComponentInChildren<PauseMenu>(true) : null;
            if (pm != null)
            {
                levelController.FindProperty("pauseMenu").objectReferenceValue = pm;
                if (_gaze != null) { UnityEventTools.AddVoidPersistentListener(_gaze.onEmptyTap, pm.Toggle); EditorUtility.SetDirty(_gaze); }   // phone: tap on nothing = pause menu
            }
            var cert = PlaceUi("Certificate", new Vector3(0.0f, 1.32f, 0.5f), 0.56f, backplate: false);   // after the last level
            var cp = cert != null ? cert.GetComponentInChildren<CertificatePanel>(true) : null;
            if (cp != null)
            {
                levelController.FindProperty("certificate").objectReferenceValue = cp;
                var certRoot = new SerializedObject(cp).FindProperty("root").objectReferenceValue as GameObject;
                (certRoot != null ? certRoot : cp.gameObject).SetActive(false);   // hidden until the last level is passed
            }
            var cues = AssetDatabase.LoadAssetAtPath<AudioCues>("Assets/ASTRA/Audio/AudioCues.asset");
            if (cues != null) levelController.FindProperty("audioCues").objectReferenceValue = cues;
            if (instruction == null || phase == null || debrief == null) return false;
            levelController.FindProperty("instructionPanel").objectReferenceValue = instruction.GetComponentInChildren<InstructionPanel>(true);
            levelController.FindProperty("phaseBanner").objectReferenceValue = phase.GetComponentInChildren<PhaseBanner>(true);
            var dp = debrief.GetComponentInChildren<DebriefPanel>(true);
            levelController.FindProperty("debriefPanel").objectReferenceValue = dp;
            // the backplate must hide with the panel: move it under the object the LevelController toggles
            var plate = debrief.transform.Find("Screen_Backplate");
            if (plate != null && dp != null && dp.transform != debrief.transform) plate.SetParent(dp.transform, true);
            if (dp != null) dp.gameObject.SetActive(false);   // hidden until the Debrief phase
            return true;
        }

        private static GameObject PlaceUi(string prefabName, Vector3 position, float targetWidth, bool backplate = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{UiPrefabs}/{prefabName}.prefab");
            if (prefab == null) { Debug.LogWarning($"[AstraSceneBuilder] UI prefab {prefabName} not found; using the test HUD."); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var flat = new Vector3(position.x - Eye.x, 0f, position.z - Eye.z);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(flat.normalized));
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.GetComponentInChildren<RectTransform>();   // explicit null check (Unity fake-null)
            if (rt != null)
            {
                var corners = new Vector3[4]; rt.GetWorldCorners(corners);
                float width = Vector3.Distance(corners[0], corners[3]);
                if (width > 1e-5f) go.transform.localScale *= targetWidth / width;   // exact physical width in metres
                rt.GetWorldCorners(corners);
                float w = Vector3.Distance(corners[0], corners[3]), h = Vector3.Distance(corners[0], corners[1]);
                Debug.Log($"[AstraSceneBuilder] {prefabName}: {w:0.00} m x {h:0.00} m");
                if (backplate) Backplate(go.transform, (corners[0] + corners[2]) / 2f, w, h);
            }
            return go;
        }

        /// <summary>Mounted-screen look: dark glass plate + thin glowing edge behind a world-space UI panel.</summary>
        private static void Backplate(Transform ui, Vector3 centre, float w, float h)
        {
            var holder = new GameObject("Screen_Backplate").transform;
            holder.SetPositionAndRotation(centre + ui.forward * 0.012f, ui.rotation);
            holder.SetParent(ui, true);
            var glass = Mat("Screen_Glass", new Color(0.015f, 0.02f, 0.035f), 0.95f, 0.2f);
            var edge = Mat("Screen_Edge", new Color(0.1f, 0.4f, 0.7f), 0.6f, 0f, new Color(0.15f, 0.6f, 1.3f));
            var frame = Mat("Screen_Frame", new Color(0.16f, 0.17f, 0.19f), 0.7f, 0.8f);
            float W = w + 0.04f, H = h + 0.04f;
            WorldBox("Glass", holder, Vector3.zero, new Vector3(W, H, 0.01f), glass);
            WorldBox("Frame_T", holder, new Vector3(0, H / 2f, -0.004f), new Vector3(W + 0.016f, 0.012f, 0.02f), frame);
            WorldBox("Frame_B", holder, new Vector3(0, -H / 2f, -0.004f), new Vector3(W + 0.016f, 0.012f, 0.02f), frame);
            WorldBox("Frame_L", holder, new Vector3(-W / 2f, 0, -0.004f), new Vector3(0.012f, H, 0.02f), frame);
            WorldBox("Frame_R", holder, new Vector3(W / 2f, 0, -0.004f), new Vector3(0.012f, H, 0.02f), frame);
            WorldBox("Edge_Glow", holder, new Vector3(0, -H / 2f + 0.004f, -0.0065f), new Vector3(W * 0.9f, 0.003f, 0.002f), edge);
            holder.Find("Edge_Glow").gameObject.AddComponent<EmissionPulse>().Configure(new Color(0.15f, 0.6f, 1.3f), 3.5f, 0.55f, 1f);
        }

        /// <summary>Box placed in the holder's local space, immune to the holder's (canvas) scale.</summary>
        private static void WorldBox(string name, Transform holder, Vector3 local, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(holder.TransformPoint(Vector3.zero) + holder.rotation * local, holder.rotation);
            go.transform.SetParent(holder, true);
            var ls = go.transform.lossyScale; var cur = go.transform.localScale;
            go.transform.localScale = new Vector3(cur.x * size.x / ls.x, cur.y * size.y / ls.y, cur.z * size.z / ls.z);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ camera + post

        private static (Camera, DesktopRig) BuildCamera()
        {
            var go = new GameObject("Seat_Eye (Desktop Test Rig)");
            go.transform.position = Eye;
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 400f;
            cam.clearFlags = RenderSettings.skybox != null && RenderSettings.skybox.shader.name == "Skybox/Panoramic" ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.0f, 0.0f, 0.01f);
            cam.allowHDR = true; cam.allowMSAA = true;
            // reflections for metal and glass: one realtime probe for the whole cabin
            var probe = new GameObject("Cabin_ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(0f, 1.2f, 0.2f);
            probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(2.6f, 2.3f, 2.4f); probe.resolution = 128; probe.boxProjection = true;
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<DesktopRig>();
            var reticle = AssetDatabase.LoadAssetAtPath<Material>(GazeRig.ReticleMaterialPath);
            _gaze = GazeRig.AttachTo(cam, reticle, _font, keepDesktopMouse: true);
            return (cam, rig);
        }

        private static void BuildPostProcessing(Camera cam, string folder, string sceneName)
        {
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            string path = $"{folder}/{sceneName}_Volume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(0.9f); bloom.intensity.Override(0.8f); bloom.scatter.Override(0.6f);
            var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.28f); vignette.smoothness.Override(0.45f);
            var color = profile.Add<ColorAdjustments>(true); color.postExposure.Override(0.45f); color.contrast.Override(10f); color.saturation.Override(4f);
            var split = profile.Add<SplitToning>(true);   // cinematic: cool shadows, warm highlights
            split.shadows.Override(new Color(0.42f, 0.48f, 0.56f)); split.highlights.Override(new Color(0.58f, 0.54f, 0.48f)); split.balance.Override(-10f);
            AssetDatabase.CreateAsset(profile, path);
            foreach (var c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            var vol = new GameObject("Global_Volume").AddComponent<Volume>();
            vol.isGlobal = true; vol.sharedProfile = profile;
        }

        /// <summary>Sets UnityEditor.WebGL.UserBuildSettings.codeOptimization by reflection (WebGL module may be absent).</summary>
        private static void SetWebGLCodeOptimization(string value)
        {
            try
            {
                var t = Type.GetType("UnityEditor.WebGL.UserBuildSettings, UnityEditor.WebGL.Extensions");
                var prop = t?.GetProperty("codeOptimization", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop == null) { Debug.LogWarning("[AstraSceneBuilder] WebGL codeOptimization setting not found; using the default."); return; }
                prop.SetValue(null, Enum.Parse(prop.PropertyType, value));
                Debug.Log($"[AstraSceneBuilder] WebGL code optimization = {prop.GetValue(null)}");
            }
            catch (Exception ex) { Debug.LogWarning($"[AstraSceneBuilder] could not set WebGL code optimization: {ex.Message}"); }
        }

        // ------------------------------------------------------------------ helpers

        private static string _label;          // display label of the control being built (gaze reticle text)
        private static GazeInteractor _gaze;   // phone gaze rig of the level being built

        private static Transform ControlRoot(string id, Transform panel, Vector2 at)
        {
            var root = new GameObject(id).transform;   // object name == targetId (Blender/Unity naming convention)
            root.SetParent(panel, false);
            root.localPosition = new Vector3(at.x, at.y, -0.015f);
            root.localRotation = Quaternion.identity;
            return root;
        }

        private static Transform Pivot(Transform root, float z)
        {
            var pivot = new GameObject("__part").transform;
            pivot.SetParent(root, false); pivot.localPosition = new Vector3(0, 0, z);
            return pivot;
        }

        private static void Target(GameObject go, string id, TestTarget.Kind kind, string evt, Transform part, Renderer highlight, TMP_Text state, Vector3? hitSize, string accepts = null)
        {
            var col = go.AddComponent<BoxCollider>();   // every target gets a hit box
            if (hitSize.HasValue)
            {
                col.size = hitSize.Value;                 // unscaled roots: size in metres (>= 6 cm)
                col.center = new Vector3(0, 0, -hitSize.Value.z / 2f + 0.01f);
            }
            go.AddComponent<TestTarget>().Configure(id, evt, kind, part, highlight, state, accepts, _label);
        }

        private static Vector3 Face(Transform panel, Vector2 at, float outward = 0.016f) =>
            panel.position + panel.right * at.x + panel.up * at.y - panel.forward * outward;

        private static Vector3 V3(float[] a) => new Vector3(a[0], a[1], a[2]);

        private static string CamelCase(string snake) =>
            Regex.Replace(snake, "_([a-z0-9])", m => m.Groups[1].Value.ToUpperInvariant());

        private static string EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
            return path;
        }

        private static Transform Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());   // rays only hit the dedicated hit boxes
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        /// <summary>Depth-correct TextMeshPro placard. <paramref name="size"/> x 7 = text height in metres.</summary>
        private static TMP_Text Label(string name, Transform parent, Vector3 worldPos, Quaternion rot, string text, float size, Color color, float maxWidth = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            go.transform.rotation = rot;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = _font; tmp.text = text; tmp.fontSize = 10f; tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(100f, 20f);   // wide box: placards never wrap
            tmp.ForceMeshUpdate();
            float measured = Mathf.Max(0.0001f, tmp.textBounds.size.y);
            go.transform.localScale *= (size * 7f) / measured;
            if (maxWidth > 0f)   // shrink long labels so they never spill past their control
            {
                float width = tmp.textBounds.size.x * go.transform.lossyScale.x;
                if (width > maxWidth) go.transform.localScale *= maxWidth / width;
            }
            return tmp;
        }

        /// <summary>Cylinder whose axis points out of the panel (local Z). Diameter and depth in metres.</summary>
        private static Transform Cyl(string name, Transform parent, Vector3 localPos, float diameter, float depth, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, depth / 2f, diameter);   // primitive is 2 units tall
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        /// <summary>PBR material with procedurally generated albedo + normal maps (cached PNGs, no downloads).</summary>
        private static Material Surface(string name, string pattern, Color tint, float smoothness, float metallic, Vector2 tiling, float bump)
        {
            if (Mats.TryGetValue(name, out var cached)) return cached;
            var (albedo, normal) = PatternTextures(pattern);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetTexture("_BaseMap", albedo); m.SetColor("_BaseColor", tint);
            m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
            m.SetTextureScale("_BaseMap", tiling);
            AssetDatabase.CreateAsset(m, $"{MatDir}/{name}.mat");
            Mats[name] = m;
            return m;
        }

        private static (Texture2D, Texture2D) PatternTextures(string pattern)
        {
            string dir = $"{SharedDir}/Textures";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(SharedDir, "Textures");
            string aPath = $"{dir}/{pattern}_albedo.png", nPath = $"{dir}/{pattern}_normal.png";
            if (!File.Exists(aPath) || !File.Exists(nPath))
            {
                const int N = 512;
                var height = new float[N * N]; var albedo = new Color[N * N];
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N, h, a;
                    switch (pattern)
                    {
                        case "quilt":   // padded wall: soft cushions between stitched seams
                        {
                            float cu = Mathf.Repeat(u * 4f, 1f), cv = Mathf.Repeat(v * 4f, 1f);
                            float cushion = Mathf.Sin(cu * Mathf.PI) * Mathf.Sin(cv * Mathf.PI);
                            h = Mathf.Pow(cushion, 0.6f) + 0.05f * Mathf.PerlinNoise(u * 60f, v * 60f);
                            a = 0.9f + 0.1f * Mathf.PerlinNoise(u * 25f, v * 25f) - (cushion < 0.08f ? 0.18f : 0f);
                            break;
                        }
                        case "panel":   // smooth wall panels: 2x2 per tile, thin recessed seams, faint fabric weave
                        {
                            float pu = Mathf.Repeat(u * 2f, 1f), pv = Mathf.Repeat(v * 2f, 1f);
                            float edge = Mathf.Min(Mathf.Min(pu, 1f - pu), Mathf.Min(pv, 1f - pv));
                            float seamH = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.012f));
                            float weave = 0.5f + 0.5f * Mathf.Sin(u * 900f) * Mathf.Sin(v * 900f);
                            h = seamH * (0.97f + 0.03f * weave);
                            a = 0.93f + 0.04f * Mathf.PerlinNoise(u * 12f, v * 12f) + 0.02f * weave - (1f - seamH) * 0.35f;
                            break;
                        }
                        case "diamond": // diamond-plate floor: raised slanted lozenges
                        {
                            float du = Mathf.Repeat(u * 8f + (Mathf.Floor(v * 8f) % 2) * 0.5f, 1f) - 0.5f, dv = Mathf.Repeat(v * 8f, 1f) - 0.5f;
                            float r = new Vector2(du + dv, (du - dv) * 3.2f).magnitude;
                            h = Mathf.Clamp01(1f - r * 2.4f);
                            a = 0.8f + 0.2f * Mathf.PerlinNoise(u * 18f, v * 18f);
                            break;
                        }
                        default:        // brushed metal panel with light grime toward the edges
                        {
                            float streak = Mathf.PerlinNoise(u * 2f, v * 180f);
                            float edgeD = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                            h = 0.15f * streak;
                            a = 0.82f + 0.12f * streak + 0.06f * Mathf.PerlinNoise(u * 9f, v * 9f) - (edgeD < 0.04f ? 0.12f * (1f - edgeD / 0.04f) : 0f);
                            break;
                        }
                    }
                    height[y * N + x] = h;
                    albedo[y * N + x] = new Color(a, a, a, 1f);
                }
                var normal = new Color[N * N];
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float hl = height[y * N + (x + N - 1) % N], hr = height[y * N + (x + 1) % N];
                    float hd = height[((y + N - 1) % N) * N + x], hu = height[((y + 1) % N) * N + x];
                    var n = new Vector3((hl - hr) * 4f, (hd - hu) * 4f, 1f).normalized;
                    normal[y * N + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
                WritePng(aPath, N, albedo, linear: false);
                WritePng(nPath, N, normal, linear: true);
                if (AssetImporter.GetAtPath(nPath) is TextureImporter ti) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            }
            return (AssetDatabase.LoadAssetAtPath<Texture2D>(aPath), AssetDatabase.LoadAssetAtPath<Texture2D>(nPath));
        }

        private static void WritePng(string path, int n, Color[] px, bool linear)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, linear);
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        private static Material Mat(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            if (Mats.TryGetValue(name, out var cached)) return cached;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(m, $"{MatDir}/{name}.mat");
            Mats[name] = m;
            return m;
        }
    }
}
