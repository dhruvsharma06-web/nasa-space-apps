using System;
using System.Collections.Generic;
using Astra.App;
using Astra.Core.Assessment;
using Astra.UI.Hub;
using Astra.XR.Input;
using Astra.XR.Interactables;
using Astra.XR.Rig;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Astra.UI.Editor
{
    /// <summary>
    /// Generates P1's rig and pause prefabs, shared materials and the Hub, L0 and L1 scenes.
    /// Layout follows CONTRACT.md §8 comfort rules: seated rig at the origin facing +Z, every target within
    /// 0.7 m horizontally and 0.7–1.5 m high, nothing behind the cadet, interactables at least 6 cm.
    /// Scenes are generated artifacts: rebuilding overwrites them. Prefabs/materials are created only when missing.
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
        private static readonly Vector3 Eye = new Vector3(0f, EyeHeight, 0f);

        private static readonly Color Panel = new Color(0.035f, 0.065f, 0.11f, 0.98f);
        private static readonly Color Ink = new Color(0.96f, 0.97f, 1f, 1f);
        private static readonly Color ButtonBlue = new Color(0.15f, 0.3f, 0.46f, 1f);

        [MenuItem("ASTRA/P1/Rebuild Hub, L0 and L1 scenes (overwrites scenes)")]
        public static void BuildAll()
        {
            UiPrefabBuilder.BuildUiPrefabs();
            EnsureMaterials();
            EnsurePausePrefab();
            EnsureRigPrefab();
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
            var unlit = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
            if (lit == null || unlit == null)
                throw new InvalidOperationException("Required lit/unlit shaders are unavailable in this render pipeline.");
            CreateMaterial("Floor", lit, new Color(0.11f, 0.13f, 0.17f));
            CreateMaterial("Wall", lit, new Color(0.2f, 0.24f, 0.3f));
            CreateMaterial("Trim", lit, new Color(0.32f, 0.45f, 0.6f));
            CreateMaterial("Desk", lit, new Color(0.42f, 0.46f, 0.52f));
            CreateMaterial("Control", lit, Color.white);
            CreateMaterial("Target", lit, Color.white);
            CreateMaterial("Tool", lit, new Color(0.95f, 0.62f, 0.15f));
            CreateMaterial("Bay", lit, new Color(0.18f, 0.32f, 0.42f));
            CreateMaterial("Ray", unlit, new Color(0.35f, 0.9f, 1f));
            CreateMaterial("Light", unlit, new Color(0.85f, 0.93f, 1f));
        }

        private static void CreateMaterial(string name, Shader shader, Color color)
        {
            var path = Materials + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                return;
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(material, path);
        }

        // ------------------------------------------------------------------ prefabs

        private static void EnsureRigPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab) != null)
                return;
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
                camera.farClipPlane = 50f;
                camera.fieldOfView = 75f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 1f);
                head.gameObject.AddComponent<AudioListener>();
                var headPose = head.gameObject.AddComponent<HeadPoseDriver>();

                var desktop = Child(root.transform, "DesktopPointer", Vector3.zero);
                var desktopTag = desktop.gameObject.AddComponent<InputModalityTag>();
                desktopTag.Configure(InputModality.ControllerRay, isSimulator: true);
                var desktopRay = desktop.gameObject.AddComponent<RayInteractor>();
                var desktopInput = desktop.gameObject.AddComponent<DesktopMouseInput>();
                desktopInput.Configure(desktopRay, desktopTag, camera, headPose);

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

                root.AddComponent<RigRecenterService>().Configure(headPose, desktopInput);
                Save(root, RigPrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsurePausePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefab) != null)
                return;
            var root = new GameObject("PauseMenu");
            try
            {
                var pause = root.AddComponent<PauseMenu>();

                var pauseCanvas = WorldCanvas(root.transform, "PauseButton", new Vector3(0.34f, 1.02f, 0.44f), 260f, 110f, 0.0006f);
                var pauseButton = MakeButton(pauseCanvas, "Pause", "PAUSE", 0f, 0f, 260f, 110f, 34);
                UnityEventTools.AddPersistentListener(pauseButton.onClick, pause.Pause);

                var menu = WorldCanvas(root.transform, "MenuRoot", new Vector3(0f, 1.04f, 0.38f), 600f, 700f, 0.0006f);
                menu.gameObject.AddComponent<Image>().color = Panel;
                MakeLabel(menu, "Title", "PAUSED", 40, 0f, 290f, 560f, 70f, TextAnchor.MiddleCenter);
                var resume = MakeButton(menu, "Resume", "RESUME", 0f, 195f, 460f, 90f, 32);
                var recenter = MakeButton(menu, "Recenter", "RECENTER", 0f, 90f, 460f, 90f, 32);
                var restart = MakeButton(menu, "Restart", "RESTART", 0f, -15f, 460f, 90f, 32);
                var exit = MakeButton(menu, "Exit", "EXIT TO HUB", 0f, -120f, 460f, 90f, 32);
                var finish = MakeButton(menu, "Finish", "FINISH", 0f, -255f, 460f, 90f, 32);
                finish.GetComponent<Image>().color = new Color(0.45f, 0.25f, 0.1f, 1f);
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
            public RigRecenterService Recenter;
        }

        private static LevelRig NewScene(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.52f);

            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetAsLastSibling();

            var rigInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab), scene);
            var rig = new LevelRig
            {
                Rig = rigInstance,
                Camera = rigInstance.GetComponentInChildren<Camera>(true),
                Desktop = rigInstance.GetComponentInChildren<DesktopMouseInput>(true),
                Hand = rigInstance.GetComponentInChildren<HandAimInput>(true),
                Recenter = rigInstance.GetComponent<RigRecenterService>()
            };
            rig.DesktopTag = rig.Desktop.GetComponent<InputModalityTag>();
            BuildRoom();
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            return rig;
        }

        private static void BuildRoom()
        {
            var room = new GameObject("Environment (hidden in AR)");
            room.AddComponent<PassthroughHidden>();
            Box(room.transform, "Floor", new Vector3(0f, -0.01f, 0.6f), new Vector3(6f, 0.02f, 5f), "Floor");
            Box(room.transform, "Front Wall", new Vector3(0f, 1.5f, 2.6f), new Vector3(6f, 3f, 0.05f), "Wall");
            Box(room.transform, "Left Wall", new Vector3(-2.4f, 1.5f, 0.6f), new Vector3(0.05f, 3f, 5f), "Wall");
            Box(room.transform, "Right Wall", new Vector3(2.4f, 1.5f, 0.6f), new Vector3(0.05f, 3f, 5f), "Wall");
            Box(room.transform, "Back Wall", new Vector3(0f, 1.5f, -1.9f), new Vector3(6f, 3f, 0.05f), "Wall");
            Box(room.transform, "Ceiling", new Vector3(0f, 3f, 0.6f), new Vector3(6f, 0.05f, 5f), "Wall");
            Box(room.transform, "Trim Strip", new Vector3(0f, 0.9f, 2.57f), new Vector3(6f, 0.06f, 0.02f), "Trim");
            for (var i = -1; i <= 1; i++)
                Box(room.transform, "Ceiling Light", new Vector3(i * 1.4f, 2.97f, 0.9f), new Vector3(0.9f, 0.02f, 0.2f), "Light");
            foreach (var collider in room.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);
        }

        private static Transform BuildDesk()
        {
            var desk = new GameObject("Training Desk").transform;
            Box(desk, "Desk Top", new Vector3(0f, 0.755f, 0.5f), new Vector3(1.1f, 0.05f, 0.5f), "Desk");
            Box(desk, "Desk Base", new Vector3(0f, 0.37f, 0.62f), new Vector3(1.0f, 0.72f, 0.06f), "Trim");
            return desk;
        }

        private static void BuildHub()
        {
            var rig = NewScene(HubScene);
            var board = WorldCanvas(null, "Hub Board", new Vector3(0f, 1.2f, 0.85f), 1160f, 860f, 0.0008f);
            board.gameObject.AddComponent<Image>().color = Panel;
            MakeLabel(board, "Title", "ASTRA ACADEMY", 52, 0f, 370f, 1080f, 80f, TextAnchor.MiddleCenter);
            MakeLabel(board, "Subtitle", "Fictional ASTRA Station training. Select an available level.", 26, 0f, 315f, 1080f, 40f,
                TextAnchor.MiddleCenter);
            var notice = MakeLabel(board, "Progress Notice", string.Empty, 22, 0f, 275f, 1080f, 32f, TextAnchor.MiddleCenter);

            var cards = new LevelCard[LevelCatalog.Levels.Count];
            for (var i = 0; i < cards.Length; i++)
            {
                var entry = LevelCatalog.Levels[i];
                var column = i % 4;
                var row = i / 4;
                var x = -405f + column * 270f;
                var y = 125f - row * 290f;
                var button = MakeButton(board, "L" + entry.Level + " Card", string.Empty, x, y, 250f, 270f, 24);
                Object.DestroyImmediate(button.transform.Find("Label").gameObject);
                MakeLabel(button.transform, "Level", "LEVEL " + entry.Level, 22, 0f, 95f, 230f, 36f, TextAnchor.MiddleCenter);
                var title = MakeLabel(button.transform, "Title", entry.Title, 26, 0f, 20f, 230f, 100f, TextAnchor.MiddleCenter);
                var status = MakeLabel(button.transform, "Status", string.Empty, 22, 0f, -90f, 230f, 60f, TextAnchor.MiddleCenter);
                status.color = new Color(0.75f, 0.9f, 1f);
                var card = button.gameObject.AddComponent<LevelCard>();
                card.Configure(entry.Level, entry.Title, button, title, status);
                cards[i] = card;
            }
            var quit = MakeButton(board, "Quit", "QUIT", 0f, -385f, 260f, 70f, 28);
            var quitter = board.gameObject.AddComponent<ApplicationQuit>();
            UnityEventTools.AddPersistentListener(quit.onClick, quitter.Quit);

            var hub = new GameObject("Hub");
            var controller = hub.AddComponent<HubController>();
            controller.Configure(cards, notice);
            hub.AddComponent<HubProgressBinding>();
            FinishScene(rig, HubScene);
        }

        private static PauseMenu AddLevelUi(LevelRig rig, string missionFile, out LevelController controller)
        {
            var ui = new GameObject("Level UI").transform;
            var banner = PlaceUi(ui, "PhaseBanner", new Vector3(0f, 1.52f, 1.0f), 0.0008f).GetComponent<PhaseBanner>();
            var instruction = PlaceUi(ui, "InstructionPanel", new Vector3(0f, 1.32f, 1.0f), 0.0008f).GetComponent<InstructionPanel>();
            PlaceUi(ui, "AlarmIndicator", new Vector3(0.55f, 1.42f, 0.85f), 0.0005f);
            var debrief = PlaceUi(ui, "DebriefPanel", new Vector3(0f, 1.16f, 0.6f), 0.0006f);
            debrief.SetActive(false);

            var pauseObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefab));
            var pause = pauseObject.GetComponent<PauseMenu>();
            foreach (Transform child in pauseObject.transform)
                Face(child);
            SetField(pause, "defaultInteractor", rig.DesktopTag);
            SetField(pause, "recenterHandler", rig.Recenter);
            UnityEventTools.AddPersistentListener(rig.Desktop.onMenuPressed, pause.Toggle);
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

            var confirm = Box(desk, "CONFIRM POSITION", new Vector3(-0.14f, 0.8f, 0.43f), new Vector3(0.16f, 0.04f, 0.1f), "Control");
            var button = confirm.gameObject.AddComponent<ButtonTarget>();
            button.Configure(SystemTargets.Calibration, EventNames.CalibrationCompleted);
            SetField(button, "feedbackRenderer", confirm.GetComponent<Renderer>());
            ObjectLabel(confirm, "CONFIRM POSITION", new Vector3(-0.14f, 0.88f, 0.45f));
            ObjectLabel(desk, "Sit upright, then confirm your position. Recenter is in the pause menu.",
                new Vector3(0f, 1.03f, 0.62f), 360f, 22);
            FinishScene(rig, L0Scene);
        }

        private static void BuildL1()
        {
            var rig = NewScene(L1Scene);
            var desk = BuildDesk();
            AddLevelUi(rig, "l1-orientation-v1.json", out _);

            var panel = Box(desk, "TARGET", new Vector3(-0.38f, 1.08f, 0.55f), new Vector3(0.22f, 0.16f, 0.02f), "Target");
            Face(panel);
            var select = panel.gameObject.AddComponent<ButtonTarget>();
            select.Configure("orientation_target_panel", EventNames.TargetSelected);
            SetField(select, "feedbackRenderer", panel.GetComponent<Renderer>());
            panel.gameObject.AddComponent<LocatableTarget>().Configure("orientation_target_panel");
            ObjectLabel(desk, "TARGET", panel.position + new Vector3(0f, 0.12f, 0f));

            var start = Box(desk, "START", new Vector3(0f, 0.8f, 0.42f), new Vector3(0.09f, 0.04f, 0.09f), "Control");
            var startButton = start.gameObject.AddComponent<ButtonTarget>();
            startButton.Configure("orientation_start_button");
            SetField(startButton, "feedbackRenderer", start.GetComponent<Renderer>());
            ObjectLabel(desk, "START", new Vector3(0f, 0.87f, 0.44f));

            var tool = new GameObject("TOOL").transform;
            tool.position = new Vector3(-0.2f, 0.801f, 0.45f);
            var visual = Box(tool, "Body", tool.position, new Vector3(0.16f, 0.04f, 0.06f), "Tool");
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            var grip = Box(tool, "Grip", tool.position + new Vector3(0.055f, 0.025f, 0f), new Vector3(0.04f, 0.012f, 0.05f), "Trim");
            Object.DestroyImmediate(grip.GetComponent<Collider>());
            tool.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.16f, 0.05f, 0.07f);
            var body = tool.gameObject.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            tool.gameObject.AddComponent<GrabbableTool>().Configure("orientation_practice_tool");
            ObjectLabel(tool, "TOOL", tool.position + new Vector3(0f, 0.06f, 0.02f));

            var bay = Box(desk, "TOOL BAY", new Vector3(0.24f, 0.79f, 0.45f), new Vector3(0.22f, 0.02f, 0.12f), "Bay");
            var socket = bay.gameObject.AddComponent<PlacementSocket>();
            socket.Configure("orientation_tool_socket", "orientation_practice_tool");
            var capture = new GameObject("Capture Volume").AddComponent<BoxCollider>();
            capture.transform.SetParent(bay, false);
            capture.isTrigger = true;
            capture.center = new Vector3(0f, 3f, 0f);
            capture.size = new Vector3(1.1f, 6f, 1.2f);
            var snap = new GameObject("Snap Point").transform;
            snap.SetParent(bay, false);
            snap.position = new Vector3(0.24f, 0.821f, 0.45f);
            SetField(socket, "snapPoint", snap);
            ObjectLabel(desk, "TOOL BAY", new Vector3(0.24f, 0.87f, 0.48f));
            FinishScene(rig, L1Scene);
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
            box.GetComponent<Renderer>().sharedMaterial = Mat(material);
            return box.transform;
        }

        private static void ObjectLabel(Transform parent, string text, Vector3 position, float width = 300f, int size = 30)
        {
            var canvas = WorldCanvas(null, text + " Label", position, width, 90f, 0.0005f);
            canvas.SetParent(parent, true);
            Face(canvas);
            var label = MakeLabel(canvas, "Text", text, size, 0f, 0f, width, 90f, TextAnchor.MiddleCenter);
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
            text.color = Ink;
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
            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonBlue;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.7f, 1f, 0.75f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
            button.colors = colors;
            MakeLabel(rect, "Label", caption, size, 0f, 0f, width - 20f, height, TextAnchor.MiddleCenter);
            return button;
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
