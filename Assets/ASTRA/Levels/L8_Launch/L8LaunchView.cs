using System.Collections.Generic;
using System.Globalization;
using Astra.App;
using Astra.Core.Mission;
using TMPro;
using UnityEngine;

/// <summary>
/// L8 Launch Day: the cinematic finale, from the launch pad to orbit. The trainee flies the ascent; every completed
/// step drives the next beat of the launch:
/// <list type="bullet">
/// <item><b>On the pad, at sunrise:</b> the launch tower and lightning masts in the window; the pad vents steam.</item>
/// <item><b>HARNESS, ENGINE ARM, GUIDANCE:</b> the flight computer's go/no-go calls.</item>
/// <item><b>LAUNCH:</b> a ten-second countdown with beeps. Ignition at T-6: a real launch roar, smoke billowing up past the
/// window, an orange engine glow, vibration. Liftoff at T-0: the tower drops away.</item>
/// <item><b>Ascent:</b> the clouds stream past (the punch-through), and the sky deepens from blue to black.</item>
/// <item><b>THROTTLE:</b> through MAX-Q. <b>STAGE:</b> a staging jolt and a second-stage light.
/// <b>TOWER JETT:</b> the escape tower streaks away. <b>CUTOFF:</b> silence, weightlessness, the Earth's curve and the stars.</item>
/// </list>
/// A flight-computer caption panel and live telemetry sit under the window.
/// The flight only advances as far as the completed steps allow, and it catches up fast when the trainee is quick.
/// Every new phase (Briefing, Demonstration, Practice, Qualification) starts on the pad again; the Debrief keeps the orbit view.
///
/// Comfort (team quality bar): the camera and the cabin never move; only the world outside the window does.
/// The "shake" is sound, vibration and millimetre rattles of decorative props. Nothing flashes faster than 3 Hz.
/// Visual and audio only: it never touches scoring or the mission.
///
/// Built in code at runtime from shaders every build already has: Sprites/Default (always included) and clones of
/// URP materials in the scene. The one asset is the launch roar: NASA's public-domain recording
/// "Discovery STS-131: Sound of Launch", trimmed into a seamless loop (Resources/L8/launch_roar.ogg).
/// OWNER: Core Logic (L8).
/// </summary>
public class L8LaunchView : MonoBehaviour
{
    // ---- filled by the level factory ----
    [SerializeField] private LevelController level;
    [SerializeField] private Light[] cabinLights = new Light[0];
    [SerializeField] private Renderer boosterSepLight;
    [SerializeField] private Renderer orbitLight;

    public const int StepCount = 11;
    public const float CountdownSeconds = 10f;
    public const float IgnitionAt = 4f;   // seconds into the countdown: T-6
    public const float FireAfterStaging = 3f;   // seconds of second-stage burn before the fire breaks out
    /// <summary>How far the flight may go (0 = pad, 1 = orbit) after N completed steps. The climb holds while the fire burns.</summary>
    private static readonly float[] Gate = { 0f, 0f, 0f, 0f, 0.34f, 0.34f, 0.48f, 0.48f, 0.64f, 0.93f, 1f, 1f };

    // mission steps by order (l8-launch-v1)
    private const int StepLaunch = 4, StepThrottle = 5, StepStage = 6, StepGrab = 7, StepExtinguish = 8, StepJettison = 9, StepCutoff = 10, StepReport = 11;

    /// <summary>True from the fire breaking out until it is put out.</summary>
    public bool FireBurning => _fireT >= 0f && !_fireOut;
    private const float ClimbRate = 0.026f;   // flight progress per second at cruise

    private static readonly Vector3 Eye = new Vector3(0f, 1.2f, 0f);
    private static readonly Vector3 TowerBase = new Vector3(6.8f, 0f, 20.9f);
    private static readonly string[] RattleNames =
    {
        "Cable_L0", "Cable_L1", "Cable_L2", "Cable_R0", "Cable_R1", "Cable_R2",
        "Strap_1", "Strap_2", "Stowage_Bag_1", "Stowage_Bag_2", "Stowage_Bag_3",
    };
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // ---- flight state ----
    public int Done { get; private set; }
    /// <summary>0 = on the pad, 1 = in orbit.</summary>
    public float Altitude { get; private set; }
    public float Countdown { get; private set; } = -1f;
    public float SinceLiftoff { get; private set; } = -1f;
    public bool Staged { get; private set; }
    public bool EngineCut { get; private set; }

    private int _demoDone;
    private float _thrust, _stageGap = -1f, _jettT = -1f, _floatT = -1f, _clock, _nextBuzz;
    private bool _ignited, _liftedOff, _maxQCalled, _boosterCalled, _jettCalled, _orbitCalled, _reported, _punchCalled;
    private int _lastBeep = -1;
    private float _fireT = -1f, _outT = -1f;   // seconds since the fire broke out / since it was put out
    private bool _fireOut;

    // ---- built objects ----
    private Transform _padRoot, _cloudRoot, _tower, _escapeTower;
    private ParticleSystem _smoke, _vent, _clouds, _cloudDeck, _debris, _escapeTrail;
    private Mesh _skyMesh;
    private Vector3[] _skyDirs;
    private Color32[] _skyColors;
    private MeshRenderer _sky;
    private Light _engineGlow, _escapeLight, _sun;
    private Color _sunColor0, _cabinColor0;
    private float _sunIntensity0;
    private float[] _cabinIntensity0 = new float[0];
    private AudioSource _roar, _sfx;
    private AudioClip _beep, _beepHigh, _clunk, _whoosh, _click;
    private TextMeshPro _caption, _telemetry;
    private string _captionTarget = string.Empty;
    private float _captionShown;
    private readonly List<(Transform t, Vector3 rest)> _rattle = new List<(Transform, Vector3)>();
    private readonly List<Renderer> _spaceRenderers = new List<Renderer>();
    private readonly List<ParticleSystem> _spaceParticles = new List<ParticleSystem>();
    private Transform _earth, _atmosphere;
    private Vector3 _earthPos0, _atmoPos0;
    private readonly List<Transform> _floaters = new List<Transform>();
    private readonly List<Vector3> _floaterRest = new List<Vector3>();
    private MaterialPropertyBlock _block;
    private Material _puffMat;

    // ------------------------------------------------------------------ lifecycle

    private void Start()
    {
        if (level == null) level = FindAnyObjectByType<LevelController>();
        _block = new MaterialPropertyBlock();
        _sun = FindSun();
        if (_sun != null) { _sunColor0 = _sun.color; _sunIntensity0 = _sun.intensity; }
        _cabinIntensity0 = new float[cabinLights != null ? cabinLights.Length : 0];
        for (int i = 0; i < _cabinIntensity0.Length; i++)
            if (cabinLights[i] != null) { _cabinIntensity0[i] = cabinLights[i].intensity; _cabinColor0 = cabinLights[i].color; }

        foreach (var n in RattleNames)
        {
            var go = GameObject.Find(n);
            if (go != null) _rattle.Add((go.transform, go.transform.localPosition));
        }
        CollectSpaceView();

        _puffMat = SpriteMaterial(PuffTexture(), "Launch_Puff (runtime)");
        BuildWindshield();
        BuildSky();
        BuildSun();
        BuildVapor();
        BuildPad();
        BuildClouds();
        BuildEscapeTower();
        BuildDebris();
        BuildCaptionPanel();
        BuildFloaters();
        BuildAudio();
        BuildFire();

        if (level != null)
        {
            level.PhaseChanged += OnPhaseChanged;
            level.DemonstrationStep += OnDemonstrationStep;
        }
        ResetFlight();
    }

    private void OnDestroy()
    {
        if (level == null) return;
        level.PhaseChanged -= OnPhaseChanged;
        level.DemonstrationStep -= OnDemonstrationStep;
    }

    private void OnPhaseChanged(ModulePhase phase)
    {
        if (phase == ModulePhase.Debrief) return;   // the debrief keeps the flight where it is
        ResetFlight();
        if (phase == ModulePhase.Briefing) Say("Good morning, Cadet. Weather is go and the pad is clear. Strap in when ready.");
        else if (phase == ModulePhase.Demonstration) Say("Watch the launch sequence once. Then it is your turn.");
        else if (phase == ModulePhase.Practice) Say("Your ship, Cadet. Strap in and work the checklist.");
        else if (phase == ModulePhase.Qualification) Say("Qualification flight. No help from here on. Good luck.");
    }

    private void OnDemonstrationStep(MissionStep step)
    {
        _demoDone = step == null ? StepCount : Mathf.Clamp(step.order, 0, StepCount);
    }

