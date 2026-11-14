using System;
using Astra.App;
using Astra.Core.Assessment;
using Astra.UI.Hub;
using Astra.XR.Feedback;
using Astra.XR.Input;
using Astra.XR.Interactables;
using Astra.XR.Rig;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Astra.UI.Editor
{
    /// <summary>
    /// Generates P1's rig and pause prefabs, shared materials/art and the Hub, L0 and L1 scenes.
    /// Layout follows CONTRACT.md §8 comfort rules: seated rig at the origin facing +Z, every target within
    /// 0.7 m horizontally and 0.7–1.5 m high, nothing behind the cadet, interactables at least 6 cm.
    /// The positions are documented in each level folder's README (scene build spec).
    /// Scenes and P1 prefabs are generated artifacts: rebuilding overwrites them in place (GUIDs kept).
    /// </summary>
    public static class P1SceneBuilder
    {
        public const string HubScene = "Assets/ASTRA/Scenes/Hub.unity";
        public const string L0Scene = "Assets/ASTRA/Levels/L0_Safety/L0_Safety.unity";
        public const string L1Scene = "Assets/ASTRA/Levels/L1_Orientation/L1_Orientation.unity";
        public static readonly string[] Scenes = { HubScene, L0Scene, L1Scene };

        private const string RigPrefab = "Assets/ASTRA/XR/Prefabs/AstraXRRig.prefab";
        private const string PausePrefab = "Assets/ASTRA/UI/Prefabs/PauseMenu.prefab";
        private const string Materials = "Assets/ASTRA/XR/Materials";
        private const string Missions = "Assets/ASTRA/Content/Missions";
        private const float EyeHeight = 1.2f;
        private const float DeskTop = 0.78f;
        private static readonly Vector3 Eye = new Vector3(0f, EyeHeight, 0f);

        private static readonly Color LabelPlate = new Color(0.02f, 0.035f, 0.06f, 0.88f);

        [MenuItem("ASTRA/P1/Rebuild Hub, L0 and L1 scenes (overwrites scenes)")]
        public static void BuildAll()
        {
            UiPrefabBuilder.RebuildUiPrefabs();
            EnsureMaterials();
            BuildPausePrefab();
            BuildRigPrefab();
            BuildHub();
            BuildL0();
            BuildL1();
            AssetDatabase.SaveAssets();
            Debug.Log("[P1SceneBuilder] Built " + string.Join(", ", Scenes));
        }

        // ------------------------------------------------------------------ materials

        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Materials + "/" + name + ".mat");

        private static void EnsureMaterials()
        {
            EnsureFolder(Materials);
            var urp = GraphicsSettings.defaultRenderPipeline != null;
            var lit = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            var unlit = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Texture");
            var vertexColor = Shader.Find("Sprites/Default");
            if (lit == null || unlit == null || vertexColor == null)
                throw new InvalidOperationException("Required lit/unlit shaders are unavailable in this render pipeline.");
            Material(lit, "Floor", new Color(0.07f, 0.085f, 0.11f), 0.55f);
            Material(lit, "Wall", new Color(0.16f, 0.19f, 0.24f), 0.3f);
            Material(lit, "Trim", new Color(0.28f, 0.36f, 0.46f), 0.5f, 0.4f);
            Material(lit, "Desk", new Color(0.3f, 0.34f, 0.4f), 0.45f, 0.2f);
            Material(lit, "Housing", new Color(0.07f, 0.085f, 0.11f), 0.6f, 0.3f);
            Material(lit, "Control", Color.white, 0.6f);
            Material(lit, "Target", Color.white, 0.4f);
            Material(lit, "Tool", new Color(0.98f, 0.6f, 0.12f), 0.55f);
            Material(lit, "Metal", new Color(0.72f, 0.75f, 0.8f), 0.8f, 0.9f);
            Material(lit, "Bay", new Color(0.12f, 0.24f, 0.32f), 0.4f);
            Material(lit, "Planet", new Color(0.22f, 0.48f, 0.72f), 0.2f);
            Material(unlit, "Ray", new Color(0.35f, 0.9f, 1f));
            Material(unlit, "Light", new Color(0.85f, 0.93f, 1f));
            Material(unlit, "Glow", new Color(0.3f, 0.85f, 1f));
            Material(unlit, "Amber", new Color(1f, 0.62f, 0.2f));
            Material(unlit, "Screen", new Color(0.05f, 0.22f, 0.3f));
            Material(vertexColor, "Reticle", Color.white);
            var stars = Material(unlit, "Starfield", Color.white);
            var texture = P1Art.Starfield;
            if (stars.HasProperty("_BaseMap"))
                stars.SetTexture("_BaseMap", texture);
            if (stars.HasProperty("_MainTex"))
                stars.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(stars);
        }

        /// <summary>Creates the material, or updates an existing one in place so its GUID is kept.</summary>
        private static Material Material(Shader shader, string name, Color color, float smoothness = 0.25f, float metallic = 0f)
        {
            var path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
                material.shader = shader;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ prefabs

        private static void BuildRigPrefab()
        {
            EnsureFolder("Assets/ASTRA/XR/Prefabs");
            var root = new GameObject("AstraXRRig");
            try
            {
                var space = Child(root.transform, "TrackingSpace", new Vector3(0f, EyeHeight, 0f));
                var head = Child(space, "Head", Vector3.zero);
                head.localRotation = Quaternion.Euler(12f, 0f, 0f);
                head.gameObject.tag = "MainCamera";
                var camera = head.gameObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.03f;
                camera.farClipPlane = 60f;
                camera.fieldOfView = 75f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.01f, 0.015f, 0.035f, 1f);
                head.gameObject.AddComponent<AudioListener>();
                var headPose = head.gameObject.AddComponent<HeadPoseDriver>();

                var desktop = Child(root.transform, "DesktopPointer", Vector3.zero);
                var desktopTag = desktop.gameObject.AddComponent<InputModalityTag>();
                desktopTag.Configure(InputModality.ControllerRay, isSimulator: true);
                var desktopRay = desktop.gameObject.AddComponent<RayInteractor>();
                var desktopInput = desktop.gameObject.AddComponent<DesktopMouseInput>();
                desktopInput.Configure(desktopRay, desktopTag, camera, headPose);

                // Phone headset baseline: head gaze + headset-button tap.
                var gaze = Child(root.transform, "GazeTap", Vector3.zero);
                var gazeRay = gaze.gameObject.AddComponent<RayInteractor>();
                var gazeTag = Child(gaze, "Gaze Ray", Vector3.zero).gameObject.AddComponent<InputModalityTag>();
                gazeTag.Configure(InputModality.ControllerRay);
                var previewTag = Child(gaze, "Desktop Preview", Vector3.zero).gameObject.AddComponent<InputModalityTag>();
                previewTag.Configure(InputModality.ControllerRay, isSimulator: true);
                var ring = Child(gaze, "Reticle", Vector3.zero).gameObject.AddComponent<LineRenderer>();
                ring.shadowCastingMode = ShadowCastingMode.Off;
                ring.receiveShadows = false;
                ring.sharedMaterial = Mat("Reticle");
                ring.numCornerVertices = 2;
                ring.enabled = false;
                var reticle = ring.gameObject.AddComponent<GazeReticle>();
                gaze.gameObject.AddComponent<GazeTapInput>().Configure(gazeRay, gazeTag, previewTag, headPose, head, reticle);

                var hand = Child(root.transform, "HandAim", Vector3.zero);
                var handRay = hand.gameObject.AddComponent<RayInteractor>();
                var pinch = Child(hand, "Pinch", Vector3.zero).gameObject.AddComponent<InputModalityTag>();
                pinch.Configure(InputModality.HandPinch);
                var grab = Child(hand, "Grab", Vector3.zero).gameObject.AddComponent<InputModalityTag>();
                grab.Configure(InputModality.HandGrab);
                var line = hand.gameObject.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.startWidth = 0.004f;
                line.endWidth = 0.002f;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = Mat("Ray");
                line.enabled = false;
                var cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cursor.name = "Cursor";
                Object.DestroyImmediate(cursor.GetComponent<Collider>());
                cursor.transform.SetParent(hand, false);
                cursor.transform.localScale = Vector3.one * 0.012f;
                cursor.GetComponent<Renderer>().sharedMaterial = Mat("Ray");
                cursor.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                cursor.SetActive(false);
                var handInput = hand.gameObject.AddComponent<HandAimInput>();
                handInput.Configure(handRay, pinch, grab, space, headPose, line, cursor.transform);
                handInput.HandleMenuButton = false; // the gaze route owns the headset button

                root.AddComponent<RigRecenterService>().Configure(headPose, desktopInput);
                Save(root, RigPrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildPausePrefab()
        {
            var root = new GameObject("PauseMenu");
            try
            {
                var pause = root.AddComponent<PauseMenu>();

                var pauseCanvas = WorldCanvas(root.transform, "PauseButton", new Vector3(0.34f, 1.02f, 0.44f), 280f, 120f, 0.0006f);
                var pauseButton = MakeButton(pauseCanvas, "Pause", "PAUSE", 0f, 0f, 280f, 120f, 40);
                UnityEventTools.AddPersistentListener(pauseButton.onClick, pause.Pause);

                var menu = WorldCanvas(root.transform, "MenuRoot", new Vector3(0f, 1.1f, 0.5f), 640f, 780f, 0.0006f);
                UiPrefabBuilder.Rounded(menu.gameObject.AddComponent<Image>(), UiPrefabBuilder.Background);
                UiPrefabBuilder.AccentHeader(menu);
                var title = MakeLabel(menu, "Title", "PAUSED", 48, 0f, 318f, 580f, 74f, TextAnchor.MiddleCenter);
                title.fontStyle = FontStyle.Bold;
                title.color = UiPrefabBuilder.Accent;
                var resume = MakeButton(menu, "Resume", "RESUME", 0f, 200f, 500f, 96f, 38);
                var recenter = MakeButton(menu, "Recenter", "RECENTER", 0f, 88f, 500f, 96f, 38);
                var restart = MakeButton(menu, "Restart", "RESTART", 0f, -24f, 500f, 96f, 38);
                var exit = MakeButton(menu, "Exit", "EXIT TO HUB", 0f, -136f, 500f, 96f, 38);
                var finish = MakeButton(menu, "Finish", "FINISH", 0f, -280f, 500f, 96f, 38);
                var colors = finish.colors;
                colors.normalColor = new Color(0.5f, 0.28f, 0.1f, 1f);
                colors.highlightedColor = new Color(0.72f, 0.42f, 0.15f, 1f);
                colors.selectedColor = colors.normalColor;
                finish.colors = colors;
                UnityEventTools.AddPersistentListener(resume.onClick, pause.Resume);
                UnityEventTools.AddPersistentListener(recenter.onClick, pause.Recenter);
                UnityEventTools.AddPersistentListener(restart.onClick, pause.RequestRestart);
                UnityEventTools.AddPersistentListener(exit.onClick, pause.RequestExit);
                UnityEventTools.AddPersistentListener(finish.onClick, pause.RequestFinish);
                pause.Configure(menu.gameObject, finish.gameObject, null);
                Save(root, PausePrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ scenes

        private sealed class LevelRig
        {
            public GameObject Rig;
            public Camera Camera;
            public InputModalityTag DesktopTag;
            public DesktopMouseInput Desktop;
            public HandAimInput Hand;
            public GazeTapInput Gaze;
            public HeadPoseDriver Head;
            public RigRecenterService Recenter;
        }

        private static LevelRig NewScene(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.48f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.33f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.15f, 0.18f);

            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 4.5f;
            fill.intensity = 0.7f;
            fill.color = new Color(0.65f, 0.82f, 1f);
            fill.shadows = LightShadows.None;
            fill.transform.position = new Vector3(0f, 2.3f, 0.9f);

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            // Keyboard submit would re-click the last selected button (Space is the gaze-preview tap).
            events.GetComponent<EventSystem>().sendNavigationEvents = false;
            events.transform.SetAsLastSibling();

            var rigInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab), scene);
            var rig = new LevelRig
            {
                Rig = rigInstance,
                Camera = rigInstance.GetComponentInChildren<Camera>(true),
                Desktop = rigInstance.GetComponentInChildren<DesktopMouseInput>(true),
                Hand = rigInstance.GetComponentInChildren<HandAimInput>(true),
                Gaze = rigInstance.GetComponentInChildren<GazeTapInput>(true),
                Head = rigInstance.GetComponentInChildren<HeadPoseDriver>(true),
                Recenter = rigInstance.GetComponent<RigRecenterService>()
            };
            rig.DesktopTag = rig.Desktop.GetComponent<InputModalityTag>();
            BuildRoom();
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            return rig;
        }

        /// <summary>Fictional ASTRA station module: hidden in AR so only the desk, controls and panels remain.</summary>
        private static void BuildRoom()
        {
            var room = new GameObject("Environment (hidden in AR)");
            room.AddComponent<PassthroughHidden>();
            var t = room.transform;
            Box(t, "Floor", new Vector3(0f, -0.01f, 0.6f), new Vector3(6f, 0.02f, 5f), "Floor");
            for (var i = -1; i <= 1; i += 2)
                Box(t, "Floor Guide", new Vector3(i * 0.95f, 0.002f, 0.9f), new Vector3(0.03f, 0.004f, 3.2f), "Glow");

            // Front wall with an observation window onto a fictional planet.
            const float windowBottom = 0.75f, windowTop = 2.25f, windowHalf = 1.7f, wallZ = 2.6f;
            Box(t, "Front Wall Lower", new Vector3(0f, windowBottom * 0.5f, wallZ), new Vector3(6f, windowBottom, 0.05f), "Wall");
            Box(t, "Front Wall Upper", new Vector3(0f, (windowTop + 3f) * 0.5f, wallZ), new Vector3(6f, 3f - windowTop, 0.05f), "Wall");
            var pillarWidth = 3f - windowHalf;
            for (var i = -1; i <= 1; i += 2)
                Box(t, "Front Wall Pillar", new Vector3(i * (windowHalf + pillarWidth * 0.5f), 1.5f, wallZ),
                    new Vector3(pillarWidth, windowTop - windowBottom, 0.05f), "Wall");
            var middle = (windowTop + windowBottom) * 0.5f;
            Box(t, "Window Sill", new Vector3(0f, windowBottom, wallZ - 0.03f), new Vector3(windowHalf * 2f + 0.12f, 0.06f, 0.1f), "Trim");
            Box(t, "Window Head", new Vector3(0f, windowTop, wallZ - 0.03f), new Vector3(windowHalf * 2f + 0.12f, 0.06f, 0.1f), "Trim");
            for (var i = -1; i <= 1; i += 2)
            {
                Box(t, "Window Jamb", new Vector3(i * windowHalf, middle, wallZ - 0.03f), new Vector3(0.06f, windowTop - windowBottom, 0.1f), "Trim");
                Box(t, "Window Mullion", new Vector3(i * windowHalf / 3f, middle, wallZ - 0.02f), new Vector3(0.035f, windowTop - windowBottom, 0.05f), "Trim");
            }
            Box(t, "Window Light Strip", new Vector3(0f, windowBottom + 0.035f, wallZ - 0.085f), new Vector3(windowHalf * 2f, 0.012f, 0.012f), "Glow");

            var stars = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stars.name = "Starfield";
            stars.transform.SetParent(t, false);
            stars.transform.position = new Vector3(0f, 1.6f, 16f);
            stars.transform.localScale = new Vector3(40f, 20f, 1f);
            Finish(stars, "Starfield");
            var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Planet (fictional)";
            planet.transform.SetParent(t, false);
            planet.transform.position = new Vector3(2.8f, -0.6f, 11f);
            planet.transform.localScale = Vector3.one * 8f;
            Finish(planet, "Planet");

            Box(t, "Left Wall", new Vector3(-2.4f, 1.5f, 0.6f), new Vector3(0.05f, 3f, 5f), "Wall");
            Box(t, "Right Wall", new Vector3(2.4f, 1.5f, 0.6f), new Vector3(0.05f, 3f, 5f), "Wall");
            Box(t, "Back Wall", new Vector3(0f, 1.5f, -1.9f), new Vector3(6f, 3f, 0.05f), "Wall");
            Box(t, "Ceiling", new Vector3(0f, 3f, 0.6f), new Vector3(6f, 0.05f, 5f), "Wall");
            for (var side = -1; side <= 1; side += 2)
            {
                for (var z = -1; z <= 2; z++)
                    Box(t, "Wall Rib", new Vector3(side * 2.37f, 1.5f, z * 0.9f + 0.4f), new Vector3(0.06f, 3f, 0.1f), "Trim");
                Box(t, "Wall Light Strip", new Vector3(side * 2.36f, 2.4f, 0.6f), new Vector3(0.02f, 0.03f, 4.6f), "Glow");
                // Side consoles give the module depth; decoration only, out of reach.
                var console = new GameObject("Side Console").transform;
                console.SetParent(t, false);
                console.position = new Vector3(side * 1.55f, 0f, 1.25f);
                console.rotation = Quaternion.Euler(0f, side * -55f, 0f);
                Box(console, "Body", console.TransformPoint(new Vector3(0f, 0.5f, 0f)), new Vector3(0.9f, 1f, 0.5f), "Housing").rotation = console.rotation;
                var screen = Box(console, "Screen", console.TransformPoint(new Vector3(0f, 1.25f, 0.12f)), new Vector3(0.8f, 0.45f, 0.03f), "Screen");
                screen.rotation = console.rotation * Quaternion.Euler(-12f, 0f, 0f);
                var bar = Box(console, "Screen Bar", console.TransformPoint(new Vector3(0f, 1.05f, 0.1f)), new Vector3(0.8f, 0.015f, 0.02f), "Glow");
                bar.rotation = console.rotation;
            }
            for (var i = -1; i <= 1; i++)
                Box(t, "Ceiling Light", new Vector3(i * 1.4f, 2.97f, 0.9f), new Vector3(0.9f, 0.02f, 0.2f), "Light");
            foreach (var collider in room.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);
            foreach (var renderer in room.GetComponentsInChildren<Renderer>())
                renderer.receiveShadows = renderer.name != "Planet (fictional)" && renderer.name != "Starfield";
        }

        private static Transform BuildDesk()
        {
            var desk = new GameObject("Training Desk").transform;
            Box(desk, "Desk Top", new Vector3(0f, DeskTop - 0.025f, 0.5f), new Vector3(1.1f, 0.05f, 0.5f), "Desk");
            var trims = new[]
            {
                Box(desk, "Desk Edge Light", new Vector3(0f, DeskTop - 0.03f, 0.248f), new Vector3(1.1f, 0.012f, 0.008f), "Glow"),
                Box(desk, "Desk Base", new Vector3(0f, 0.37f, 0.7f), new Vector3(1.0f, 0.72f, 0.06f), "Housing"),
                Box(desk, "Desk Leg L", new Vector3(-0.5f, 0.37f, 0.5f), new Vector3(0.05f, 0.72f, 0.42f), "Housing"),
                Box(desk, "Desk Leg R", new Vector3(0.5f, 0.37f, 0.5f), new Vector3(0.05f, 0.72f, 0.42f), "Housing")
            };
            foreach (var trim in trims)
                Object.DestroyImmediate(trim.GetComponent<Collider>());
            return desk;
        }

        private static void BuildHub()
        {
            var rig = NewScene(HubScene);
            var header = WorldCanvas(null, "Hub Title", new Vector3(0f, 1.6f, 1.05f), 1100f, 190f, 0.0008f);
            UiPrefabBuilder.Rounded(header.gameObject.AddComponent<Image>(), UiPrefabBuilder.Background);
            UiPrefabBuilder.AccentHeader(header);
            var title = MakeLabel(header, "Title", "ASTRA ACADEMY", 72, 0f, 28f, 1060f, 96f, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            MakeLabel(header, "Subtitle", "Fictional ASTRA Station cadet training. Choose an available level.", 30, 0f, -52f, 1060f, 44f,
                TextAnchor.MiddleCenter).color = UiPrefabBuilder.Accent;
            Face(header);

            // Cards sit on an arc around the seated cadet so each one faces the eye at the same distance.
            var cards = new LevelCard[LevelCatalog.Levels.Count];
            var yaws = new[] { -22.5f, -7.5f, 7.5f, 22.5f };
            var rows = new[] { 1.36f, 1.06f };
            for (var i = 0; i < cards.Length; i++)
            {
                var entry = LevelCatalog.Levels[i];
                var yaw = yaws[i % 4] * Mathf.Deg2Rad;
                var y = rows[i / 4];
                var position = new Vector3(Mathf.Sin(yaw), y - EyeHeight, Mathf.Cos(yaw)) * 1f + new Vector3(0f, EyeHeight, 0f);
                position.y = y;
                var canvas = WorldCanvas(null, "L" + entry.Level + " Card", position, 300f, 330f, 0.0008f);
                Face(canvas);
                var button = MakeButton(canvas, "Card Button", string.Empty, 0f, 0f, 300f, 330f, 24);
                Object.DestroyImmediate(button.transform.Find("Label").gameObject);
                var level = MakeLabel(button.transform, "Level", "LEVEL " + entry.Level, 26, 0f, 125f, 270f, 40f, TextAnchor.MiddleCenter);
                level.fontStyle = FontStyle.Bold;
                level.color = UiPrefabBuilder.Accent;
                var cardTitle = MakeLabel(button.transform, "Title", entry.Title, 34, 0f, 22f, 270f, 150f, TextAnchor.MiddleCenter);
                cardTitle.fontStyle = FontStyle.Bold;
                var status = MakeLabel(button.transform, "Status", string.Empty, 28, 0f, -122f, 270f, 50f, TextAnchor.MiddleCenter);
                status.color = new Color(0.82f, 0.94f, 1f);
                var card = button.gameObject.AddComponent<LevelCard>();
                card.Configure(entry.Level, entry.Title, button, cardTitle, status);
                cards[i] = card;
            }

            var footer = WorldCanvas(null, "Hub Footer", new Vector3(0f, 0.86f, 0.95f), 1100f, 110f, 0.0008f);
            UiPrefabBuilder.Rounded(footer.gameObject.AddComponent<Image>(), LabelPlate);
            var hint = MakeLabel(footer, "Controls", ControlsHint.DesktopText, 28, 0f, 22f, 1060f, 44f, TextAnchor.MiddleCenter);
            footer.gameObject.AddComponent<ControlsHint>().Configure(hint, rig.Head, rig.Gaze);
            var notice = MakeLabel(footer, "Progress Notice", string.Empty, 26, 0f, -26f, 1060f, 40f, TextAnchor.MiddleCenter);
            notice.color = UiPrefabBuilder.Accent;
            Face(footer);

            var quitCanvas = WorldCanvas(null, "Quit", new Vector3(0f, 0.74f, 0.8f), 280f, 100f, 0.0007f);
            var quit = MakeButton(quitCanvas, "Quit", "QUIT", 0f, 0f, 280f, 100f, 38);
            var quitter = quitCanvas.gameObject.AddComponent<ApplicationQuit>();
            UnityEventTools.AddPersistentListener(quit.onClick, quitter.Quit);
            Face(quitCanvas);

            var hub = new GameObject("Hub");
            var controller = hub.AddComponent<HubController>();
            controller.Configure(cards, notice);
            hub.AddComponent<HubProgressBinding>();
            FinishScene(rig, HubScene);
        }

        private static PauseMenu AddLevelUi(LevelRig rig, string missionFile, out LevelController controller)
        {
            var ui = new GameObject("Level UI").transform;
            var banner = PlaceUi(ui, "PhaseBanner", new Vector3(0f, 1.545f, 1.0f), 0.0008f).GetComponent<PhaseBanner>();
            var instruction = PlaceUi(ui, "InstructionPanel", new Vector3(0f, 1.33f, 1.0f), 0.0008f).GetComponent<InstructionPanel>();
            PlaceUi(ui, "AlarmIndicator", new Vector3(0.62f, 1.5f, 0.8f), 0.0005f);
            var debrief = PlaceUi(ui, "DebriefPanel", new Vector3(0f, 1.16f, 0.6f), 0.0006f);
            debrief.SetActive(false);

            var controls = WorldCanvas(ui, "Controls Hint", new Vector3(0f, 1.125f, 1.0f), 1000f, 56f, 0.0008f);
            UiPrefabBuilder.Rounded(controls.gameObject.AddComponent<Image>(), LabelPlate);
            var hint = MakeLabel(controls, "Text", ControlsHint.DesktopText, 28, 0f, 0f, 980f, 56f, TextAnchor.MiddleCenter);
            hint.color = UiPrefabBuilder.Accent;
            controls.gameObject.AddComponent<ControlsHint>().Configure(hint, rig.Head, rig.Gaze);
            Face(controls);

            var pauseObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefab));
            var pause = pauseObject.GetComponent<PauseMenu>();
            foreach (Transform child in pauseObject.transform)
                Face(child);
            SetField(pause, "defaultInteractor", rig.DesktopTag);
            SetField(pause, "recenterHandler", rig.Recenter);
            UnityEventTools.AddPersistentListener(rig.Desktop.onMenuPressed, pause.Toggle);
            UnityEventTools.AddPersistentListener(rig.Gaze.onMenuPressed, pause.Toggle);
            UnityEventTools.AddPersistentListener(rig.Hand.onMenuPressed, pause.Toggle);

            var host = new GameObject("LevelController");
            controller = host.AddComponent<LevelController>();
            var mission = AssetDatabase.LoadAssetAtPath<TextAsset>(Missions + "/" + missionFile);
            if (mission == null)
                throw new InvalidOperationException("Mission asset missing: " + missionFile);
            SetField(controller, "missionJson", mission);
            SetField(controller, "instructionPanel", instruction);
            SetField(controller, "phaseBanner", banner);
            SetField(controller, "debriefPanel", debrief.GetComponent<DebriefPanel>());
            SetField(controller, "pauseMenu", pause);
            return pause;
        }

        private static void BuildL0()
        {
            var rig = NewScene(L0Scene);
            var desk = BuildDesk();
            AddLevelUi(rig, "l0-safety-v1.json", out _);

            var confirm = PhysicalButton(desk, "CONFIRM POSITION", new Vector3(-0.14f, DeskTop, 0.43f), new Vector2(0.16f, 0.1f), false,
                new Color(0.55f, 0.78f, 1f));
            confirm.Configure(SystemTargets.Calibration, EventNames.CalibrationCompleted);
            ObjectLabel(desk, "CONFIRM POSITION", new Vector3(-0.14f, 0.89f, 0.46f), 360f);
            ObjectLabel(desk, "Sit upright, then confirm your position. Recenter is in the pause menu.",
                new Vector3(0f, 1.03f, 0.62f), 560f, 28, 120f);
            FinishScene(rig, L0Scene);
        }

        private static void BuildL1()
        {
            var rig = NewScene(L1Scene);
            var desk = BuildDesk();
            AddLevelUi(rig, "l1-orientation-v1.json", out _);

            // TARGET: an upright framed screen on the left.
            var panel = new GameObject("TARGET").transform;
            panel.SetParent(desk, false);
            panel.position = new Vector3(-0.38f, 1.08f, 0.55f);
            Face(panel);
            var frame = Box(panel, "Frame", panel.position, new Vector3(0.24f, 0.18f, 0.02f), "Housing");
            frame.rotation = panel.rotation;
            var screen = Box(panel, "Screen", panel.TransformPoint(new Vector3(0f, 0f, -0.011f)), new Vector3(0.2f, 0.14f, 0.004f), "Target");
            screen.rotation = panel.rotation;
            var halo = Box(panel, "Hover Frame", panel.TransformPoint(new Vector3(0f, 0f, 0.006f)), new Vector3(0.27f, 0.21f, 0.01f), "Glow");
            halo.rotation = panel.rotation;
            var mark = Box(panel, "Crosshair H", panel.TransformPoint(new Vector3(0f, 0f, -0.014f)), new Vector3(0.08f, 0.006f, 0.002f), "Glow");
            mark.rotation = panel.rotation;
            var markV = Box(panel, "Crosshair V", panel.TransformPoint(new Vector3(0f, 0f, -0.014f)), new Vector3(0.006f, 0.08f, 0.002f), "Glow");
            markV.rotation = panel.rotation;
            foreach (var part in new[] { frame, screen, halo, mark, markV })
                Object.DestroyImmediate(part.GetComponent<Collider>());
            var panelCollider = panel.gameObject.AddComponent<BoxCollider>();
            panelCollider.size = new Vector3(0.24f, 0.18f, 0.03f);
            var select = panel.gameObject.AddComponent<ButtonTarget>();
            select.Configure("orientation_target_panel", EventNames.TargetSelected);
            SetField(select, "feedbackRenderer", screen.GetComponent<Renderer>());
            SetControlColors(select, new Color(0.1f, 0.45f, 0.55f), new Color(0.3f, 0.85f, 1f), new Color(0.35f, 1f, 0.55f));
            panel.gameObject.AddComponent<PressFeedback>().Configure(select, null, halo.gameObject);
            panel.gameObject.AddComponent<LocatableTarget>().Configure("orientation_target_panel");
            ObjectLabel(desk, "TARGET", panel.position + new Vector3(0f, 0.14f, 0f));

            var start = PhysicalButton(desk, "START", new Vector3(0f, DeskTop, 0.42f), new Vector2(0.09f, 0.09f), true,
                new Color(0.45f, 0.95f, 0.55f));
            start.Configure("orientation_start_button");
            ObjectLabel(desk, "START", new Vector3(0f, 0.88f, 0.45f));

            var tool = new GameObject("TOOL").transform;
            tool.position = new Vector3(-0.2f, DeskTop + 0.021f, 0.45f);
            Strip(Box(tool, "Body", tool.position, new Vector3(0.13f, 0.04f, 0.055f), "Tool"));
            Strip(Box(tool, "Grip", tool.position + new Vector3(0.045f, 0.022f, 0f), new Vector3(0.05f, 0.008f, 0.06f), "Housing"));
            Strip(Box(tool, "Probe", tool.position + new Vector3(-0.085f, 0f, 0f), new Vector3(0.04f, 0.012f, 0.012f), "Metal"));
            Strip(Box(tool, "Status Light", tool.position + new Vector3(-0.02f, 0.021f, 0f), new Vector3(0.02f, 0.004f, 0.02f), "Glow"));
            tool.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.2f, 0.05f, 0.07f);
            var body = tool.gameObject.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            tool.gameObject.AddComponent<GrabbableTool>().Configure("orientation_practice_tool");
            ObjectLabel(tool, "TOOL", tool.position + new Vector3(0f, 0.07f, 0.02f));

            var bay = Box(desk, "TOOL BAY", new Vector3(0.24f, DeskTop + 0.01f, 0.45f), new Vector3(0.24f, 0.02f, 0.13f), "Bay");
            var lips = new[]
            {
                Box(bay, "Lip Front", new Vector3(0.24f, DeskTop + 0.02f, 0.38f), new Vector3(0.26f, 0.02f, 0.012f), "Housing"),
                Box(bay, "Lip Back", new Vector3(0.24f, DeskTop + 0.02f, 0.52f), new Vector3(0.26f, 0.02f, 0.012f), "Housing"),
                Box(bay, "Lip Left", new Vector3(0.11f, DeskTop + 0.02f, 0.45f), new Vector3(0.012f, 0.02f, 0.15f), "Housing"),
                Box(bay, "Lip Right", new Vector3(0.37f, DeskTop + 0.02f, 0.45f), new Vector3(0.012f, 0.02f, 0.15f), "Housing"),
                Box(bay, "Bay Guide", new Vector3(0.24f, DeskTop + 0.021f, 0.45f), new Vector3(0.14f, 0.002f, 0.004f), "Amber")
            };
            foreach (var lip in lips)
                Strip(lip);
            var socket = bay.gameObject.AddComponent<PlacementSocket>();
            socket.Configure("orientation_tool_socket", "orientation_practice_tool");
            var capture = new GameObject("Capture Volume").AddComponent<BoxCollider>();
            capture.transform.SetParent(bay, false);
            capture.isTrigger = true;
            capture.center = new Vector3(0f, 3f, 0f);
            capture.size = new Vector3(1.1f, 6f, 1.2f);
            var snap = new GameObject("Snap Point").transform;
            snap.SetParent(bay, false);
            snap.position = new Vector3(0.24f, DeskTop + 0.041f, 0.45f);
            SetField(socket, "snapPoint", snap);
            ObjectLabel(desk, "TOOL BAY", new Vector3(0.24f, 0.88f, 0.48f));
            FinishScene(rig, L1Scene);
        }

        /// <summary>
        /// A desk button: dark housing, coloured cap that travels when pressed and a glow frame shown on hover.
        /// <paramref name="position"/> is the desk-surface point under the button.
        /// </summary>
        private static ButtonTarget PhysicalButton(Transform parent, string name, Vector3 position, Vector2 footprint, bool round,
            Color idle)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            var housing = Box(root, "Housing", position + new Vector3(0f, 0.01f, 0f),
                new Vector3(footprint.x + 0.024f, 0.02f, footprint.y + 0.024f), "Housing");
            var halo = Box(root, "Hover Frame", position + new Vector3(0f, 0.004f, 0f),
                new Vector3(footprint.x + 0.05f, 0.008f, footprint.y + 0.05f), "Glow");
            Transform cap;
            if (round)
            {
                cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
                cap.name = "Cap";
                cap.SetParent(root, false);
                cap.position = position + new Vector3(0f, 0.031f, 0f);
                cap.localScale = new Vector3(footprint.x, 0.011f, footprint.y);
                cap.GetComponent<Renderer>().sharedMaterial = Mat("Control");
            }
            else
                cap = Box(root, "Cap", position + new Vector3(0f, 0.031f, 0f), new Vector3(footprint.x, 0.022f, footprint.y), "Control");
            foreach (var part in new[] { housing, halo, cap })
                Object.DestroyImmediate(part.GetComponent<Collider>());
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.022f, 0f);
            collider.size = new Vector3(footprint.x + 0.024f, 0.044f, footprint.y + 0.024f);
            var button = root.gameObject.AddComponent<ButtonTarget>();
            SetField(button, "feedbackRenderer", cap.GetComponent<Renderer>());
            SetControlColors(button, idle, new Color(0.35f, 0.9f, 1f), new Color(0.4f, 1f, 0.55f));
            root.gameObject.AddComponent<PressFeedback>().Configure(button, cap, halo.gameObject);
            return button;
        }

        private static void FinishScene(LevelRig rig, string path)
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                if (canvas.renderMode == RenderMode.WorldSpace)
                    canvas.worldCamera = rig.Camera;
            var scene = SceneManager.GetActiveScene();
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException("Could not save scene " + path);
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject PlaceUi(Transform parent, string prefab, Vector3 position, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ASTRA/UI/Prefabs/" + prefab + ".prefab");
            if (asset == null)
                throw new InvalidOperationException("UI prefab missing: " + prefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            instance.transform.localScale = Vector3.one * scale;
            Face(instance.transform);
            return instance;
        }

        /// <summary>Orients a world-space object so its front faces the seated eye point.</summary>
        private static void Face(Transform target)
        {
            var away = target.position - Eye;
            if (away.sqrMagnitude > 0.0001f)
                target.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        private static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, string material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            if (parent != null)
                box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            Finish(box, material);
            return box.transform;
        }

        private static void Finish(GameObject primitive, string material)
        {
            var renderer = primitive.GetComponent<Renderer>();
            renderer.sharedMaterial = Mat(material);
            var unlit = material == "Glow" || material == "Light" || material == "Amber" || material == "Screen" ||
                        material == "Starfield";
            if (unlit)
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Strip(Transform part) => Object.DestroyImmediate(part.GetComponent<Collider>());

        /// <summary>Caption on a dark rounded plate, so it stays readable over passthrough and on the phone.</summary>
        private static void ObjectLabel(Transform parent, string text, Vector3 position, float width = 300f, int size = 34,
            float height = 76f)
        {
            var canvas = WorldCanvas(null, text + " Label", position, width, height, 0.0005f);
            canvas.SetParent(parent, true);
            Face(canvas);
            UiPrefabBuilder.Rounded(canvas.gameObject.AddComponent<Image>(), LabelPlate);
            canvas.GetComponent<Image>().raycastTarget = false;
            var label = MakeLabel(canvas, "Text", text, size, 0f, 0f, width - 24f, height, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                Object.DestroyImmediate(raycaster);
        }

        private static RectTransform WorldCanvas(Transform parent, string name, Vector3 position, float width, float height, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            if (parent != null)
                rect.SetParent(parent, false);
            rect.position = position;
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one * scale;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            go.AddComponent<GraphicRaycaster>();
            return rect;
        }

        private static Text MakeLabel(Transform parent, string name, string value, int size, float x, float y, float width,
            float height, TextAnchor anchor)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = UiPrefabBuilder.Foreground;
            text.alignment = anchor;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button MakeButton(Transform parent, string name, string caption, float x, float y, float width,
            float height, int size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            var button = UiPrefabBuilder.StyleButton(rect.gameObject);
            var label = MakeLabel(rect, "Label", caption, size, 0f, 0f, width - 20f, height, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            return button;
        }

        private static void SetControlColors(ControlTarget control, Color idle, Color hover, Color pressed)
        {
            var serialized = new SerializedObject(control);
            serialized.FindProperty("idleColor").colorValue = idle;
            serialized.FindProperty("hoverColor").colorValue = hover;
            serialized.FindProperty("pressedColor").colorValue = pressed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(GameObject root, string path)
        {
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new InvalidOperationException("Could not save prefab " + path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var separator = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }
    }
}
