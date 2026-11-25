using System;
using System.IO;
using TaxiSim.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TaxiSim.World
{
    /// <summary>
    /// Entry point of the 3D world. Put this on one empty GameObject in an empty scene (the editor menu
    /// "Taxi Manager → Setup BKC 3D World" does it for you) and press Play: the whole BKC city —
    /// terrain, river, roads, flyovers, buildings, landmarks, transit, traffic, sky, weather, lighting
    /// and post-processing — is generated from the simulation core's map data.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class BkcWorldBootstrap : MonoBehaviour
    {
        [Tooltip("Assigned automatically by the editor setup so the shaders are included in builds.")]
        public ShaderSet Shaders = new ShaderSet();

        [Header("Start conditions")]
        [Range(0f, 24f)] public float StartHour = 15.5f;
        public bool StartTimeLapse = true;
        public WeatherMode StartWeather = WeatherMode.Clear;
        [Tooltip("Auto = realistic day + subtle neon at night (V cycles Auto / Neon / Realistic in Play mode).")]
        public VisualStyle StartStyle = VisualStyle.Auto;
        [Tooltip("Show the title screen when Play starts (untick to go straight into the city).")]
        public bool ShowTitleScreen = true;

        public CityMap Map { get; private set; }
        public WorldContext Context { get; private set; }

        private Camera _cam;
        private ReflectionProbe _probe;
        private TimeOfDay _time;
        private WeatherSystem _weather;
        private PostFxRig _post;
        private CityCameraRig _rig;
        private StreetLights _lamps;
        private TrafficAmbient _traffic;
        private CityPulse _pulse;
        private RouteShowcase _routes;
        private WorldOverlay _overlay;
        private GraphicsTier _tier;
        private Transform _neonRoot;
        private AirTraffic _air;
        private VisualStyle _style;
        private string _textureReport = "";
        private CitySoundscape _sound;
        private CinematicDirector _director;
        private FloodSystem _floods;
        private FestivalSystem _festivals;
        private MinimapView _minimap;
        private GameMenu _menu;
        private SimulationDriver _game;

        /// <summary>The dispatch game (simulation + console + HUD).</summary>
        public SimulationDriver Game => _game;

        private void Awake()
        {
            float t0 = Time.realtimeSinceStartup;
            _tier = GraphicsQuality.Load();
            DisableOtherCamerasAndSuns();

            Map = BkcMap.Create();
            var mats = new MaterialLibrary(Shaders);
            _textureReport = mats.ApplyRealTextures();
            var worldRoot = new GameObject("BKC_City").transform;

            var ctx = new WorldContext
            {
                Map = Map,
                Mats = mats,
                Space = new SpaceRegistry(),
                Towers = new TowerMeshBuilder(),
                Root = worldRoot,
                Density = GraphicsQuality.Density(_tier)
            };
            Context = ctx;

            // ---- City generation (order matters: each step registers the space it uses) ----
            TerrainBuilder.Build(Map, mats, worldRoot);
            ctx.Roads = RoadNetworkBuilder.Build(Map, mats, worldRoot, ctx.Space);
            TransitBuilder.Build(ctx);
            LandmarkBuilder.Build(ctx);
            var buildingProps = new ChunkedMeshBuilder();
            CityBlockBuilder.StreetFrontage(Map, ctx.Roads, ctx.Towers, ctx.Space, ctx.Density, buildingProps);
            CityBlockBuilder.Build(Map, ctx.Towers, ctx.Space, ctx.Density, buildingProps);
            StreetLifeBuilder.Stats life = StreetLifeBuilder.Build(ctx);
            StreetClutterBuilder.Stats clutter = StreetClutterBuilder.Build(ctx);
            NatureBuilder.Build(ctx);
            ctx.Towers.Emit("Buildings", worldRoot, mats.Tower);
            var propsRoot = new GameObject("Building Props (balconies, tanks, AC, parapets)").transform;
            propsRoot.SetParent(worldRoot, false);
            buildingProps.Emit("BuildingProps", propsRoot, mats.Concrete);
            ctx.EmitDetails();
            _neonRoot = NeonBuilder.Build(ctx);

            // ---- Lighting, sky, reflections ----
            var sunGo = new GameObject("Sun & Moon");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowNormalBias = 0.3f;
            sun.shadowBias = 0.05f;
            RenderSettings.sun = sun;
            if (mats.Sky != null) RenderSettings.skybox = mats.Sky;

            var probeGo = new GameObject("City Reflection Probe");
            probeGo.transform.position = new Vector3(170f, 25f, 120f);
            _probe = probeGo.AddComponent<ReflectionProbe>();
            _probe.mode = ReflectionProbeMode.Realtime;
            _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            _probe.size = new Vector3(1600f, 400f, 1600f);
            _probe.hdr = true;
            _probe.farClipPlane = 1800f;
            _probe.nearClipPlane = 1f;
            _probe.clearFlags = ReflectionProbeClearFlags.Skybox;

            // Box-projected local probes (Ultra / Cinematic): sharper reflections on wet streets and glass
            var localProbes = new System.Collections.Generic.List<ReflectionProbe>();
            if (GraphicsQuality.LocalReflectionProbes(_tier))
            {
                localProbes.Add(LocalProbe("Probe_GBlock", new Vector3(185f, 12f, 140f), new Vector3(170f, 40f, 150f)));
                localProbes.Add(LocalProbe("Probe_Equinox_LBS", new Vector3(310f, 12f, 115f), new Vector3(110f, 40f, 170f)));
                localProbes.Add(LocalProbe("Probe_Kurla", new Vector3(350f, 10f, 95f), new Vector3(60f, 30f, 110f)));
            }

            // ---- Camera ----
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            _cam = camGo.AddComponent<Camera>();
            _cam.fieldOfView = 40f;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 4000f;
            camGo.AddComponent<AudioListener>();

            // ---- Systems ----
            var systems = new GameObject("World Systems");
            _post = systems.AddComponent<PostFxRig>();
            _post.Init();

            _weather = systems.AddComponent<WeatherSystem>();
            _weather.Mode = StartWeather;
            _weather.FollowCamera = camGo.transform;
            _weather.MaxRainParticlesPerSecond = _tier >= GraphicsTier.Ultra ? 10000 : _tier == GraphicsTier.High ? 7000 : 4000;
            _weather.Init(mats.Rain);

            var lampsGo = new GameObject("Street Lights");
            _lamps = lampsGo.AddComponent<StreetLights>();
            _lamps.Build(ctx.Roads, GraphicsQuality.StreetLightCount(_tier));

            var signalsGo = new GameObject("Traffic Signals");
            var signals = signalsGo.AddComponent<TrafficSignals>();
            signals.Build(ctx);

            var trafficGo = new GameObject("Traffic");
            _traffic = trafficGo.AddComponent<TrafficAmbient>();
            _traffic.Signals = signals;
            _traffic.Init(ctx, GraphicsQuality.TrafficCount(_tier), _tier >= GraphicsTier.High);

            var pedGo = new GameObject("Pedestrians");
            var pedestrians = pedGo.AddComponent<PedestrianSystem>();
            pedestrians.Signals = signals;
            pedestrians.Init(ctx, GraphicsQuality.PedestrianCount(_tier));

            _pulse = systems.AddComponent<CityPulse>();
            _pulse.Graph = Map.Graph;
            _pulse.Roads = ctx.Roads;
            _pulse.Weather = _weather;

            var routesGo = new GameObject("Route Showcase");
            _routes = routesGo.AddComponent<RouteShowcase>();
            _routes.Graph = Map.Graph;
            _routes.Roads = ctx.Roads;
            _routes.Mats = mats;
            _routes.Pulse = _pulse;

            _time = systems.AddComponent<TimeOfDay>();
            _time.Hour = StartHour;
            _time.TimeLapse = StartTimeLapse;
            _time.SkyLight = sun;
            _time.SkyMaterial = mats.Sky;
            _time.HdriSkyMaterial = mats.HdriSky;
            _time.Probe = _probe;
            _time.LocalProbes = localProbes;
            _time.Post = _post;
            _time.Lamps = _lamps;
            _time.Traffic = _traffic;
            _time.Weather = _weather;
            _pulse.Clock = _time;

            var airGo = new GameObject("Air Taxis");
            airGo.transform.SetParent(_neonRoot, false);
            _air = airGo.AddComponent<AirTraffic>();
            _air.Init(ctx, GraphicsQuality.AirTaxiCount(_tier));

            _rig = camGo.AddComponent<CityCameraRig>();
            _rig.Cam = _cam;
            _rig.Geography = Map.Geography;
            _rig.Post = _post;
            _rig.Landmarks = ctx.Landmarks;
            _rig.Traffic = _traffic;

            // ---- Sound and cinematic mode ----
            var soundGo = new GameObject("City Soundscape");
            _sound = soundGo.AddComponent<CitySoundscape>();
            _sound.Listener = camGo.transform;
            _sound.Traffic = _traffic;
            _sound.Pedestrians = pedestrians;
            _sound.Signals = signals;
            _sound.Weather = _weather;
            _sound.Clock = _time;
            _sound.Init(ctx);

            // ---- City events: monsoon waterlogging and festivals ----
            var eventsGo = new GameObject("City Events");
            _floods = eventsGo.AddComponent<FloodSystem>();
            _floods.Weather = _weather;
            _floods.Traffic = _traffic;
            _floods.CameraTransform = camGo.transform;
            _floods.Init(ctx);
            _traffic.SpeedFactor = _floods.SpeedAt;

            _festivals = eventsGo.AddComponent<FestivalSystem>();
            _festivals.Traffic = _traffic;
            _festivals.Pulse = _pulse;
            _festivals.Sound = _sound;
            _festivals.Clock = _time;
            _festivals.CameraTransform = camGo.transform;
            _festivals.Init(ctx);

            _overlay = systems.AddComponent<WorldOverlay>();
            _overlay.Cam = _cam;
            _overlay.Clock = _time;
            _overlay.Weather = _weather;
            _overlay.Rig = _rig;
            _overlay.Routes = _routes;
            _overlay.Pulse = _pulse;
            _overlay.Traffic = _traffic;
            _overlay.Landmarks = ctx.Landmarks;
            _overlay.BuildingCount = ctx.Towers.BuildingCount;
            _overlay.TextureReport = _textureReport;

            _director = systems.AddComponent<CinematicDirector>();
            _director.Cam = _cam;
            _director.Rig = _rig;
            _director.Post = _post;
            _director.Overlay = _overlay;
            _director.Clock = _time;
            _director.Traffic = _traffic;
            _director.Pedestrians = pedestrians;
            _director.Signals = signals;
            _director.Init(ctx);

            _minimap = systems.AddComponent<MinimapView>();
            _minimap.Cam = _cam;
            _minimap.Rig = _rig;
            _minimap.Traffic = _traffic;
            _minimap.Signals = signals;
            _minimap.Festivals = _festivals;
            _minimap.Floods = _floods;
            _minimap.Overlay = _overlay;
            _minimap.Init(ctx);

            _menu = systems.AddComponent<GameMenu>();
            _menu.World = this;
            _menu.Rig = _rig;
            _menu.Director = _director;
            _menu.Overlay = _overlay;
            _menu.Sound = _sound;
            _menu.Clock = _time;
            _menu.Weather = _weather;
            _menu.Festivals = _festivals;
            _menu.Floods = _floods;
            _menu.Minimap = _minimap;
            _menu.ShowTitleOnStart = ShowTitleScreen;
            _director.Menu = _menu;

            // ---- Dispatch game: simulation driver, ESP32 console bridge, HUD ----
            var gameGo = new GameObject("Dispatch Game");
            _game = gameGo.AddComponent<SimulationDriver>();
            _game.Ctx = ctx;
            _game.Traffic = _traffic;
            _game.Clock = _time;
            _game.Weather = _weather;
            _game.Rig = _rig;
            _game.Pulse = _pulse;
            _game.Overlay = _overlay;
            _game.Menu = _menu;
            _menu.Game = _game;
            var hud = gameGo.AddComponent<DispatchHud>();
            var bridge = gameGo.AddComponent<ConsoleBridge>();
            hud.Game = _game;
            hud.Bridge = bridge;
            hud.Cam = _cam;
            hud.Traffic = _traffic;
            hud.Menu = _menu;
            hud.Overlay = _overlay;
            bridge.Game = _game;
            bridge.Hud = hud;
            bridge.Menu = _menu;

            ApplyTier(_tier);
            SetStyle(StartStyle);
            _time.Apply();
            _pulse.Recompute();
            _probe.RenderProbe();

            Debug.Log($"[TaxiSim] BKC world generated in {(Time.realtimeSinceStartup - t0) * 1000f:0} ms: " +
                      $"{Map.Graph.Nodes.Count} locations, {Map.Graph.Edges.Count} roads, {ctx.Towers.BuildingCount} buildings, " +
                      $"{ctx.Landmarks.Count} landmarks, {signals.Count} signalised junctions, {_traffic.Agents.Count} vehicles, " +
                      $"{pedestrians.Count} walkers + {life.People} standing people, {life.Stalls} stalls, {life.Shelters} bus shelters, " +
                      $"{life.Hoardings} hoardings, {life.RoofBoards} rooftop billboards, {life.WallAds} wall ads, graphics tier {_tier}, real textures: {_textureReport}, " +
                      $"sound: {ProceduralAudio.ClipNames.Length} clips ({_sound.Overrides} real recordings). Clutter: {clutter.ParkedCars} parked cars, " +
                      $"{clutter.Scooters} parked scooters, {clutter.Poles} poles / {clutter.Wires} cables, {clutter.Bins} bin corners, {clutter.Dogs} dogs, " +
                      $"{clutter.Cows} cows, {clutter.Carts} carts, {clutter.Breakers} speed breakers, {clutter.Fins} tower fins.");
        }

        private ReflectionProbe LocalProbe(string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.position = center;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            p.size = size;
            p.boxProjection = true;
            p.importance = 2;
            p.blendDistance = 8f;
            p.resolution = 256;
            p.hdr = true;
            p.farClipPlane = 600f;
            p.clearFlags = ReflectionProbeClearFlags.Skybox;
            return p;
        }

        /// <summary>Auto (realistic day, subtle neon at night) / Neon Mumbai 2077 / Realistic Mumbai.</summary>
        public VisualStyle CurrentStyle => _style;
        public GraphicsTier CurrentTier => _tier;

        public void SetStyle(VisualStyle style)
        {
            _style = style;
            _time.Style = style;
            _overlay.StyleName = style == VisualStyle.Auto ? "Auto (realistic day / neon night)" : style == VisualStyle.Neon ? "Neon Mumbai 2077" : "Realistic Mumbai";
            _time.Apply();
            UpdateNeonProps();
            _probe.RenderProbe();
        }

        /// <summary>Billboards, searchlights, hologram and air-taxis only appear when the neon look is active.</summary>
        private void UpdateNeonProps()
        {
            bool show = _time.NeonWeight > 0.05f;
            if (_neonRoot != null && _neonRoot.gameObject.activeSelf != show) _neonRoot.gameObject.SetActive(show);
        }

        public void ApplyTier(GraphicsTier tier)
        {
            _tier = tier;
            GraphicsQuality.Apply(tier, _cam, _probe, _post);
            _lamps.Build(Context.Roads, GraphicsQuality.StreetLightCount(tier));
            _overlay.Tier = tier;
        }

        private void Update()
        {
            UpdateNeonProps();
            if (_menu != null && _menu.IsOpen) return; // menus own the keyboard

            // Time of day
            if (InputCompat.Pressed(TsKey.T)) _time.TimeLapse = !_time.TimeLapse;
            if (InputCompat.Pressed(TsKey.LeftBracket)) _time.Hour = Mathf.Repeat(_time.Hour - 0.5f, 24f);
            if (InputCompat.Pressed(TsKey.RightBracket)) _time.Hour = Mathf.Repeat(_time.Hour + 0.5f, 24f);
            if (InputCompat.Pressed(TsKey.Alpha1)) _time.Hour = 6.4f;
            if (InputCompat.Pressed(TsKey.Alpha2)) _time.Hour = 12.5f;
            if (InputCompat.Pressed(TsKey.Alpha3)) _time.Hour = 18.1f;
            if (InputCompat.Pressed(TsKey.Alpha4)) _time.Hour = 21.5f;

            // Weather and looks
            if (InputCompat.Pressed(TsKey.M)) _weather.Cycle();
            if (InputCompat.Pressed(TsKey.Y)) _festivals.Cycle();
            if (InputCompat.Pressed(TsKey.B)) _post.Miniature = !_post.Miniature;
            if (InputCompat.Pressed(TsKey.V)) SetStyle((VisualStyle)(((int)_style + 1) % 3));
            if (InputCompat.Pressed(TsKey.G) && !_director.Active) _rig.ToggleStreetCam();
            if (InputCompat.Pressed(TsKey.F1)) ApplyTier(GraphicsTier.Low);
            if (InputCompat.Pressed(TsKey.F2)) ApplyTier(GraphicsTier.Medium);
            if (InputCompat.Pressed(TsKey.F3)) ApplyTier(GraphicsTier.High);
            if (InputCompat.Pressed(TsKey.F4)) ApplyTier(GraphicsTier.Ultra);
            if (InputCompat.Pressed(TsKey.F5)) ApplyTier(GraphicsTier.Cinematic);

            // Overlays and route demo
            bool shift = InputCompat.Held(TsKey.LeftShift);
            if (InputCompat.Pressed(TsKey.L)) _overlay.ShowLabels = !_overlay.ShowLabels;
            if (InputCompat.Pressed(TsKey.U)) _overlay.HideAll = !_overlay.HideAll;
            if (InputCompat.Pressed(TsKey.I)) _overlay.ShowHelp = !_overlay.ShowHelp;
            bool shift_ = _game != null && _game.ShiftLoaded; // P / O belong to the dispatch game during a shift
            if (InputCompat.Pressed(TsKey.P) && !shift_)
            {
                if (shift) _routes.CloseRoadOnRoute();
                else _routes.ShowRandom();
            }
            if (InputCompat.Pressed(TsKey.O) && !shift_)
            {
                if (shift) _routes.ReopenAll();
                else { _pulse.HeatOverlay = !_pulse.HeatOverlay; _pulse.Recompute(); }
            }
            if (InputCompat.Pressed(TsKey.F9)) TakeScreenshot();
        }

        private void TakeScreenshot()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "Screenshots");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"BKC_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                ScreenCapture.CaptureScreenshot(file, 2);
                Debug.Log($"[TaxiSim] Screenshot saved: {file}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TaxiSim] Screenshot failed: {e.Message}");
            }
        }

        private void DisableOtherCamerasAndSuns()
        {
            foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);
        }
    }
}