    private void Update()
    {
        if (level == null) return;
        if (level.IsPaused)
        {
            if (_roar != null && _roar.isPlaying) _roar.Pause();
            return;
        }
        if (_roar != null && !_roar.isPlaying && _thrust > 0.01f) { _roar.UnPause(); if (!_roar.isPlaying) _roar.Play(); }

        float dt = Time.unscaledDeltaTime;
        _clock += dt;
        int done = CompletedSteps();
        if (done > Done) { for (int s = Done + 1; s <= done; s++) OnStepDone(s); Done = done; }

        AdvanceFlight(dt);
        ApplySky();
        ApplySun();
        ApplyVapor();
        ApplyPad(dt);
        ApplyClouds();
        ApplySpace();
        ApplyEscapeTower(dt);
        ApplyCabin();
        ApplyIndicators();
        ApplyAudio(dt);
        ApplyFloaters(dt);
        ApplyFire(dt);
        ApplySpray(dt);
        Buzz();
        UpdateCaption(dt);
        UpdateTelemetry();
    }

    private int CompletedSteps()
    {
        switch (level.Phase)
        {
            case ModulePhase.Demonstration: return _demoDone;
            case ModulePhase.Practice:
            case ModulePhase.Qualification:
            case ModulePhase.Debrief:
            {
                var runner = level.Runner;
                if (runner == null) return Done;
                if (runner.IsComplete) return StepCount;
                var step = runner.CurrentStep;
                return step == null ? Done : Mathf.Max(Done, Mathf.Clamp(step.order - 1, 0, StepCount));
            }
            default: return 0;
        }
    }

    // ------------------------------------------------------------------ the story beats

    private void OnStepDone(int step)
    {
        switch (step)
        {
            case 1: Play(_click, 0.8f); Say("Harness locked. Cabin sealed."); break;
            case 2: Play(_clunk, 0.5f); Say("Engines armed. Propellant tanks pressurized."); VentBurst(); break;
            case 3: Play(_click, 0.6f); Say("Guidance internal. All stations report go. You are go for launch."); break;
            case StepLaunch: Countdown = 0f; Say("Final countdown. Hands clear of the ABORT handle."); break;
            case StepThrottle: Say("Throttle at 70 percent. Riding through MAX-Q."); _maxQCalled = true; break;
            case StepStage: BeginStaging(); break;
            case StepGrab: Play(_click, 0.7f); Say("Extinguisher in hand. Aim at the flames and spray!"); break;
            case StepExtinguish: PutOutFire(); break;
            case StepJettison: BeginJettison(); break;
            case StepCutoff: CutEngine(); break;
            case StepReport: _reported = true; Say("Mission control copies. Orbit confirmed. Well flown, Cadet."); break;
        }
    }

    private void AdvanceFlight(float dt)
    {
        if (Countdown >= 0f && !_liftedOff)
        {
            float speed = Done >= 5 ? 6f : 1f;   // a quick trainee is already past the countdown: catch up
            Countdown += dt * speed;
            int second = Mathf.FloorToInt(Countdown);
            if (second != _lastBeep && second < CountdownSeconds) { _lastBeep = second; Play(_beep, 0.35f); }
            if (!_ignited && Countdown >= IgnitionAt) Ignite();
            if (Countdown >= CountdownSeconds) Liftoff();
        }
        if (!_liftedOff) return;

        SinceLiftoff += dt;
        float gate = Gate[Mathf.Clamp(Done, 0, StepCount)];
        if (Altitude < gate)
        {
            // a rocket starts slow and builds speed; a trainee who is a whole segment ahead gets a fast catch-up
            float rate = ClimbRate * Mathf.Lerp(0.12f, 1f, Mathf.SmoothStep(0f, 1f, SinceLiftoff / 9f));
            float behind = Gate[Mathf.Clamp(Done - 1, 0, StepCount)];
            if (Altitude < behind - 0.02f) rate = ClimbRate * 3f;
            Altitude = Mathf.Min(gate, Altitude + rate * dt);
        }

        if (!_punchCalled && Altitude > 0.07f) { _punchCalled = true; Say("Passing through the cloud deck. Vehicle is stable."); }
        if (!_maxQCalled && Done < 5 && Altitude > 0.17f) { _maxQCalled = true; Say("Approaching MAX-Q. Throttle down to 70 percent."); }
        if (!_boosterCalled && !Staged && Altitude >= 0.32f) { _boosterCalled = true; Say("Booster burnout. BOOSTER SEP is lit. Stand by to STAGE."); }
        // the emergency: a few seconds into the second-stage burn, an electrical fire breaks out on AUX POWER
        if (Staged && _fireT < 0f && !_fireOut && Done < StepExtinguish && (_stageGap >= FireAfterStaging || Done >= StepGrab)) StartFire();
        if (!_jettCalled && Staged && !FireBurning && Done < StepJettison && Altitude >= 0.5f) { _jettCalled = true; Say("Above the thick air. Jettison the escape tower."); }
        if (!_orbitCalled && Altitude >= 0.9f && !EngineCut) { _orbitCalled = true; Say("Orbital velocity. ORBIT is lit. Cut the engine."); }
    }

    private void Ignite()
    {
        _ignited = true;
        Say("Ignition sequence start.");
        if (_smoke != null) { var em = _smoke.emission; em.rateOverTime = 70f; _smoke.Play(); }
        if (_roar != null) { _roar.volume = 0f; _roar.pitch = 1f; _roar.Play(); }
        _nextBuzz = 0f;
    }

    private void Liftoff()
    {
        _liftedOff = true;
        Countdown = CountdownSeconds;
        SinceLiftoff = 0f;
        Play(_beepHigh, 0.5f);
        Say("Liftoff! We have liftoff. Clear of the tower.");
    }

    private void BeginStaging()
    {
        if (!_liftedOff) { Countdown = CountdownSeconds; if (!_ignited) Ignite(); Liftoff(); }
        Staged = true;
        _stageGap = 0f;
        Play(_clunk, 1f);
        if (_debris != null) _debris.Play();
        Say("Staging. Booster away. Second stage ignition.");
        _nextBuzz = 0f;
    }

    private void BeginJettison()
    {
        _jettT = 0f;
        Play(_whoosh, 0.9f);
        if (_escapeTower != null) _escapeTower.gameObject.SetActive(true);
        if (_escapeTrail != null) _escapeTrail.Play();
        Say("Escape tower jettisoned. You can see the horizon now.");
    }

    private void CutEngine()
    {
        EngineCut = true;
        _floatT = 0f;
        Play(_clunk, 0.45f);
        Say("Engine cutoff. Weightless. Welcome to space, Cadet.");
    }

    // ------------------------------------------------------------------ applying the state each frame

    /// <summary>Engine thrust 0..1 from the current flight state.</summary>
    private float TargetThrust()
    {
        if (EngineCut || !_ignited) return 0f;
        if (!_liftedOff) return Mathf.Clamp01((Countdown - IgnitionAt) / 2f) * 0.85f;
        if (_stageGap >= 0f && _stageGap < 1.3f) return 0.05f;
        if (Staged) return 0.62f;
        return Done >= 5 ? 0.8f : 1f;
    }

    private void ApplyAudio(float dt)
    {
        if (_stageGap >= 0f) _stageGap += dt;
        float target = TargetThrust();
        float speed = EngineCut ? 2.5f : (_stageGap >= 0f && _stageGap < 1.3f ? 8f : 1.2f);
        _thrust = Mathf.MoveTowards(_thrust, target, speed * dt);
        if (_roar == null) return;
        _roar.volume = _thrust;
        _roar.pitch = Staged ? 1.12f : 1f;
        if (_thrust <= 0.001f && _roar.isPlaying && (EngineCut || !_ignited)) _roar.Stop();
    }

