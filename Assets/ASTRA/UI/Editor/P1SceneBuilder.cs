using System;
using System.IO;
using System.Linq;
using Astra.UI.Gaze;
using Astra.UI.Hub;
using Astra.XR.Phone;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Astra.UI.Editor
{
    /// <summary>
    /// P1 generator: shared UI prefabs, PauseMenu prefab, the phone GazeRig prefab + reticle material, the AudioCues asset
    /// and the Hub scene. L0/L1 are built by the Integration Lead's level factory from their layout files
    /// (Content/Layouts/l0-layout.json, l1-layout.json); this builder does not touch level scenes.
    /// Layout follows CONTRACT.md §8 comfort rules: seated eye at (0, 1.2, 0) facing +Z, nothing behind the cadet.
    /// Prefabs are created only when missing by BuildAll; RebuildAll overwrites them in place (GUIDs kept).
    /// </summary>
    public static class P1SceneBuilder
    {
        public const string HubScene = "Assets/ASTRA/Scenes/Hub.unity";
        public static readonly string[] Scenes = { HubScene };

        private const string PausePrefab = "Assets/ASTRA/UI/Prefabs/PauseMenu.prefab";
        private const string HubDir = "Assets/ASTRA/UI/Hub";
        private const string PreviewDir = HubDir + "/Previews";
        private const string AudioDir = "Assets/ASTRA/Audio";
        private const string TmpFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string EarthTexture = "Assets/ASTRA/Levels/_Shared/Earth_Procedural.png";
        private const float EyeHeight = 1.2f;
        private static readonly Vector3 Eye = new Vector3(0f, EyeHeight, 0f);
        private static readonly Color Plate = new Color(0.02f, 0.035f, 0.06f, 0.9f);

        /// <summary>Creates missing P1 prefabs/assets and rebuilds the Hub scene. Existing prefabs are never overwritten.</summary>
        [MenuItem("ASTRA/P1/Build P1 assets and Hub scene (keeps existing prefabs)")]
        public static void BuildAll() => Build(false);

        /// <summary>Explicit regeneration: overwrites the shared UI prefabs, PauseMenu and GazeRig (GUIDs kept).</summary>
        [MenuItem("ASTRA/P1/Rebuild P1 assets and Hub scene (overwrites prefabs)")]
        public static void RebuildAll() => Build(true);

        private static void Build(bool overwritePrefabs)
        {
            if (overwritePrefabs) UiPrefabBuilder.RebuildUiPrefabs();
            else UiPrefabBuilder.BuildUiPrefabs();
            var reticle = EnsureReticleMaterial();
            if (overwritePrefabs || AssetDatabase.LoadMainAssetAtPath(GazeRig.PrefabPath) == null)
                BuildGazeRigPrefab(reticle);
            if (overwritePrefabs || AssetDatabase.LoadMainAssetAtPath(PausePrefab) == null)
                BuildPausePrefab();
            CreateAudioCues();
            BuildHub(reticle);
            AssetDatabase.SaveAssets();
            Debug.Log("[P1SceneBuilder] Built P1 assets (" + (overwritePrefabs ? "prefabs overwritten" : "existing prefabs kept") + ") and " + HubScene);
        }

        /// <summary>Batch entry: -executeMethod Astra.UI.Editor.P1SceneBuilder.BuildAllBatch (exits 0/1).</summary>
        public static void BuildAllBatch()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (Exception ex) { Debug.LogError(ex); EditorApplication.Exit(1); }
        }

        // ------------------------------------------------------------------ gaze rig

        public static Material EnsureReticleMaterial()
        {
            var shader = Shader.Find("ASTRA/ReticleOverlay");
            if (shader == null)
                throw new InvalidOperationException("Shader ASTRA/ReticleOverlay is missing (XR/Phone/Shaders).");
            var material = AssetDatabase.LoadAssetAtPath<Material>(GazeRig.ReticleMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "ReticleOverlay" };
                AssetDatabase.CreateAsset(material, GazeRig.ReticleMaterialPath);
            }
            material.shader = shader;
            material.renderQueue = 4010;
            EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("ASTRA/P1/Rebuild GazeRig prefab (overwrites)")]
        public static void BuildGazeRigPrefabMenu() => BuildGazeRigPrefab(EnsureReticleMaterial());

        private static void BuildGazeRigPrefab(Material reticle)
        {
            var root = new GameObject("GazeRig");
            try
            {
                var head = new GameObject("Head");
                head.transform.SetParent(root.transform, false);
                head.transform.localPosition = Eye;
                head.tag = "MainCamera";
                var camera = head.AddComponent<Camera>();
                camera.fieldOfView = 70f;
                camera.nearClipPlane = 0.02f;
                camera.farClipPlane = 200f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0.01f);
                head.AddComponent<AudioListener>();
                GazeRig.AttachTo(camera, reticle, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFont));
                if (PrefabUtility.SaveAsPrefabAsset(root, GazeRig.PrefabPath) == null)
                    throw new InvalidOperationException("Could not save " + GazeRig.PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ pause menu

        private static void BuildPausePrefab()
        {
            var root = new GameObject("PauseMenu");
            try
            {
                var pause = root.AddComponent<PauseMenu>();

                var pauseCanvas = WorldCanvas(root.transform, "PauseButton", new Vector3(0.34f, 1.02f, 0.44f), 280f, 120f, 0.0006f);
                var pauseButton = MakeButton(pauseCanvas, "Pause", "PAUSE", 0f, 0f, 280f, 120f, 44, "pause_button");
                UnityEventTools.AddPersistentListener(pauseButton.onClick, pause.Pause);

                var menu = WorldCanvas(root.transform, "MenuRoot", new Vector3(0f, 1.15f, 0.5f), 640f, 760f, 0.0006f);
                P1Style.Surface(menu.gameObject.AddComponent<Image>(), P1Style.Glass);
                P1Style.Edge(menu, P1Style.Hairline, "Frame");
                UiPrefabBuilder.AccentHeader(menu);
                var title = MakeLabel(menu, "Title", "PAUSED", 56, 0f, 304f, 580f, 80f, TextAnchor.MiddleCenter);
                title.fontStyle = FontStyle.Bold;
                title.color = Theme.AccentInk;
                // 0.6 mm/px at 0.5 m: 44 px bold ≈ 18 mm cap, large gaze targets (≥ 6 cm tall).
                var resume = MakeButton(menu, "Resume", "RESUME", 0f, 180f, 520f, 110f, 44, "pause_resume");
                var recenter = MakeButton(menu, "Recenter", "RECENTER", 0f, 55f, 520f, 110f, 44, "pause_recenter");
                var restart = MakeButton(menu, "Restart", "RESTART", 0f, -70f, 520f, 110f, 44, "pause_restart");
                var exit = MakeButton(menu, "Exit", "HUB", 0f, -195f, 520f, 110f, 44, "pause_hub");
                var finish = MakeButton(menu, "Finish", "FINISH", 0f, -320f, 520f, 90f, 40, "pause_finish");
                var colors = finish.colors;
                colors.normalColor = new Color(0.5f, 0.3f, 0.08f, 1f);
                colors.highlightedColor = Theme.Warning;
                colors.selectedColor = colors.normalColor;
                finish.colors = colors;
                UnityEventTools.AddPersistentListener(resume.onClick, pause.Resume);
                UnityEventTools.AddPersistentListener(recenter.onClick, pause.Recenter);
                UnityEventTools.AddPersistentListener(restart.onClick, pause.RequestRestart);
                UnityEventTools.AddPersistentListener(exit.onClick, pause.RequestExit);
                UnityEventTools.AddPersistentListener(finish.onClick, pause.RequestFinish);
                pause.Configure(menu.gameObject, finish.gameObject, null);
                UiPrefabBuilder.ApplyTypography(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, PausePrefab) == null)
                    throw new InvalidOperationException("Could not save " + PausePrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ audio

        [MenuItem("ASTRA/P1/Create AudioCues asset")]
        public static void CreateAudioCues()
        {
            if (!AssetDatabase.IsValidFolder(AudioDir))
                return;
            var path = AudioDir + "/AudioCues.asset";
            var cues = AssetDatabase.LoadAssetAtPath<AudioCues>(path);
            if (cues == null)
            {
                cues = ScriptableObject.CreateInstance<AudioCues>();
                AssetDatabase.CreateAsset(cues, path);
            }
            AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{name}.wav");
            cues.hover = Clip("hover");
            cues.select = Clip("select");
            cues.toggle = Clip("toggle");
            cues.button = Clip("button");
            cues.dialTick = Clip("dial_tick");
            cues.grab = Clip("grab");
            cues.place = Clip("place");
            cues.correct = Clip("correct");
            cues.wrong = Clip("wrong");
            cues.phaseChange = Clip("phase_change");
            cues.alarm = Clip("alarm");
            cues.success = Clip("success");
            cues.fail = Clip("fail");
            cues.ambientHum = Clip("ambient_hum");
            EditorUtility.SetDirty(cues);
            var hum = AssetImporter.GetAtPath(AudioDir + "/ambient_hum.wav") as AudioImporter;
            if (hum != null && !hum.loadInBackground)
            {
                hum.loadInBackground = true;
                hum.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ hub

        private static void BuildHub(Material reticle)
        {
            EnsureFolder("Assets/ASTRA/Scenes");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.35f, 0.4f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.22f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.07f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.6f, -0.25f, 1f).normalized);
            BuildSpaceBackdrop();

            // Camera + phone gaze rig (mouse click + right-drag look in the editor and browser).
            var head = new GameObject("Seat_Eye (Gaze Rig)");
            head.transform.position = Eye;
            head.tag = "MainCamera";
            var camera = head.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0.01f);
            head.AddComponent<AudioListener>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFont);
            GazeRig.AttachTo(camera, reticle, font);

            var hubObject = new GameObject("Hub");
            var controller = hubObject.AddComponent<HubController>();

            // Header: wordmark, continue line, and three instrument-style stats (label over value).
            var header = WorldCanvas(null, "Hub Header", new Vector3(0f, 1.63f, 1.05f), 1300f, 160f, 0.0008f);
            P1Style.Surface(header.gameObject.AddComponent<Image>(), P1Style.Glass).raycastTarget = false;
            P1Style.Edge(header, P1Style.Hairline, "Frame");
            P1Style.Type(MakeLabel(header, "Kicker", "ASTRA STATION  \u00b7  CADET TRAINING", 28, -290f, 48f, 640f, 34f,
                TextAnchor.MiddleLeft), P1Style.Role.Mono, P1Style.InkMuted);
            P1Style.Type(MakeLabel(header, "Title", "Astra Academy", 66, -290f, 4f, 640f, 76f, TextAnchor.MiddleLeft), P1Style.Role.Title, P1Style.Ink);
            var cont = P1Style.Type(MakeLabel(header, "Continue", string.Empty, 32, -290f, -50f, 640f, 40f, TextAnchor.MiddleLeft),
                P1Style.Role.Medium, Theme.AccentInk);
            var callsign = Stat(header, "Callsign", "CALLSIGN", "CADET", 190f);
            var score = Stat(header, "Score", "SCORE", "0", 360f);
            var medals = Stat(header, "Medals", "MEDALS", "0G 0S 0B", 555f);
            var notice = P1Style.Type(MakeLabel(header, "Progress Notice", string.Empty, 22, 400f, -58f, 470f, 28f, TextAnchor.MiddleRight),
                P1Style.Role.Body, P1Style.InkMuted);
            FitText(notice, 14, 22);
            Face(header);

            // Cards: two rows of four on a gentle arc, 1.0 m from the eye, each card facing the cadet.
            var previews = ImportPreviews();
            var cards = new LevelCard[LevelCatalog.Levels.Count];
            var presenters = new LevelCardPresenter[cards.Length];
            var yaws = new[] { -28.5f, -9.5f, 9.5f, 28.5f };
            var rows = new[] { 1.31f, 0.93f };
            for (var i = 0; i < cards.Length; i++)
            {
                var entry = LevelCatalog.Levels[i];
                var yaw = yaws[i % 4] * Mathf.Deg2Rad;
                var position = new Vector3(Mathf.Sin(yaw) * 1.0f, rows[i / 4], Mathf.Cos(yaw) * 1.0f);
                BuildCard(entry, position, previews[i] != null, out cards[i], out presenters[i]);
            }
            controller.Configure(cards, notice);
            controller.ConfigurePresentation(presenters, previews);
            hubObject.AddComponent<HubProgressBinding>();

            // Left: how to play, as key / description rows.
            var howTo = SideCard("How To Play", -48f, 1.12f, 600f);
            P1Style.Type(MakeLabel(howTo, "Kicker", "HOW TO PLAY", 26, 0f, 256f, 390f, 34f, TextAnchor.MiddleLeft), P1Style.Role.Mono, Theme.AccentInk);
            var steps = new[]
            {
                ("LOOK", "Aim with the centre ring."),
                ("TAP", "Use what the ring is on. Pinch works too."),
                ("HOLD", "Hold for 1 s to recenter."),
                ("TOOLS", "Tap to pick up, tap the bay to place.")
            };
            for (var i = 0; i < steps.Length; i++)
            {
                var y = 168f - i * 122f;
                P1Style.Type(MakeLabel(howTo, "Key " + steps[i].Item1, steps[i].Item1, 26, -136f, y + 24f, 120f, 34f, TextAnchor.UpperLeft),
                    P1Style.Role.Mono, Theme.AccentInk);
                P1Style.Type(MakeLabel(howTo, "Step " + steps[i].Item1, steps[i].Item2, 32, 54f, y, 270f, 100f, TextAnchor.UpperLeft),
                    P1Style.Role.Body, P1Style.Ink);
                if (i < steps.Length - 1)
                    MakeImage(howTo, "Rule", 0f, y - 62f, 390f, 2f, P1Style.Hairline).sprite = null;
            }
            Face(howTo);

            // Right: settings (dwell toggle), the judges' unlock-all, quit.
            var settings = SideCard("Settings", 48f, 1.12f, 600f);
            P1Style.Type(MakeLabel(settings, "Kicker", "SETTINGS", 26, 0f, 256f, 390f, 34f, TextAnchor.MiddleLeft), P1Style.Role.Mono, Theme.AccentInk);
            P1Style.Type(MakeLabel(settings, "Dwell Title", "Dwell select", 34, -70f, 186f, 250f, 48f, TextAnchor.MiddleLeft), P1Style.Role.Medium, P1Style.Ink);
            var dwell = MakeButton(settings, "Dwell Toggle", "Off", 130f, 186f, 120f, 64f, 30, "hub_dwell_toggle");
            P1Style.Type(MakeLabel(settings, "Dwell Help", "Look at a control for 1.5 s to use it. No tap needed.", 28, 0f, 112f, 390f, 72f,
                TextAnchor.UpperLeft), P1Style.Role.Body, P1Style.InkSecondary);
            MakeImage(settings, "Rule", 0f, 58f, 390f, 2f, P1Style.Hairline).sprite = null;
            var unlock = MakeButton(settings, "Demo Unlock All", "Unlock all levels", 0f, -14f, 390f, 90f, 32, "hub_demo_unlock_all");
            unlock.colors = P1Style.ButtonColors(unlock.colors, primary: true);
            P1Style.Type(MakeLabel(settings, "Unlock Help", "Demo mode for judges: opens every level.", 28, 0f, -90f, 390f, 40f,
                TextAnchor.UpperLeft), P1Style.Role.Body, P1Style.InkSecondary);
            var quit = MakeButton(settings, "Quit", "Quit", 0f, -222f, 390f, 80f, 32, "hub_quit");
            var quitter = settings.gameObject.AddComponent<ApplicationQuit>();
            UnityEventTools.AddPersistentListener(quit.onClick, quitter.Quit);
            Face(settings);

            var hubHeader = hubObject.AddComponent<HubHeader>();
            hubHeader.Configure(controller, callsign, score, medals, cont, dwell.GetComponentInChildren<Text>(), unlock.GetComponentInChildren<Text>());
            UnityEventTools.AddPersistentListener(dwell.onClick, hubHeader.ToggleDwell);
            UnityEventTools.AddPersistentListener(unlock.onClick, hubHeader.UnlockAll);

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.renderMode == RenderMode.WorldSpace)
                    canvas.worldCamera = camera;
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), HubScene))
                throw new InvalidOperationException("Could not save " + HubScene);
        }

        private static void FitText(Text text, int min, int max)
        {
            text.resizeTextForBestFit = true; text.resizeTextMinSize = min; text.resizeTextMaxSize = max;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>Instrument-style stat: small mono label above a value (HubHeader writes the value).</summary>
        private static Text Stat(Transform parent, string name, string label, string value, float x)
        {
            P1Style.Type(MakeLabel(parent, name + " Label", label, 24, x, 32f, 160f, 30f, TextAnchor.MiddleRight), P1Style.Role.Mono, P1Style.InkMuted);
            var text = P1Style.Type(MakeLabel(parent, name, value, 40, x, -6f, 140f, 48f, TextAnchor.MiddleRight), P1Style.Role.Title, P1Style.Ink);
            FitText(text, 22, 40);
            return text;
        }

        /// <summary>
        /// One level card (0.29 × 0.36 m, 1.0 m away): full-bleed preview with level/duration chips, title, status dot,
        /// best score and medal chip; hover reveals the goal; a locked card veils its preview. Title 40 px ≈ 22 mm cap.
        /// </summary>
        private static void BuildCard(LevelCatalog.Entry entry, Vector3 position, bool hasPreview, out LevelCard card, out LevelCardPresenter presenter)
        {
            const float W = 360f, H = 450f, PW = 340f, PH = 191f, PY = 117f;   // preview 16:9, 10 px inset
            var canvas = WorldCanvas(null, $"L{entry.Level} Card", position, W, H, 0.0008f);
            Face(canvas);
            var glow = MakeImage(canvas, "Edge Glow", 0f, 0f, W + 14f, H + 14f, new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0f));
            glow.sprite = P1Art.Hairline != null ? P1Art.Hairline : P1Art.Outline;
            var button = MakeButton(canvas, "Card Button", string.Empty, 0f, 0f, W, H, 30, $"hub_card_l{entry.Level}");
            Object.DestroyImmediate(button.transform.Find("Label").gameObject);
            var colors = button.colors;
            colors.normalColor = P1Style.GlassRaised;
            colors.highlightedColor = new Color(0.11f, 0.15f, 0.21f, 0.96f);
            colors.selectedColor = P1Style.GlassRaised;
            colors.disabledColor = new Color(0.05f, 0.06f, 0.085f, 0.9f);
            button.colors = colors;
            button.transform.Find("Edge").GetComponent<Image>().color = P1Style.Hairline;
            var body = button.transform;

            var preview = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
            preview.rectTransform.SetParent(body, false);
            preview.rectTransform.anchoredPosition = new Vector2(0f, PY);
            preview.rectTransform.sizeDelta = new Vector2(PW, PH);
            preview.raycastTarget = false;
            preview.enabled = hasPreview;
            var veil = MakeImage(body, "Preview Fade", 0f, PY - PH / 2f + 40f, PW, 80f, P1Style.GlassRaised);
            veil.sprite = P1Art.Fade;
            veil.type = Image.Type.Simple;
            Chip(body, "Level Chip", "L" + entry.Level, -PW / 2f + 46f, PY + PH / 2f - 28f, 72f);
            Chip(body, "Duration Chip", entry.Duration.Replace("~", "").ToUpperInvariant(), PW / 2f - 66f, PY + PH / 2f - 28f, 112f);

            var cardTitle = P1Style.Type(MakeLabel(body, "Title", entry.Title, 40, 0f, -26f, W - 40f, 84f, TextAnchor.MiddleLeft),
                P1Style.Role.Title, P1Style.Ink);
            FitText(cardTitle, 28, 40);
            var dot = MakeImage(body, "Status Dot", -W / 2f + 28f, -104f, 14f, 14f, Theme.Accent);
            dot.sprite = P1Art.Dot;
            dot.type = Image.Type.Simple;
            var status = P1Style.Type(MakeLabel(body, "Status", string.Empty, 30, 14f, -104f, W - 76f, 40f, TextAnchor.MiddleLeft),
                P1Style.Role.Medium, P1Style.InkSecondary);
            FitText(status, 20, 30);
            MakeImage(body, "Rule", 0f, -140f, W - 40f, 2f, P1Style.Hairline).sprite = null;
            var best = P1Style.Type(MakeLabel(body, "Best", "NOT PLAYED", 28, -46f, -180f, 248f, 40f, TextAnchor.MiddleLeft),
                P1Style.Role.Mono, P1Style.InkSecondary);
            FitText(best, 20, 28);
            var badge = MakeImage(body, "Medal Badge", W / 2f - 76f, -180f, 116f, 44f, Theme.Gold);
            var medal = P1Style.Type(MakeLabel(badge.transform, "Medal", string.Empty, 26, 0f, 0f, 116f, 44f, TextAnchor.MiddleCenter),
                P1Style.Role.Mono, new Color(0.06f, 0.07f, 0.09f));
            badge.enabled = false;
            medal.enabled = false;

            // Details (expand on hover) over the preview: the goal and skill / difficulty / time.
            var detailsRect = new GameObject("Details", typeof(RectTransform)).GetComponent<RectTransform>();
            detailsRect.SetParent(body, false);
            detailsRect.anchoredPosition = new Vector2(0f, PY);
            detailsRect.sizeDelta = new Vector2(PW, PH);
            var detailsGroup = detailsRect.gameObject.AddComponent<CanvasGroup>();
            detailsGroup.alpha = 0f;
            detailsGroup.blocksRaycasts = false;
            detailsGroup.interactable = false;
            P1Style.Surface(detailsRect.gameObject.AddComponent<Image>(), new Color(0.03f, 0.05f, 0.08f, 0.93f)).raycastTarget = false;
            var goal = P1Style.Type(MakeLabel(detailsRect, "Goal", entry.Goal, 30, 0f, 22f, PW - 36f, 110f, TextAnchor.MiddleLeft),
                P1Style.Role.Body, P1Style.Ink);
            var meta = P1Style.Type(MakeLabel(detailsRect, "Meta", string.Empty, 26, 0f, -62f, PW - 36f, 36f, TextAnchor.MiddleLeft),
                P1Style.Role.Mono, Theme.AccentInk);

            // Locked: veil the preview (no cartoon padlock) with LOCKED and the unlock rule.
            var lockRoot = new GameObject("Lock", typeof(RectTransform)).GetComponent<RectTransform>();
            lockRoot.SetParent(body, false);
            lockRoot.anchoredPosition = new Vector2(0f, PY);
            lockRoot.sizeDelta = new Vector2(PW, PH);
            P1Style.Surface(lockRoot.gameObject.AddComponent<Image>(), new Color(0.02f, 0.03f, 0.05f, 0.72f)).raycastTarget = false;
            P1Style.Type(MakeLabel(lockRoot, "Locked", "LOCKED", 28, 0f, 24f, PW - 40f, 36f, TextAnchor.MiddleCenter), P1Style.Role.Mono, P1Style.InkSecondary);
            var lockText = P1Style.Type(MakeLabel(lockRoot, "Lock Text", string.Empty, 30, 0f, -18f, PW - 40f, 40f, TextAnchor.MiddleCenter),
                P1Style.Role.Medium, P1Style.Ink);

            var dim = body.gameObject.AddComponent<CanvasGroup>();
            button.GetComponent<GazeButton>().Configure($"hub_card_l{entry.Level}", $"L{entry.Level} {entry.Title.ToUpperInvariant()}");
            card = button.gameObject.AddComponent<LevelCard>();
            card.Configure(entry.Level, entry.Title, button, cardTitle, status);
            presenter = canvas.gameObject.AddComponent<LevelCardPresenter>();
            presenter.Configure(canvas, glow, detailsGroup, preview, goal, meta, best, medal, badge, lockRoot.gameObject, lockText, dim,
                button.GetComponent<GazeButton>());
            presenter.ConfigureStatusDot(dot);
            lockRoot.SetAsLastSibling();
        }

        /// <summary>Small dark chip with mono text (over images).</summary>
        private static void Chip(Transform parent, string name, string value, float x, float y, float width)
        {
            var chip = MakeImage(parent, name, x, y, width, 40f, new Color(0.02f, 0.03f, 0.05f, 0.78f));
            P1Style.Type(MakeLabel(chip.transform, "Text", value, 26, 0f, 0f, width, 40f, TextAnchor.MiddleCenter), P1Style.Role.Mono, P1Style.Ink);
        }

        private static RectTransform SideCard(string name, float yawDegrees, float y, float height)
        {
            var yaw = yawDegrees * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Sin(yaw) * 0.95f, y, Mathf.Cos(yaw) * 0.95f);
            var canvas = WorldCanvas(null, name, position, 440f, height, 0.0008f);
            P1Style.Surface(canvas.gameObject.AddComponent<Image>(), P1Style.Glass).raycastTarget = false;
            P1Style.Edge(canvas, P1Style.Hairline, "Frame");
            UiPrefabBuilder.AccentHeader(canvas);
            return canvas;
        }

        /// <summary>Earth seen from the station window, behind the cards; star field beyond. No colliders.</summary>
        private static void BuildSpaceBackdrop()
        {
            var root = new GameObject("Space View (window)").transform;
            var urp = GraphicsSettings.defaultRenderPipeline != null;
            var lit = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            var unlit = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Texture");
            var earthMat = HubMaterial("HubEarth", lit, Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(EarthTexture));
            var starMat = HubMaterial("HubStars", unlit, Color.white, P1Art.Starfield);
            var frameMat = HubMaterial("HubWindowFrame", lit, new Color(0.12f, 0.13f, 0.15f), null);

            var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earth.name = "Earth";
            Object.DestroyImmediate(earth.GetComponent<Collider>());
            earth.transform.SetParent(root, false);
            earth.transform.position = new Vector3(-8f, -22f, 70f);
            earth.transform.localScale = Vector3.one * 110f;
            earth.transform.rotation = Quaternion.Euler(20f, 160f, 0f);
            earth.GetComponent<Renderer>().sharedMaterial = earthMat;
            earth.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var stars = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stars.name = "Stars";
            Object.DestroyImmediate(stars.GetComponent<Collider>());
            stars.transform.SetParent(root, false);
            stars.transform.position = new Vector3(0f, 10f, 120f);
            stars.transform.localScale = new Vector3(320f, 160f, 1f);
            stars.GetComponent<Renderer>().sharedMaterial = starMat;
            stars.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Station window frame around the view (beyond the cards, out of reach).
            void Bar(string name, Vector3 position, Vector3 size)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = name;
                Object.DestroyImmediate(bar.GetComponent<Collider>());
                bar.transform.SetParent(root, false);
                bar.transform.position = position;
                bar.transform.localScale = size;
                bar.GetComponent<Renderer>().sharedMaterial = frameMat;
            }
            Bar("Window Top", new Vector3(0f, 2.45f, 2.2f), new Vector3(5f, 0.12f, 0.12f));
            Bar("Window Left", new Vector3(-2.5f, 1.4f, 2.2f), new Vector3(0.12f, 2.2f, 0.12f));
            Bar("Window Right", new Vector3(2.5f, 1.4f, 2.2f), new Vector3(0.12f, 2.2f, 0.12f));
        }

        private static Material HubMaterial(string name, Shader shader, Color color, Texture texture)
        {
            var path = $"{HubDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Copies the factory's screenshot tool output (Builds/Shots/L&lt;N&gt;_*_front.png) into the Hub as previews.</summary>
        private static Texture[] ImportPreviews()
        {
            var result = new Texture[LevelCatalog.Levels.Count];
            var shots = Path.GetFullPath("Builds/Shots");
            for (var i = 0; i < result.Length; i++)
            {
                var asset = $"{PreviewDir}/L{i}_preview.png";
                if (Directory.Exists(shots))
                {
                    var source = Directory.GetFiles(shots, $"L{i}_*_front.png").OrderBy(p => p).FirstOrDefault();
                    if (source != null)
                    {
                        EnsureFolder(PreviewDir);
                        File.Copy(source, asset, true);
                        AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
                    }
                }
                result[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
            }
            return result;
        }

        // ------------------------------------------------------------------ helpers

        private static ColorBlock Primary(ColorBlock colors)
        {
            colors.normalColor = Theme.Accent;
            colors.highlightedColor = Color.Lerp(Theme.Accent, Color.white, 0.25f);
            colors.selectedColor = Theme.Accent;
            return colors;
        }

        /// <summary>Orients a world-space object so its front faces the seated eye point.</summary>
        private static void Face(Transform target)
        {
            var away = target.position - Eye;
            if (away.sqrMagnitude > 0.0001f)
                target.rotation = Quaternion.LookRotation(away, Vector3.up);
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
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            go.AddComponent<GraphicRaycaster>();
            return rect;
        }

        private static Image MakeImage(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            var image = rect.gameObject.AddComponent<Image>();
            UiPrefabBuilder.Rounded(image, color);
            image.raycastTarget = false;
            return image;
        }

        private static Text MakeLabel(Transform parent, string name, string value, int size, float x, float y, float width,
            float height, TextAnchor anchor)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = P1Style.Font(P1Style.Role.Body);
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
            float height, int size, string gazeId)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            var button = UiPrefabBuilder.StyleButton(rect.gameObject);
            var label = P1Style.Type(MakeLabel(rect, "Label", caption, size, 0f, 0f, width - 24f, height, TextAnchor.MiddleCenter),
                P1Style.Role.Title);
            UiPrefabBuilder.Gaze(button, gazeId, string.IsNullOrEmpty(caption) ? null : caption);
            return button;
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