    private void ApplySky()
    {
        if (_skyMesh == null) return;
        float a = Altitude;
        float fade = 1f - Mathf.InverseLerp(0.75f, 0.92f, a);   // the dome fades out to reveal the real star field
        _sky.enabled = fade > 0.001f;
        if (!_sky.enabled) return;
        var sunDir = Direction(32f, 9f);
        for (int i = 0; i < _skyDirs.Length; i++)
        {
            var d = _skyDirs[i];
            float el = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
            Color c = SkyColor(el, a);
            float sunGlow = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(d, sunDir)), 12f) * (1f - a);
            c += new Color(1f, 0.55f, 0.25f) * (0.55f * sunGlow);
            c.a = fade;
            _skyColors[i] = c;
        }
        _skyMesh.colors32 = _skyColors;
    }

    /// <summary>Sunrise on the pad (a = 0), deepening to black space (a = 1), with a thin blue limb band low down.</summary>
    private static Color SkyColor(float el, float a)
    {
        Color zenith = new Color(0.13f, 0.27f, 0.56f), mid = new Color(0.36f, 0.56f, 0.84f);
        Color low = new Color(0.98f, 0.72f, 0.50f), horizon = new Color(1f, 0.56f, 0.34f), ground = new Color(0.16f, 0.15f, 0.19f);
        Color day;
        if (el < 0f) day = Color.Lerp(horizon, ground, Mathf.Clamp01(-el / 6f));
        else if (el < 10f) day = Color.Lerp(horizon, low, el / 10f);
        else if (el < 30f) day = Color.Lerp(low, mid, (el - 10f) / 20f);
        else day = Color.Lerp(mid, zenith, Mathf.Clamp01((el - 30f) / 60f));

        Color space = new Color(0.005f, 0.008f, 0.02f);
        Color limb = new Color(0.25f, 0.5f, 0.95f);
        float band = Mathf.Clamp01(1f - Mathf.Abs(el - 4f) / 10f);
        Color high = Color.Lerp(space, limb, band * 0.6f);
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.75f, a));
        return Color.Lerp(day, high, t);
    }

    private void ApplyPad(float dt)
    {
        if (_padRoot == null) return;
        // the pad falls away after liftoff: slow at first, then faster (the camera never moves)
        float drop = _liftedOff ? 0.5f * 7f * SinceLiftoff * SinceLiftoff + 30f * Altitude * Altitude * 60f : 0f;
        _padRoot.localPosition = new Vector3(0f, -drop, 0f);
        bool padVisible = drop < 400f;
        if (_padRoot.gameObject.activeSelf != padVisible) _padRoot.gameObject.SetActive(padVisible);

        if (_smoke != null && _ignited)
        {
            var em = _smoke.emission;
            em.rateOverTime = _liftedOff ? Mathf.Max(0f, 70f - SinceLiftoff * 30f) : Mathf.Lerp(25f, 70f, Mathf.InverseLerp(IgnitionAt, CountdownSeconds, Countdown));
        }
        if (_engineGlow != null)
        {
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(_clock * 2.2f, 0.3f);   // a slow shimmer, well under 3 Hz
            _engineGlow.intensity = _thrust * 2.4f * flicker * (Staged ? 0.6f : 1f);
        }
    }

    private void ApplyClouds()
    {
        if (_cloudRoot == null) return;
        // cloud deck sits ~130 m up (high in the window); the ship climbs through it early in the ascent
        float climb = Mathf.InverseLerp(0f, 0.22f, Altitude);
        _cloudRoot.localPosition = new Vector3(0f, -Mathf.Lerp(0f, 260f, climb * climb * (3f - 2f * climb)), 0f);
        bool visible = Altitude < 0.3f;
        if (_cloudRoot.gameObject.activeSelf != visible) _cloudRoot.gameObject.SetActive(visible);
    }

    private void ApplySun()
    {
        if (_launchSun == null) return;
        // the sun climbs into the windshield as the ship pitches over, then fades as the Earth takes over the view
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.5f, Altitude));
        var dir = Direction(Mathf.Lerp(30f, 22f, rise), Mathf.Lerp(3f, 24f, rise));
        _launchSun.position = Eye + dir * 240f;
        _launchSun.rotation = Quaternion.LookRotation(dir);
        float fade = 1f - Mathf.InverseLerp(0.7f, 0.9f, Altitude);
        _launchSunRenderer.enabled = fade > 0.01f;
        _launchSunRenderer.sharedMaterial.color = new Color(1f, Mathf.Lerp(0.8f, 0.97f, rise), Mathf.Lerp(0.55f, 0.9f, rise), fade);
    }

    private void ApplyVapor()
    {
        if (_vapor == null) return;
        // condensation streaks rush past the windshield through MAX-Q: the sense of speed
        bool on = _liftedOff && !Staged && Altitude > 0.1f && Altitude < 0.33f;
        var em = _vapor.emission;
        em.rateOverTime = on ? Mathf.Lerp(30f, 110f, Mathf.InverseLerp(0.1f, 0.25f, Altitude)) : 0f;
        if (on && !_vapor.isPlaying) _vapor.Play();
    }

    private void ApplySpace()
    {
        bool show = Altitude >= 0.55f;
        foreach (var r in _spaceRenderers) if (r != null && r.enabled != show) r.enabled = show;
        foreach (var p in _spaceParticles)
            if (p != null) { var pr = p.GetComponent<ParticleSystemRenderer>(); if (pr != null && pr.enabled != show) pr.enabled = show; }
        if (_earth == null) return;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.9f, Altitude));
        var low = _earthPos0 + new Vector3(0f, -95f, 10f);   // starts below the window and rises into view
        _earth.position = Vector3.Lerp(low, _earthPos0, t);
        if (_atmosphere != null) _atmosphere.position = _earth.position + (_atmoPos0 - _earthPos0);
    }

    private void ApplyEscapeTower(float dt)
    {
        if (_escapeTower == null || _jettT < 0f) return;
        _jettT += dt;
        float t = _jettT;
        var start = new Vector3(0.3f, 3.2f, 5f);   // low in the window, so the streak crosses it
        var dir = new Vector3(0.12f, 0.75f, 0.65f).normalized;
        _escapeTower.position = start + dir * (4f * t + 22f * t * t);
        _escapeTower.Rotate(Vector3.up, 90f * dt, Space.World);
        if (_escapeLight != null) _escapeLight.intensity = Mathf.Lerp(6f, 0f, t / 3f);
        if (t > 4f && _escapeTower.gameObject.activeSelf)
        {
            _escapeTower.gameObject.SetActive(false);
            if (_escapeTrail != null) _escapeTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void ApplyCabin()
    {
        // warm launch lighting while the engine burns; red alarm lighting (a 1 Hz pulse) while the fire burns;
        // back to the cabin's own light in orbit
        float warm = Mathf.Clamp01(_thrust) * 0.6f;
        float alarm = AlarmLevel();
        float pulse = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 1f * _clock);
        for (int i = 0; i < _cabinIntensity0.Length; i++)
        {
            var l = cabinLights[i];
            if (l == null) continue;
            var normal = Color.Lerp(_cabinColor0, new Color(1f, 0.78f, 0.58f), warm);
            l.color = Color.Lerp(normal, new Color(1f, 0.12f, 0.08f), alarm);
            l.intensity = _cabinIntensity0[i] * Mathf.Lerp(1f, 0.35f + 0.45f * pulse, alarm);
        }
        if (_alarmLight != null) _alarmLight.intensity = alarm * (0.4f + 2.6f * pulse);
        if (_sun != null)
        {
            float day = 1f - Mathf.InverseLerp(0.4f, 0.9f, Altitude);
            _sun.color = Color.Lerp(_sunColor0, new Color(1f, 0.74f, 0.52f), day);
        }

        float amp = (_liftedOff ? 0.0035f : 0.0018f) * _thrust;
        if (!_liftedOff || Done < 5) amp *= Altitude > 0.17f && Altitude < 0.34f ? 1.6f : 1f;   // MAX-Q is the roughest
        if (_stageGap >= 0f && _stageGap < 0.4f) amp = 0.006f;                                   // the staging jolt
        for (int i = 0; i < _rattle.Count; i++)
        {
            var (tr, rest) = _rattle[i];
            if (tr == null) continue;
            float s = _clock * 21f + i * 7.3f;
            tr.localPosition = rest + new Vector3(Mathf.PerlinNoise(s, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, s) - 0.5f,
                Mathf.PerlinNoise(s, s * 0.5f) - 0.5f) * (2f * amp);
        }
    }

    private void ApplyIndicators()
    {
        SetIndicator(boosterSepLight, "booster_sep_light", Staged ? 2 : (Altitude >= 0.32f ? 1 : 0), Staged ? "SEP DONE" : "BOOSTER SEP");
        SetIndicator(orbitLight, "orbit_light", Altitude >= 0.9f ? 2 : 0, "ORBIT");
    }

    /// <summary>state: 0 off, 1 amber, 2 green.</summary>
    private void SetIndicator(Renderer r, string id, int state, string text)
    {
        if (r == null) return;
        Color c = state == 2 ? new Color(0.19f, 0.82f, 0.35f) : state == 1 ? new Color(1f, 0.70f, 0.25f) : new Color(0.12f, 0.14f, 0.12f);
        r.GetPropertyBlock(_block);
        _block.SetColor(BaseColorId, c);
        _block.SetColor(EmissionColorId, state == 0 ? Color.black : c * 2.2f);
        r.SetPropertyBlock(_block);
        if (!_labels.TryGetValue(id, out var t))
        {
            var go = GameObject.Find(id + "_Label");
            t = go != null ? go.GetComponent<TMP_Text>() : null;
            _labels[id] = t;
        }
        if (t != null && t.text != text) t.text = text;
    }

    private readonly Dictionary<string, TMP_Text> _labels = new Dictionary<string, TMP_Text>();

    private void ApplyFloaters(float dt)
    {
        if (_floatT < 0f) return;
        _floatT += dt;
        for (int i = 0; i < _floaters.Count; i++)
        {
            var f = _floaters[i];
            if (f == null) continue;
            if (!f.gameObject.activeSelf) f.gameObject.SetActive(true);
            float t = _floatT;
            f.position = _floaterRest[i] + new Vector3(0.012f * Mathf.Sin(t * 0.4f + i), 0.025f * t * (0.6f + 0.25f * i), 0.01f * Mathf.Sin(t * 0.3f + i * 2f));
            f.Rotate(new Vector3(9f + i * 5f, 14f - i * 3f, 6f) * dt, Space.Self);
            if (t > 30f) f.gameObject.SetActive(false);
        }
    }

    private void Buzz()
    {
        if (_thrust < 0.3f || _clock < _nextBuzz) return;
        float gap = !_liftedOff ? 0.35f : (Done < 5 && Altitude > 0.17f) ? 0.4f : Staged ? 1.2f : 0.6f;
        if (_stageGap >= 0f && _stageGap < 0.5f) gap = 0.15f;
        if (FireBurning) gap = 0.8f;   // the alarm throb
        _nextBuzz = _clock + gap;
#if UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    // ------------------------------------------------------------------ the fire emergency

    private ParticleSystem _flames, _fireSmoke, _sparks, _co2;
    private Light _fireLight, _alarmLight;
    private AudioSource _alarm;
    private AudioClip _hiss;
    private Vector3 _firePos;

    private void StartFire()
    {
        _fireT = 0f;
        _fireOut = false;
        foreach (var ps in new[] { _flames, _fireSmoke, _sparks })
            if (ps != null) { SetRate(ps, 0f); ps.Play(); }
        if (_sparks != null) _sparks.Emit(40);   // the short-circuit flash
        Play(_clunk, 0.8f);
        Say("FIRE! FIRE! Electrical fire on AUX POWER. Grab the FIRE EXT on your left!");
        _nextBuzz = 0f;
    }

    private void PutOutFire()
    {
        if (_fireT < 0f) StartFire();   // the demonstration or a very quick trainee: still show the fire, then its end
        _fireOut = true;
        _outT = 0f;
        _sprayT = 0f;   // ApplySpray: the extinguisher comes up into the cadet's hand and sprays
        if (_co2 != null) { SetRate(_co2, 0f); _co2.Play(); }
        Play(_hiss, 0.9f);
        Say("Fire is out. Good work, Cadet. Alarm cleared. Resume the ascent.");
    }

    /// <summary>0..1: how strongly the alarm (red light, klaxon) is on.</summary>
    private float AlarmLevel() => _fireT < 0f ? 0f : !_fireOut ? Mathf.Clamp01(_fireT / 0.3f) : Mathf.Clamp01(1f - _outT / 0.8f);

    private void ApplyFire(float dt)
    {
        if (_fireT < 0f) return;
        if (!_fireOut)
        {
            _fireT += dt;
            float grow = Mathf.Clamp01(_fireT / 1.2f);
            SetRate(_flames, 95f * grow);
            SetRate(_fireSmoke, 18f * grow);
            SetRate(_sparks, 12f * grow);
            if (_fireLight != null) _fireLight.intensity = grow * (1.3f + 1.1f * Mathf.PerlinNoise(_clock * 2.5f, 1.7f));   // a flicker, not a strobe
            if (_alarm != null) { _alarm.volume = 0.42f; if (!_alarm.isPlaying) _alarm.Play(); }
        }
        else
        {
            _outT += dt;
            float smother = Mathf.Clamp01((_outT - 0.45f) / 0.9f);   // the flames die as the CO2 reaches them
            SetRate(_flames, Mathf.Lerp(95f, 0f, smother));
            SetRate(_fireSmoke, _outT < 0.45f ? 18f : 0f);
            SetRate(_sparks, 0f);
            if (_fireLight != null) _fireLight.intensity = (1f - smother) * 1.8f;
            if (_alarm != null)
            {
                _alarm.volume = Mathf.Max(0f, 0.42f * (1f - _outT / 0.7f));
                if (_alarm.volume <= 0f && _alarm.isPlaying) _alarm.Stop();
            }
        }
    }

    private static void SetRate(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.rateOverTime = Mathf.Max(0f, rate);
    }

    private static void Restart(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play();
    }

    private void BuildFire()
    {
        var zone = GameObject.Find("fire_zone");
        var pos = zone != null ? zone.transform.position : new Vector3(0f, 0.76f, 0.5f);
        var outward = zone != null ? -zone.transform.forward : (Eye - pos).normalized;   // out of the panel, toward the cadet
        _firePos = pos + outward * 0.015f;
        var up = Quaternion.Euler(-90f, 0f, 0f);   // emit straight up

        _flames = NewParticles("Cabin_Fire", null, _firePos, false);
        _flames.transform.rotation = up;
        var m = _flames.main;
        m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f); m.maxParticles = 200;
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var s = _flames.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(0.09f, 0.04f, 0.01f);
        var so = _flames.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        var n = _flames.noise; n.enabled = true; n.strength = 0.12f; n.frequency = 2f;
        var col = _flames.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.65f), 0f), new GradientColorKey(new Color(1f, 0.62f, 0.15f), 0.3f), new GradientColorKey(new Color(0.9f, 0.22f, 0.05f), 0.75f), new GradientColorKey(new Color(0.25f, 0.08f, 0.04f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.75f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        _fireSmoke = NewParticles("Cabin_Fire_Smoke", null, _firePos + Vector3.up * 0.08f, false);
        _fireSmoke.transform.rotation = up;
        var sm = _fireSmoke.main;
        sm.loop = true; sm.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f); sm.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); sm.maxParticles = 150;
        sm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var ss = _fireSmoke.shape; ss.shapeType = ParticleSystemShapeType.Cone; ss.angle = 18f; ss.radius = 0.04f;
        var sso = _fireSmoke.sizeOverLifetime; sso.enabled = true; sso.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 4f));
        var sn = _fireSmoke.noise; sn.enabled = true; sn.strength = 0.08f; sn.frequency = 0.6f;
        FadeOverLife(_fireSmoke, new Color(0.22f, 0.2f, 0.2f), new Color(0.12f, 0.12f, 0.13f));

        _sparks = NewParticles("Cabin_Fire_Sparks", null, _firePos, false);
        _sparks.transform.rotation = up;
        var pm = _sparks.main;
        pm.loop = true; pm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f); pm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
        pm.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.013f); pm.maxParticles = 120; pm.gravityModifier = 0.6f;
        pm.startColor = new Color(1f, 0.85f, 0.45f, 1f);
        var ps2 = _sparks.shape; ps2.shapeType = ParticleSystemShapeType.Cone; ps2.angle = 55f; ps2.radius = 0.02f;
        var spr = _sparks.GetComponent<ParticleSystemRenderer>(); spr.renderMode = ParticleSystemRenderMode.Stretch; spr.velocityScale = 0.04f; spr.lengthScale = 1f;

        // the extinguisher: the factory's generic tool reshaped into a red CO2 extinguisher with a hose and nozzle
        BuildExtinguisher(GameObject.Find("fire_extinguisher"));

        // the CO2 cloud: out of the nozzle (sprayed from the cadet's hand), into the flames
        _co2 = NewParticles("CO2_Spray", _nozzle, Vector3.zero, false);
        if (_nozzle == null) { _co2.transform.position = _firePos + outward * 0.3f; _co2.transform.rotation = Quaternion.LookRotation(-outward); }
        else _co2.transform.localRotation = Quaternion.identity;   // the nozzle's +Z
        var cm = _co2.main;
        cm.loop = true; cm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); cm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        cm.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f); cm.maxParticles = 400;
        cm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var cs = _co2.shape; cs.shapeType = ParticleSystemShapeType.Cone; cs.angle = 12f; cs.radius = 0.006f;
        var cso = _co2.sizeOverLifetime; cso.enabled = true; cso.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 7f));
        FadeOverLife(_co2, new Color(0.95f, 0.97f, 1f), new Color(0.9f, 0.92f, 0.95f));

        _fireLight = new GameObject("Cabin_Fire_Light").AddComponent<Light>();
        _fireLight.transform.position = _firePos + outward * 0.05f + Vector3.up * 0.1f;
        _fireLight.type = LightType.Point; _fireLight.color = new Color(1f, 0.5f, 0.15f); _fireLight.range = 1.8f; _fireLight.intensity = 0f; _fireLight.shadows = LightShadows.None;

        _alarmLight = new GameObject("Alarm_Light").AddComponent<Light>();
        _alarmLight.transform.position = new Vector3(0f, 2.05f, 0.3f);
        _alarmLight.type = LightType.Point; _alarmLight.color = new Color(1f, 0.1f, 0.06f); _alarmLight.range = 3.6f; _alarmLight.intensity = 0f; _alarmLight.shadows = LightShadows.None;

        _alarm = gameObject.AddComponent<AudioSource>();
        _alarm.clip = Klaxon();
        _alarm.loop = true; _alarm.playOnAwake = false; _alarm.spatialBlend = 0f; _alarm.volume = 0f;
        _hiss = Tone("Hiss", 1.8f, 0f, 1f, 0.75f);
    }

    private Transform _ext, _nozzle;
    private float _sprayT = -1f;
    private bool _inHand, _returning;
    private Vector3 _returnFrom;
    private Quaternion _returnFromRot;
    private Transform _extHomeParent;
    private Vector3 _extHomePos;
    private Quaternion _extHomeRot;

    /// <summary>
    /// Reshapes the level factory's generic tool (an orange box on a shaft) into a CO2 extinguisher: a red cylinder
    /// with a white band, a black valve head and handle, and a hose ending in a nozzle that points along the tool's +Z.
    /// The red cylinder stays the tool's "Body", so hover, gaze and Practice glow still light it.
    /// </summary>
    private void BuildExtinguisher(GameObject ext)
    {
        if (ext == null) return;
        _ext = ext.transform;
        _extHomeParent = _ext.parent;
        _extHomePos = _ext.position;
        _extHomeRot = _ext.rotation;
        var red = LitMaterial(new Color(0.8f, 0.05f, 0.04f), "Extinguisher_Red (runtime)");
        var black = LitMaterial(new Color(0.05f, 0.05f, 0.06f), "Extinguisher_Black (runtime)");
        var white = LitMaterial(new Color(0.92f, 0.92f, 0.9f), "Extinguisher_White (runtime)");
        foreach (var part in new[] { "Shaft", "Grip", "Tether_Ring" })
        {
            var t = _ext.Find(part);
            if (t != null && t.TryGetComponent<Renderer>(out var r)) r.enabled = false;
        }
        var body = _ext.Find("Body");
        if (body != null && body.TryGetComponent<MeshFilter>(out var mf) && body.TryGetComponent<Renderer>(out var br))
        {
            mf.sharedMesh = PrimitiveMesh(PrimitiveType.Cylinder);
            br.sharedMaterial = red;
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            body.localScale = new Vector3(0.075f, 0.1f, 0.075f);   // 7.5 cm across, 20 cm tall
        }
        Part("Ext_Band", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(0.078f, 0.012f, 0.078f), white);
        Part("Ext_Head", PrimitiveType.Cylinder, new Vector3(0f, 0.112f, 0f), Quaternion.identity, new Vector3(0.042f, 0.014f, 0.042f), black);
        Part("Ext_Valve", PrimitiveType.Cube, new Vector3(0f, 0.132f, 0f), Quaternion.identity, new Vector3(0.02f, 0.026f, 0.02f), black);
        Part("Ext_Handle", PrimitiveType.Cube, new Vector3(0f, 0.15f, -0.02f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(0.014f, 0.01f, 0.07f), black);
        Part("Ext_Lever", PrimitiveType.Cube, new Vector3(0f, 0.163f, -0.012f), Quaternion.Euler(-18f, 0f, 0f), new Vector3(0.014f, 0.008f, 0.06f), red);
        Part("Ext_Hose", PrimitiveType.Cylinder, new Vector3(0f, 0.11f, 0.05f), Quaternion.Euler(70f, 0f, 0f), new Vector3(0.012f, 0.05f, 0.012f), black);
        Part("Ext_Nozzle", PrimitiveType.Cylinder, new Vector3(0f, 0.095f, 0.1f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.024f, 0.022f, 0.024f), black);
        _nozzle = new GameObject("Ext_Nozzle_Tip").transform;
        _nozzle.SetParent(_ext, false);
        _nozzle.localPosition = new Vector3(0f, 0.095f, 0.125f);
        _nozzle.localRotation = Quaternion.identity;
        var label = _ext.Find("Label");
        if (label != null) label.localPosition = new Vector3(label.localPosition.x, 0.215f, label.localPosition.z);
    }

    private void Part(string name, PrimitiveType type, Vector3 localPos, Quaternion localRot, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(_ext, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static readonly Dictionary<PrimitiveType, Mesh> Meshes = new Dictionary<PrimitiveType, Mesh>();

    private static Mesh PrimitiveMesh(PrimitiveType type)
    {
        if (Meshes.TryGetValue(type, out var mesh) && mesh != null) return mesh;
        var go = GameObject.CreatePrimitive(type);
        mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Destroy(go);
        Meshes[type] = mesh;
        return mesh;
    }

    /// <summary>
    /// After the trainee taps the FIRE: the extinguisher comes back into the cadet's hand (lower right of the view),
    /// aims at the flames and sprays a CO2 cloud from its nozzle. Then it goes back on its bracket.
    /// The tool's own glide into the socket is allowed to finish first (0.25 s).
    /// </summary>
    private void ApplySpray(float dt)
    {
        if (_ext == null || _sprayT < 0f) return;
        _sprayT += dt;
        var cam = Camera.main;
        if (!_inHand && !_returning && _sprayT >= 0.3f && cam != null)
        {
            _ext.SetParent(cam.transform, true);
            if (_ext.TryGetComponent<Collider>(out var col)) col.enabled = false;   // never blocks the reticle or gets re-grabbed
            _inHand = true;
        }
        if (_inHand)
        {
            var hand = new Vector3(0.13f, -0.16f, 0.4f);
            _ext.localPosition = Vector3.Lerp(_ext.localPosition, hand, Mathf.Clamp01(dt * 10f));
            var aim = Quaternion.LookRotation(_firePos - _nozzle.position, Vector3.up);
            _ext.rotation = Quaternion.Slerp(_ext.rotation, aim, Mathf.Clamp01(dt * 10f));
            SetRate(_co2, _sprayT > 0.45f && _sprayT < 2.1f ? 170f : 0f);
            if (_sprayT >= 2.6f)
            {
                _inHand = false;
                _returning = true;
                _ext.SetParent(_extHomeParent, true);
                _returnFrom = _ext.position;
                _returnFromRot = _ext.rotation;
                _returnT = 0f;
            }
        }
        if (_returning)
        {
            _returnT += dt;
            float k = Mathf.SmoothStep(0f, 1f, _returnT / 0.45f);
            _ext.SetPositionAndRotation(Vector3.Lerp(_returnFrom, _extHomePos, k), Quaternion.Slerp(_returnFromRot, _extHomeRot, k));
            if (k >= 1f) { _returning = false; _sprayT = -1f; }
        }
    }

    private float _returnT;

    /// <summary>A blaring two-tone klaxon (620 / 880 Hz, a quarter second each), seamless as a 1 s loop.</summary>
    private static AudioClip Klaxon()
    {
        const int rate = 22050;
        const int n = rate;
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float seg = t % 0.5f;
            float f = seg < 0.25f ? 620f : 880f;
            float local = seg % 0.25f;
            float env = Mathf.Clamp01(local / 0.01f) * Mathf.Clamp01((0.25f - local) / 0.02f);
            float ph = 2f * Mathf.PI * f * t;
            d[i] = 0.45f * env * (Mathf.Sin(ph) + 0.33f * Mathf.Sin(3f * ph) + 0.2f * Mathf.Sin(5f * ph));
        }
        var clip = AudioClip.Create("Klaxon (generated)", n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    // ------------------------------------------------------------------ captions and telemetry

    private void Say(string text)
    {
        _captionTarget = text ?? string.Empty;
        _captionShown = 0f;
    }

    private void UpdateCaption(float dt)
    {
        if (_caption == null) return;
        _captionShown = Mathf.Min(_captionTarget.Length, _captionShown + 48f * dt);   // typed on, like a ship's computer
        int n = Mathf.FloorToInt(_captionShown);
        string shown = _captionTarget.Substring(0, n) + (n < _captionTarget.Length ? "_" : string.Empty);
        if (_caption.text != shown) _caption.text = shown;
        var color = FireBurning ? new Color(1f, 0.42f, 0.36f) : new Color(0.95f, 0.97f, 1f);
        if (_caption.color != color) _caption.color = color;
    }

    private void UpdateTelemetry()
    {
        if (_telemetry == null) return;
        string clock;
        if (!_liftedOff)
            clock = Countdown < 0f ? "T-00:10  HOLD" : "T-00:" + Mathf.CeilToInt(CountdownSeconds - Countdown).ToString("00", CultureInfo.InvariantCulture);
        else
            clock = "T+" + ((int)(SinceLiftoff / 60f)).ToString("00", CultureInfo.InvariantCulture) + ":" + ((int)SinceLiftoff % 60).ToString("00", CultureInfo.InvariantCulture);
        float km = 185f * Mathf.Pow(Altitude, 1.5f);
        float ms = 7800f * Mathf.Pow(Altitude, 1.15f);
        string stage = FireBurning ? "ALARM: FIRE" : EngineCut ? "COAST" : Staged ? "STAGE 2" : _ignited ? "STAGE 1" : "PAD";
        string text = clock + "    ALT " + km.ToString("0.0", CultureInfo.InvariantCulture) + " KM    VEL "
                      + Mathf.RoundToInt(ms).ToString("N0", CultureInfo.InvariantCulture) + " M/S    " + stage;
        if (_telemetry.text != text) _telemetry.text = text;
    }

    // ------------------------------------------------------------------ reset

    private void ResetFlight()
    {
        Done = 0; _demoDone = 0;
        Altitude = 0f; Countdown = -1f; SinceLiftoff = -1f;
        Staged = false; EngineCut = false;
        _thrust = 0f; _stageGap = -1f; _jettT = -1f; _floatT = -1f; _lastBeep = -1;
        _ignited = _liftedOff = _maxQCalled = _boosterCalled = _jettCalled = _orbitCalled = _reported = _punchCalled = false;
        if (_roar != null) _roar.Stop();
        if (_smoke != null) { _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var em = _smoke.emission; em.rateOverTime = 0f; }
        if (_debris != null) _debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_vapor != null) _vapor.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_escapeTrail != null) _escapeTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_escapeTower != null) _escapeTower.gameObject.SetActive(false);
        if (_padRoot != null) { _padRoot.gameObject.SetActive(true); _padRoot.localPosition = Vector3.zero; }
        if (_cloudRoot != null) { _cloudRoot.gameObject.SetActive(true); _cloudRoot.localPosition = Vector3.zero; }
        // a deactivated root can leave its particle systems stopped: restart the clouds and the pad steam for every new flight
        Restart(_clouds);
        Restart(_cloudDeck);
        if (_vent != null && !_vent.isPlaying) _vent.Play();
        _fireT = -1f; _outT = -1f; _fireOut = false;
        _sprayT = -1f; _inHand = false; _returning = false;   // TestTarget.ResetAll puts the extinguisher back on its bracket
        foreach (var ps in new[] { _flames, _fireSmoke, _sparks, _co2 })
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_fireLight != null) _fireLight.intensity = 0f;
        if (_alarmLight != null) _alarmLight.intensity = 0f;
        if (_alarm != null) _alarm.Stop();
        for (int i = 0; i < _floaters.Count; i++)
            if (_floaters[i] != null) { _floaters[i].gameObject.SetActive(false); _floaters[i].SetPositionAndRotation(_floaterRest[i], Quaternion.identity); }
        foreach (var (tr, rest) in _rattle) if (tr != null) tr.localPosition = rest;
        if (_engineGlow != null) _engineGlow.intensity = 0f;
        ApplySky();
        ApplySun();
        ApplySpace();
        ApplyIndicators();
        UpdateTelemetry();
    }

    // ------------------------------------------------------------------ building the world (runtime, no asset files)

    private static Vector3 Direction(float azimuthDeg, float elevationDeg)
    {
        float az = azimuthDeg * Mathf.Deg2Rad, el = elevationDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
    }

    private static Light FindSun()
    {
        var go = GameObject.Find("Sun");
        if (go != null && go.TryGetComponent<Light>(out var l)) return l;
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional) return light;
        return null;
    }

    private void CollectSpaceView()
    {
        var space = GameObject.Find("Space_View");
        if (space == null) return;
        _earth = space.transform.Find("Earth");
        _atmosphere = space.transform.Find("Atmosphere");
        if (_earth != null) _earthPos0 = _earth.position;
        if (_atmosphere != null) _atmoPos0 = _atmosphere.position;
        foreach (var r in space.GetComponentsInChildren<Renderer>(true))
            if (!(r is ParticleSystemRenderer)) _spaceRenderers.Add(r);
        _spaceParticles.AddRange(space.GetComponentsInChildren<ParticleSystem>(true));
    }

    /// <summary>A URP Lit material the level already uses (opaque, no emission): its shader and variants ship in every build.</summary>
    private static Material LitMaterial(Color color, string name)
    {
        Material pick = null;
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var m = r.sharedMaterial;
            if (m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Lit") continue;
            if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) continue;
            if (m.IsKeywordEnabled("_EMISSION") || m.IsKeywordEnabled("_ALPHATEST_ON")) continue;
            pick = m;
            if (m.name.StartsWith("Wall_Panel")) break;
        }
        var mat = pick != null ? new Material(pick) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = name;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
        if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.3f);
        return mat;
    }

    /// <summary>Sprites/Default is in the project's always-included shaders: alpha-blended, unlit, vertex-coloured.</summary>
    private static Material SpriteMaterial(Texture2D tex, string name)
    {
        var mat = new Material(Shader.Find("Sprites/Default")) { name = name };
        if (tex != null) mat.mainTexture = tex;
        return mat;
    }

    private static Texture2D PuffTexture()
    {
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "Puff (generated)", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - s * 0.5f) / (s * 0.5f), dy = (y - s * 0.5f) / (s * 0.5f);
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float n = 0.55f * Mathf.PerlinNoise(x * 0.06f + 11f, y * 0.06f + 3f) + 0.3f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f + 7f) + 0.15f * Mathf.PerlinNoise(x * 0.33f + 5f, y * 0.33f);
            float a = Mathf.Clamp01((1f - r) * 1.6f) * Mathf.Clamp01(n * 1.5f - 0.15f);
            a = a * a * (3f - 2f * a);
            byte v = (byte)(220 + 35 * n);
            px[y * s + x] = new Color32(v, v, v, (byte)(255 * Mathf.Clamp01(a)));
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        return tex;
    }

    /// <summary>
    /// Turns the cabin's porthole into a cockpit windshield: x ±0.95 m, from 1.30 m up to the ceiling, split into three panes.
    /// The level factory's front-wall pieces are resized at runtime (L8 only). The placards move below the glass.
    /// </summary>
    private void BuildWindshield()
    {
        const float wx = 0.95f, y0 = 1.30f, y1 = 2.12f, half = 1.3f;
        Resize("Wall_Front_Left", new Vector3(-(half + wx) * 0.5f, 1.1f, 1.05f), new Vector3(half - wx, 2.2f, 0.04f));
        Resize("Wall_Front_Right", new Vector3((half + wx) * 0.5f, 1.1f, 1.05f), new Vector3(half - wx, 2.2f, 0.04f));
        Resize("Wall_Front_Below", new Vector3(0f, y0 * 0.5f, 1.05f), new Vector3(2f * wx, y0, 0.04f));
        Resize("Wall_Front_Above", new Vector3(0f, (y1 + 2.2f) * 0.5f, 1.05f), new Vector3(2f * wx, 2.2f - y1, 0.04f));
        Resize("Window_Frame_Top", new Vector3(0f, y1 + 0.015f, 1.03f), new Vector3(2f * wx + 0.08f, 0.04f, 0.07f));
        Resize("Window_Frame_Bottom", new Vector3(0f, y0 - 0.015f, 1.03f), new Vector3(2f * wx + 0.08f, 0.04f, 0.07f));
        Resize("Window_Frame_Left", new Vector3(-wx - 0.02f, (y0 + y1) * 0.5f, 1.03f), new Vector3(0.04f, y1 - y0, 0.07f));
        Resize("Window_Frame_Right", new Vector3(wx + 0.02f, (y0 + y1) * 0.5f, 1.03f), new Vector3(0.04f, y1 - y0, 0.07f));
        var frame = GameObject.Find("Window_Frame_Top");
        if (frame != null && frame.TryGetComponent<Renderer>(out var fr))
            foreach (var x in new[] { -0.47f, 0.47f })   // two struts: a three-pane cockpit windshield
                Box("Windshield_Strut", frame.transform.parent, new Vector3(x, (y0 + y1) * 0.5f, 1.03f), new Vector3(0.03f, y1 - y0, 0.05f), fr.sharedMaterial);
        Move("Placard_Station", new Vector3(-0.72f, 1.235f, 1.025f));
        Move("Placard_Mission", new Vector3(-0.72f, 1.19f, 1.025f));
        Move("Placard_Fictional", new Vector3(-0.72f, 1.15f, 1.025f));
    }

    private static void Resize(string name, Vector3 localPos, Vector3 localScale)
    {
        var go = GameObject.Find(name);
        if (go == null) return;
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
    }

    private static void Move(string name, Vector3 localPos)
    {
        var go = GameObject.Find(name);
        if (go != null) go.transform.localPosition = localPos;
    }

    private Transform _launchSun;
    private Renderer _launchSunRenderer;
    private ParticleSystem _vapor;

    private void BuildSun()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Launch_Sun";
        Destroy(go.GetComponent<Collider>());
        go.transform.localScale = Vector3.one * 60f;   // ~14° of glow around a bright core, 240 m out
        _launchSunRenderer = go.GetComponent<Renderer>();
        var mat = SpriteMaterial(GlowTexture(), "Launch_Sun (runtime)");
        mat.renderQueue = 2960;   // over the sky dome, under the clouds
        _launchSunRenderer.sharedMaterial = mat;
        _launchSunRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _launchSun = go.transform;
    }

    private static Texture2D GlowTexture()
    {
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "Sun Glow (generated)", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - s * 0.5f) / (s * 0.5f), dy = (y - s * 0.5f) / (s * 0.5f);
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float core = Mathf.Clamp01((0.11f - r) / 0.02f);           // the disc
            float halo = Mathf.Pow(Mathf.Clamp01(1f - r), 3.2f) * 0.75f;  // the glare
            float a = Mathf.Clamp01(core + halo);
            px[y * s + x] = new Color32(255, 255, 255, (byte)(255 * a));
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        return tex;
    }

    private void BuildVapor()
    {
        _vapor = NewParticles("MaxQ_Vapor", null, new Vector3(0f, 11f, 4.5f), false);
        var m = _vapor.main;
        m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(26f, 40f); m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        m.maxParticles = 300; m.startColor = new Color(1f, 1f, 1f, 0.4f);
        var e = _vapor.emission; e.rateOverTime = 0f;
        var s = _vapor.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(9f, 3f, 0.5f);
        _vapor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // stream straight down past the glass
        var pr = _vapor.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.12f; pr.lengthScale = 2f;
        FadeOverLife(_vapor, Color.white, Color.white);
    }

    private void BuildSky()
    {
        const int rings = 24, segs = 48;
        const float radius = 280f;
        var verts = new List<Vector3>();
        var dirs = new List<Vector3>();
        var tris = new List<int>();
        for (int r = 0; r <= rings; r++)
        {
            float el = Mathf.Lerp(-30f, 90f, r / (float)rings);
            for (int s = 0; s <= segs; s++)
            {
                float az = 360f * s / segs;
                var d = Direction(az, el);
                dirs.Add(d);
                verts.Add(d * radius);
            }
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < segs; s++)
        {
            int a = r * (segs + 1) + s, b = a + segs + 1;
            tris.Add(a); tris.Add(b); tris.Add(a + 1);
            tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
        }
        _skyMesh = new Mesh { name = "Launch_Sky (runtime)" };
        _skyMesh.SetVertices(verts);
        _skyMesh.SetTriangles(tris, 0);
        _skyMesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2f);
        _skyDirs = dirs.ToArray();
        _skyColors = new Color32[_skyDirs.Length];
        var go = new GameObject("Launch_Sky");
        go.transform.position = Eye;
        go.AddComponent<MeshFilter>().sharedMesh = _skyMesh;
        _sky = go.AddComponent<MeshRenderer>();
        var mat = SpriteMaterial(null, "Launch_Sky (runtime)");
        mat.renderQueue = 2950;   // after the skybox and opaque objects, before the clouds and smoke
        _sky.sharedMaterial = mat;
        _sky.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _sky.receiveShadows = false;
    }

    private Transform Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    private void BuildPad()
    {
        _padRoot = new GameObject("Launch_Pad").transform;
        var red = LitMaterial(new Color(0.62f, 0.14f, 0.1f), "Tower_Red (runtime)");
        var steel = LitMaterial(new Color(0.42f, 0.44f, 0.47f), "Tower_Steel (runtime)");
        var white = LitMaterial(new Color(0.85f, 0.85f, 0.82f), "Tower_White (runtime)");

        // the launch tower: a square lattice, its top (with a beacon mast) in the right of the window
        _tower = new GameObject("Launch_Tower").transform;
        _tower.SetParent(_padRoot, false);
        _tower.localPosition = TowerBase;
        // Only the top of the tower is ever in the window (it drops out of view at liftoff), so the lattice stops at y = 0.
        const float w = 4f, top = 17f, bottom = -30f;
        foreach (var c in new[] { new Vector2(-w, -w), new Vector2(w, -w), new Vector2(-w, w), new Vector2(w, w) })
            Box("Leg", _tower, new Vector3(c.x * 0.5f, (top + bottom) * 0.5f, c.y * 0.5f), new Vector3(0.35f, top - bottom, 0.35f), red);
        for (float y = 1f; y <= top; y += 3.2f)
        {
            Box("Rail", _tower, new Vector3(0f, y, -w * 0.5f), new Vector3(w, 0.22f, 0.22f), red);
            Box("Rail", _tower, new Vector3(0f, y, w * 0.5f), new Vector3(w, 0.22f, 0.22f), red);
            Box("Rail", _tower, new Vector3(-w * 0.5f, y, 0f), new Vector3(0.22f, 0.22f, w), red);
            Box("Rail", _tower, new Vector3(w * 0.5f, y, 0f), new Vector3(0.22f, 0.22f, w), red);
            var brace = Box("Brace", _tower, new Vector3(0f, y + 1.6f, -w * 0.5f), new Vector3(0.16f, 4.5f, 0.16f), steel);
            brace.localRotation = Quaternion.Euler(0f, 0f, 41f);
        }
        Box("Roof", _tower, new Vector3(0f, top + 0.3f, 0f), new Vector3(w + 0.6f, 0.4f, w + 0.6f), steel);
        Box("Mast", _tower, new Vector3(0f, top + 2.5f, 0f), new Vector3(0.25f, 4.5f, 0.25f), white);
        Box("Mast_Tip", _tower, new Vector3(0f, top + 4.9f, 0f), new Vector3(0.45f, 0.45f, 0.45f), red);

        // lightning masts: tall white poles whose tops stand 24–33° up, inside the window's view
        foreach (var (az, dist, topY) in new[] { (-22f, 60f, 36f), (-8f, 85f, 45f), (24f, 120f, 55f) })
        {
            var p = Direction(az, 0f) * dist;
            Box("Lightning_Mast", _padRoot, new Vector3(p.x, (topY - 60f) * 0.5f, p.z), new Vector3(0.8f, topY + 60f, 0.8f), white);
            Box("Lightning_Mast_Tip", _padRoot, new Vector3(p.x, topY + 0.4f, p.z), new Vector3(1.4f, 0.8f, 1.4f), red);
        }

        // pad venting: white wisps drifting off the tower
        _vent = NewParticles("Pad_Vent", _padRoot, TowerBase + new Vector3(-2.2f, 9f, 0f), true);
        var vm = _vent.main;
        vm.loop = true; vm.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f); vm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        vm.startSize = new ParticleSystem.MinMaxCurve(2f, 5f); vm.maxParticles = 120;
        vm.startColor = new Color(1f, 0.93f, 0.88f, 0.55f);
        var ve = _vent.emission; ve.rateOverTime = 5f;
        var vs = _vent.shape; vs.shapeType = ParticleSystemShapeType.Cone; vs.angle = 25f; vs.radius = 0.5f;
        _vent.transform.localRotation = Quaternion.Euler(-60f, -90f, 0f);
        FadeOverLife(_vent, new Color(1f, 0.95f, 0.9f), new Color(1f, 1f, 1f));
        _vent.Play();

        // ignition smoke: billows up from the pad and fills the windshield
        _smoke = NewParticles("Launch_Smoke", _padRoot, new Vector3(0f, -8f, 14f), true);
        var sm = _smoke.main;
        sm.loop = true; sm.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f); sm.startSpeed = new ParticleSystem.MinMaxCurve(7f, 16f);
        sm.startSize = new ParticleSystem.MinMaxCurve(10f, 24f); sm.maxParticles = 900;
        sm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var se = _smoke.emission; se.rateOverTime = 0f;
        var ss = _smoke.shape; ss.shapeType = ParticleSystemShapeType.Box; ss.scale = new Vector3(50f, 16f, 4f);
        _smoke.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // emit upward
        var sz = _smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
        var col = _smoke.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.62f, 0.32f), 0f), new GradientColorKey(new Color(0.92f, 0.86f, 0.8f), 0.25f), new GradientColorKey(new Color(0.8f, 0.8f, 0.82f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.1f), new GradientAlphaKey(0.45f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        // the engine's glow lights the cabin from below (no shadows, so it reaches in through the floor)
        _engineGlow = new GameObject("Engine_Glow").AddComponent<Light>();
        _engineGlow.transform.position = new Vector3(0f, -0.4f, 0.4f);
        _engineGlow.type = LightType.Point; _engineGlow.color = new Color(1f, 0.52f, 0.2f);
        _engineGlow.range = 4.5f; _engineGlow.intensity = 0f; _engineGlow.shadows = LightShadows.None;
    }

    private void BuildClouds()
    {
        _cloudRoot = new GameObject("Launch_Clouds").transform;
        // scattered sunrise clouds high in the window
        _clouds = NewParticles("Clouds_High", _cloudRoot, new Vector3(0f, 125f, 150f), true);
        var m = _clouds.main;
        m.loop = false; m.startLifetime = 1e5f; m.startSpeed = 0f; m.maxParticles = 260;
        m.startSize = new ParticleSystem.MinMaxCurve(30f, 70f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.8f, 0.6f), new Color(1f, 0.74f, 0.62f, 0.42f));
        var e = _clouds.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 150) });
        var s = _clouds.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(380f, 25f, 260f);
        _clouds.Play();
        // the deck the ship punches through: dense and close
        _cloudDeck = NewParticles("Cloud_Deck", _cloudRoot, new Vector3(0f, 150f, 30f), true);
        var dm = _cloudDeck.main;
        dm.loop = false; dm.startLifetime = 1e5f; dm.startSpeed = 0f; dm.maxParticles = 220;
        dm.startSize = new ParticleSystem.MinMaxCurve(8f, 20f);
        dm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        dm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.92f, 0.7f), new Color(0.95f, 0.88f, 0.86f, 0.55f));
        var de = _cloudDeck.emission; de.rateOverTime = 0f; de.SetBursts(new[] { new ParticleSystem.Burst(0f, 160) });
        var ds = _cloudDeck.shape; ds.shapeType = ParticleSystemShapeType.Box; ds.scale = new Vector3(110f, 18f, 90f);
        _cloudDeck.Play();
    }

    private void BuildEscapeTower()
    {
        _escapeTower = new GameObject("Escape_Tower").transform;
        var white = LitMaterial(new Color(0.9f, 0.9f, 0.88f), "Escape_Tower (runtime)");
        Box("Escape_Body", _escapeTower, Vector3.zero, new Vector3(0.6f, 2.6f, 0.6f), white);
        Box("Escape_Nozzles", _escapeTower, new Vector3(0f, -1.4f, 0f), new Vector3(0.9f, 0.3f, 0.9f), white);
        _escapeLight = new GameObject("Escape_Flare").AddComponent<Light>();
        _escapeLight.transform.SetParent(_escapeTower, false);
        _escapeLight.transform.localPosition = new Vector3(0f, -2f, 0f);
        _escapeLight.type = LightType.Point; _escapeLight.color = new Color(1f, 0.8f, 0.55f); _escapeLight.range = 25f; _escapeLight.shadows = LightShadows.None;
        _escapeTrail = NewParticles("Escape_Trail", _escapeTower, new Vector3(0f, -1.8f, 0f), false);
        var m = _escapeTrail.main;
        m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f); m.startSpeed = 0.5f; m.maxParticles = 300;
        m.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        var e = _escapeTrail.emission; e.rateOverTime = 90f;
        var s = _escapeTrail.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.3f;
        FadeOverLife(_escapeTrail, new Color(1f, 0.85f, 0.6f), new Color(0.9f, 0.9f, 0.92f));
        _escapeTower.gameObject.SetActive(false);
    }

    private void BuildDebris()
    {
        // staging: bright fragments and vapour falling away past the window
        _debris = NewParticles("Stage_Debris", null, new Vector3(0f, 9f, 9f), false);
        var m = _debris.main;
        m.loop = false; m.duration = 1f; m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 12f); m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 3f); m.maxParticles = 160;
        m.gravityModifier = 0.8f;
        var e = _debris.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 70), new ParticleSystem.Burst(0.25f, 50) });
        var s = _debris.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 3f;
        FadeOverLife(_debris, new Color(1f, 0.82f, 0.6f), new Color(0.85f, 0.85f, 0.9f));
    }

    private ParticleSystem NewParticles(string name, Transform parent, Vector3 localPos, bool local)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = _puffMat;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.maxParticleSize = 6f;   // close clouds may fill the view
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    private static void FadeOverLife(ParticleSystem ps, Color from, Color to)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private void VentBurst()
    {
        if (_vent == null) return;
        _vent.Emit(25);
    }

    private void BuildCaptionPanel()
    {
        // under the window: a dark plate with the flight computer's line and live telemetry
        var pos = new Vector3(0f, 1.36f, 0.972f);   // a heads-up strip along the bottom of the windshield, above the dashboard
        var rot = Quaternion.LookRotation(pos - Eye);
        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Flight_Computer_Plate";
        Destroy(plate.GetComponent<Collider>());
        plate.transform.SetPositionAndRotation(pos + rot * new Vector3(0f, 0f, 0.006f), rot);
        plate.transform.localScale = new Vector3(1.0f, 0.14f, 0.004f);
        var frame = GameObject.Find("Window_Frame_Top");
        var pr = plate.GetComponent<Renderer>();
        if (frame != null && frame.TryGetComponent<Renderer>(out var fr) && fr.sharedMaterial != null) pr.sharedMaterial = fr.sharedMaterial;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Text("Flight_Computer_Label", pos + rot * new Vector3(-0.47f, 0.066f, 0f), rot, "FLIGHT COMPUTER", 0.014f, new Color(0.35f, 0.8f, 1f), 0f);
        _caption = Text("Flight_Computer_Caption", pos + rot * new Vector3(-0.47f, 0.045f, 0f), rot, "", 0.021f, new Color(0.95f, 0.97f, 1f), 0.94f);
        _telemetry = Text("Flight_Telemetry", pos + rot * new Vector3(-0.47f, -0.048f, 0f), rot, "", 0.018f, new Color(1f, 0.76f, 0.34f), 0f);
    }

    /// <summary>World-space text anchored at its top-left corner. <paramref name="wrapWidth"/> &gt; 0 wraps lines at that width (metres).</summary>
    private static TextMeshPro Text(string name, Vector3 pos, Quaternion rot, string text, float capHeight, Color color, float wrapWidth)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(pos, rot);
        var tmp = go.AddComponent<TextMeshPro>();
        var font = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) tmp.font = font;
        tmp.fontSize = 10f;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.pivot = new Vector2(0f, 1f);
        tmp.rectTransform.sizeDelta = new Vector2(400f, 40f);
        tmp.text = "HX";
        tmp.ForceMeshUpdate();
        float measured = Mathf.Max(0.0001f, tmp.textBounds.size.y);
        float scale = capHeight / measured;
        go.transform.localScale *= scale;
        if (wrapWidth > 0f)
        {
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = new Vector2(wrapWidth / scale, 40f);
        }
        tmp.text = text;
        return tmp;
    }

    private void BuildFloaters()
    {
        // after engine cutoff these drift up off the FLIGHT panel: the classic "weightless" moment
        var pen = LitMaterial(new Color(0.15f, 0.35f, 0.75f), "Floater_Pen (runtime)");
        var card = LitMaterial(new Color(0.92f, 0.9f, 0.82f), "Floater_Card (runtime)");
        var metal = LitMaterial(new Color(0.7f, 0.72f, 0.75f), "Floater_Metal (runtime)");
        AddFloater("Floating_Pen", new Vector3(-0.12f, 1.25f, 0.5f), new Vector3(0.008f, 0.14f, 0.008f), pen);
        AddFloater("Floating_Checklist", new Vector3(0.1f, 1.24f, 0.48f), new Vector3(0.09f, 0.002f, 0.12f), card);
        AddFloater("Floating_Washer", new Vector3(0.02f, 1.27f, 0.45f), new Vector3(0.02f, 0.004f, 0.02f), metal);
    }

    private void AddFloater(string name, Vector3 pos, Vector3 size, Material mat)
    {
        var t = Box(name, null, pos, size, mat);
        t.gameObject.SetActive(false);
        _floaters.Add(t);
        _floaterRest.Add(pos);
    }

    // ------------------------------------------------------------------ audio

    private void BuildAudio()
    {
        _roar = gameObject.AddComponent<AudioSource>();
        _roar.clip = Resources.Load<AudioClip>("L8/launch_roar");
        _roar.loop = true; _roar.playOnAwake = false; _roar.spatialBlend = 0f; _roar.volume = 0f;
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false; _sfx.spatialBlend = 0f;
        _beep = Tone("Beep", 0.12f, 880f, 0f, 0.35f);
        _beepHigh = Tone("Beep_High", 0.5f, 1320f, 0f, 0.4f);
        _click = Tone("Click", 0.06f, 2400f, 1f, 0.5f);
        _clunk = Tone("Clunk", 0.7f, 55f, 0.6f, 1f);
        _whoosh = Tone("Whoosh", 1.6f, 0f, 1f, 0.8f);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (_sfx != null && clip != null) _sfx.PlayOneShot(clip, volume);
    }

    /// <summary>A short generated effect: a sine tone (freq > 0) mixed with filtered noise, with a quick attack and a decay.</summary>
    private static AudioClip Tone(string name, float seconds, float freq, float noise, float gain)
    {
        const int rate = 22050;
        int n = Mathf.Max(1, (int)(rate * seconds));
        var d = new float[n];
        var rng = new System.Random(name.GetHashCode());
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-3.5f * t / seconds);
            float w = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (freq <= 0f ? 0.05f + 0.25f * (1f - t / seconds) : 0.3f) * (w - lp);   // a falling filter makes the whoosh
            float tone = freq > 0f ? Mathf.Sin(2f * Mathf.PI * freq * t) : 0f;
            d[i] = gain * env * ((1f - noise) * tone + noise * lp * 2.5f);
        }
        var clip = AudioClip.Create(name + " (generated)", n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
