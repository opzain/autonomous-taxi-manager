using System;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>Shader references. Filled in by the editor setup (so builds include them) or found by name at runtime.</summary>
    [Serializable]
    public sealed class ShaderSet
    {
        public Shader tower;
        public Shader road;
        public Shader surface;
        public Shader pbr;
        public Shader water;
        public Shader flowLine;
        public Shader holo;
        public Shader particle;
        public Shader sky;
        public Shader holoAd;
        public Shader volumeBeam;
        public Shader foliage;
        public Shader billboard;
        public Shader character;
        public Shader hubFloor;

        public void ResolveMissing()
        {
            tower = tower ? tower : Shader.Find("TaxiSim/CityTower");
            road = road ? road : Shader.Find("TaxiSim/Road");
            surface = surface ? surface : Shader.Find("TaxiSim/Surface");
            pbr = pbr ? pbr : Shader.Find("TaxiSim/PBR");
            water = water ? water : Shader.Find("TaxiSim/Water");
            flowLine = flowLine ? flowLine : Shader.Find("TaxiSim/FlowLine");
            holo = holo ? holo : Shader.Find("TaxiSim/Holo");
            particle = particle ? particle : Shader.Find("TaxiSim/Particle");
            sky = sky ? sky : Shader.Find("TaxiSim/Sky");
            holoAd = holoAd ? holoAd : Shader.Find("TaxiSim/HoloAd");
            volumeBeam = volumeBeam ? volumeBeam : Shader.Find("TaxiSim/VolumeBeam");
            foliage = foliage ? foliage : Shader.Find("TaxiSim/Foliage");
            billboard = billboard ? billboard : Shader.Find("TaxiSim/Billboard");
            character = character ? character : Shader.Find("TaxiSim/Character");
            hubFloor = hubFloor ? hubFloor : Shader.Find("TaxiSim/FleetHubFloor");
        }
    }

    /// <summary>
    /// Creates every material of the city once. If a custom shader is missing or fails to compile
    /// on the user's GPU, it falls back to URP/Lit (or URP/Unlit) with a matching colour, so the
    /// world still renders instead of turning pink.
    /// </summary>
    public sealed class MaterialLibrary
    {
        public Material Tower, Road, Ground, Grass, Paving, TrackBed, Sand;
        public Material Water, Fountain;
        public Material Concrete, Metal, VehiclePaint, Glass, VehicleLights, LampHeads, Warning, NeonCables, LedScreen;
        public Material Foliage, Bark, Leaves;
        public Material RouteCyan, RouteOrange, RouteMagenta;
        public Material HoloCyan, HoloGreen, HoloRed, HoloGold;
        public Material Rain, Spray, Sparkle;
        public Material Sky;
        // Neon Mumbai 2077
        public Material HoloAd, NeonTubes, LampCones, Searchlight, ProjectorBeam;
        // v0.7 street life
        public Material Billboard, Character, CharacterIdle, StallBulbs, FleetLed, HubFloor, FestiveLights, Fireworks;
        public Material SignalRedOn, SignalRedOff, SignalAmberOn, SignalAmberOff, SignalGreenOn, SignalGreenOff;

        private readonly ShaderSet _shaders;

        public MaterialLibrary(ShaderSet shaders)
        {
            _shaders = shaders;
            _shaders.ResolveMissing();

            Tower = Make(_shaders.tower, "M_Tower", new Color(0.6f, 0.62f, 0.65f));

            Road = Make(_shaders.road, "M_Road", new Color(0.12f, 0.12f, 0.13f));

            Ground = Make(_shaders.surface, "M_Ground", new Color(0.33f, 0.32f, 0.29f));
            SetColor(Ground, "_ColorA", new Color(0.38f, 0.36f, 0.31f));
            SetColor(Ground, "_ColorB", new Color(0.29f, 0.30f, 0.27f));
            SetFloat(Ground, "_NoiseScale", 0.06f);
            SetFloat(Ground, "_BankBlend", 1f);

            Grass = Make(_shaders.surface, "M_Grass", new Color(0.25f, 0.38f, 0.16f));
            SetColor(Grass, "_ColorA", new Color(0.24f, 0.40f, 0.14f));
            SetColor(Grass, "_ColorB", new Color(0.33f, 0.42f, 0.17f));
            SetFloat(Grass, "_NoiseScale", 0.2f);
            SetFloat(Grass, "_GrassDetail", 1f);
            SetFloat(Grass, "_Smoothness", 0.1f);

            Paving = Make(_shaders.surface, "M_Paving", new Color(0.55f, 0.53f, 0.5f));
            SetColor(Paving, "_ColorA", new Color(0.58f, 0.55f, 0.51f));
            SetColor(Paving, "_ColorB", new Color(0.47f, 0.46f, 0.44f));
            SetFloat(Paving, "_NoiseScale", 0.5f);
            SetFloat(Paving, "_TileSize", 0.22f);
            SetFloat(Paving, "_Smoothness", 0.25f);

            TrackBed = Make(_shaders.surface, "M_TrackBed", new Color(0.3f, 0.28f, 0.26f));
            SetColor(TrackBed, "_ColorA", new Color(0.33f, 0.30f, 0.27f));
            SetColor(TrackBed, "_ColorB", new Color(0.22f, 0.21f, 0.2f));
            SetFloat(TrackBed, "_NoiseScale", 3f);

            Sand = Make(_shaders.surface, "M_Sand", new Color(0.62f, 0.55f, 0.43f));
            SetColor(Sand, "_ColorA", new Color(0.66f, 0.58f, 0.45f));
            SetColor(Sand, "_ColorB", new Color(0.55f, 0.49f, 0.39f));
            SetFloat(Sand, "_NoiseScale", 0.3f);

            Water = Make(_shaders.water, "M_Water", new Color(0.05f, 0.15f, 0.18f, 0.85f), transparent: true);
            Fountain = Make(_shaders.water, "M_Fountain", new Color(0.1f, 0.35f, 0.45f, 0.7f), transparent: true);
            SetColor(Fountain, "_ShallowColor", new Color(0.25f, 0.65f, 0.75f, 0.55f));
            SetColor(Fountain, "_DeepColor", new Color(0.05f, 0.25f, 0.42f, 0.9f));
            SetFloat(Fountain, "_WaveAmplitude", 0.01f);
            SetFloat(Fountain, "_DepthFade", 0.4f);

            Concrete = Pbr("M_Concrete", new Color(0.62f, 0.61f, 0.58f), 0f, 0.25f);
            Metal = Pbr("M_Metal", new Color(0.55f, 0.57f, 0.6f), 0.85f, 0.6f);
            VehiclePaint = Pbr("M_VehiclePaint", Color.white, 0.35f, 0.82f);
            Glass = Pbr("M_Glass", new Color(0.04f, 0.05f, 0.06f), 0.4f, 0.95f, useVertexColor: false);
            VehicleLights = Pbr("M_VehicleLights", Color.black, 0f, 0.5f, emission: Color.white * 3.5f, nightEmission: 0.7f);
            LampHeads = Pbr("M_LampHeads", new Color(0.15f, 0.15f, 0.15f), 0f, 0.5f, emission: Color.white * 6f, nightEmission: 1f);
            Warning = Pbr("M_WarningLights", Color.black, 0f, 0.5f, emission: new Color(4f, 0.15f, 0.1f), nightEmission: 0.6f, useVertexColor: false);
            SetFloat(Warning, "_BlinkRate", 0.8f);
            NeonCables = Pbr("M_NeonCables", new Color(0.8f, 0.82f, 0.85f), 0.6f, 0.6f, emission: new Color(0.9f, 1.2f, 2.6f), nightEmission: 1f, useVertexColor: false);
            LedScreen = Pbr("M_LedScreen", Color.black, 0f, 0.9f, emission: Color.white * 2.5f, nightEmission: 0.5f);

            Foliage = Pbr("M_Foliage", Color.white, 0f, 0.18f);
            SetFloat(Foliage, "_Wind", 0.05f);
            Bark = Pbr("M_Bark", Color.white, 0f, 0.18f);
            Leaves = Make(_shaders.foliage, "M_Leaves", new Color(0.2f, 0.36f, 0.13f));
            if (Leaves.shader != _shaders.foliage) { Leaves = Foliage; } // fallback: old blob foliage material

            RouteCyan = Unlit(_shaders.flowLine, "M_RouteCyan", new Color(0.25f, 1.8f, 3.2f));
            RouteOrange = Unlit(_shaders.flowLine, "M_RouteOrange", new Color(3.2f, 1.2f, 0.2f));
            RouteMagenta = Unlit(_shaders.flowLine, "M_RouteMagenta", new Color(2.6f, 0.4f, 2.4f));

            HoloCyan = Unlit(_shaders.holo, "M_HoloCyan", new Color(0.3f, 1.6f, 3.0f));
            HoloGreen = Unlit(_shaders.holo, "M_HoloGreen", new Color(0.3f, 2.8f, 0.9f));
            HoloRed = Unlit(_shaders.holo, "M_HoloRed", new Color(3.0f, 0.35f, 0.35f));
            HoloGold = Unlit(_shaders.holo, "M_HoloGold", new Color(3.0f, 2.0f, 0.5f));

            Rain = Unlit(_shaders.particle, "M_Rain", new Color(0.75f, 0.82f, 0.95f, 0.35f), transparent: true);
            Spray = Unlit(_shaders.particle, "M_Spray", new Color(0.85f, 0.95f, 1f, 0.5f), transparent: true);
            SetFloat(Spray, "_Shape", 1f);
            Sparkle = Unlit(_shaders.particle, "M_Sparkle", new Color(3f, 2.2f, 1.2f, 1f), transparent: true);
            SetFloat(Sparkle, "_Shape", 1f);
            SetFloat(Sparkle, "_DstBlend", 1f); // additive

            HoloAd = Make(_shaders.holoAd, "M_HoloAd", new Color(1f, 0.3f, 0.8f), transparent: true);
            NeonTubes = Pbr("M_NeonTubes", Color.black, 0f, 0.6f, emission: Color.white * 7f, nightEmission: 0.9f);
            LampCones = Make(_shaders.volumeBeam, "M_LampCones", new Color(1f, 0.8f, 0.5f), transparent: true);
            SetFloat(LampCones, "_Intensity", 0.35f);
            SetFloat(LampCones, "_SourceAtTop", 1f);
            Searchlight = Make(_shaders.volumeBeam, "M_Searchlight", new Color(0.8f, 0.9f, 1f), transparent: true);
            SetColor(Searchlight, "_Color", new Color(0.7f, 0.85f, 1f));
            SetFloat(Searchlight, "_Intensity", 0.45f);
            SetFloat(Searchlight, "_SourceAtTop", 0f);
            SetFloat(Searchlight, "_SoftFade", 3f);
            SetFloat(Searchlight, "_StyleWeight", 1f);
            ProjectorBeam = Make(_shaders.volumeBeam, "M_ProjectorBeam", new Color(0.3f, 1f, 1f), transparent: true);
            SetColor(ProjectorBeam, "_Color", new Color(0.25f, 1.2f, 1.6f));
            SetFloat(ProjectorBeam, "_Intensity", 0.5f);
            SetFloat(ProjectorBeam, "_SourceAtTop", 0f);
            SetFloat(ProjectorBeam, "_DayVisibility", 0.4f);
            SetFloat(ProjectorBeam, "_StyleWeight", 1f);

            // v0.7: hoardings, pedestrians, stall bulbs, fleet-hub LED strips, traffic-signal lamps
            Billboard = Make(_shaders.billboard, "M_Billboard", new Color(0.7f, 0.5f, 0.3f));
            Character = Make(_shaders.character, "M_Character", new Color(0.5f, 0.45f, 0.4f));
            CharacterIdle = new Material(Character) { name = "M_CharacterIdle" };
            SetFloat(CharacterIdle, "_Walk", 0f);
            FestiveLights = Pbr("M_FestiveLights", new Color(0.2f, 0.2f, 0.2f), 0f, 0.6f, emission: Color.white * 5f, nightEmission: 0.85f);
            Fireworks = Unlit(_shaders.particle, "M_Fireworks", new Color(4f, 4f, 4f, 1f), transparent: true);
            SetFloat(Fireworks, "_Shape", 1f);
            SetFloat(Fireworks, "_DstBlend", 1f); // additive
            HubFloor = Make(_shaders.hubFloor, "M_FleetHubFloor", new Color(0.2f, 0.22f, 0.25f));
            StallBulbs = Pbr("M_StallBulbs", new Color(0.9f, 0.85f, 0.7f), 0f, 0.6f, emission: new Color(5f, 3.4f, 1.4f), nightEmission: 1f);
            FleetLed = Pbr("M_FleetLed", Color.black, 0f, 0.8f, emission: new Color(0.4f, 2.6f, 3.4f), nightEmission: 0.55f, useVertexColor: false);
            SignalRedOn = Pbr("M_SignalRedOn", Color.black, 0f, 0.9f, emission: new Color(6f, 0.25f, 0.15f), useVertexColor: false);
            SignalAmberOn = Pbr("M_SignalAmberOn", Color.black, 0f, 0.9f, emission: new Color(6f, 2.8f, 0.2f), useVertexColor: false);
            SignalGreenOn = Pbr("M_SignalGreenOn", Color.black, 0f, 0.9f, emission: new Color(0.2f, 5f, 2.2f), useVertexColor: false);
            SignalRedOff = Pbr("M_SignalRedOff", new Color(0.18f, 0.03f, 0.03f), 0f, 0.9f, useVertexColor: false);
            SignalAmberOff = Pbr("M_SignalAmberOff", new Color(0.18f, 0.11f, 0.02f), 0f, 0.9f, useVertexColor: false);
            SignalGreenOff = Pbr("M_SignalGreenOff", new Color(0.02f, 0.14f, 0.07f), 0f, 0.9f, useVertexColor: false);

            Sky = _shaders.sky != null && _shaders.sky.isSupported ? new Material(_shaders.sky) { name = "M_Sky" } : null;
        }

        /// <summary>Daytime HDRI skybox (only if the team added an HDRI to Resources/TaxiSimTextures/Sky).</summary>
        public Material HdriSky;

        /// <summary>
        /// Applies any real CC0 textures found in Resources/TaxiSimTextures (see docs/13-textures-guide.md).
        /// Returns a short report such as "asphalt, paving, grass, facade, sky". Missing ones stay procedural.
        /// Texture sizes are in world units (1 unit = 10 m).
        /// </summary>
        public string ApplyRealTextures()
        {
            var used = new System.Collections.Generic.List<string>();
            if (ApplySet(Road, TextureLibrary.Find("asphalt", "road"), "_AsphaltTex", "_AsphaltNormal", "_AsphaltRough", "_UseTex", "_TexTiling", 0.4f)) used.Add("asphalt");
            if (ApplySet(Paving, TextureLibrary.Find("paving", "pavement", "sidewalk", "tiles"), "_AlbedoMap", "_NormalMap", "_RoughMap", "_UseTex", "_TexTiling", 0.25f)) used.Add("paving");
            if (ApplySet(Grass, TextureLibrary.Find("grass", "lawn"), "_AlbedoMap", "_NormalMap", "_RoughMap", "_UseTex", "_TexTiling", 0.35f)) used.Add("grass");
            if (ApplySet(Ground, TextureLibrary.Find("concrete", "pavement", "ground", "dirt"), "_AlbedoMap", "_NormalMap", "_RoughMap", "_UseTex", "_TexTiling", 0.5f)) used.Add("ground");
            if (ApplySet(TrackBed, TextureLibrary.Find("gravel", "ballast", "rocks"), "_AlbedoMap", "_NormalMap", "_RoughMap", "_UseTex", "_TexTiling", 0.3f)) used.Add("gravel");
            if (ApplySet(Sand, TextureLibrary.Find("sand", "beach", "mud"), "_AlbedoMap", "_NormalMap", "_RoughMap", "_UseTex", "_TexTiling", 0.5f)) used.Add("sand");
            if (ApplySet(Tower, TextureLibrary.Find("plaster", "stucco", "concrete", "facade", "wall"), "_FacadeTex", "_FacadeNormal", null, "_UseFacadeTex", "_FacadeTiling", 0.35f)) used.Add("facade");

            Cubemap sky = TextureLibrary.FindSky();
            Shader skyboxShader = Shader.Find("Skybox/Cubemap");
            if (sky != null && skyboxShader != null)
            {
                HdriSky = new Material(skyboxShader) { name = "M_HdriSky" };
                HdriSky.SetTexture("_Tex", sky);
                used.Add("HDRI sky");
            }
            return used.Count == 0 ? "none (procedural look)" : string.Join(", ", used);
        }

        private static bool ApplySet(Material m, PbrSet set, string albedo, string normal, string rough, string useFlag, string tilingProp, float tiling)
        {
            if (m == null || !set.Valid || !m.HasProperty(albedo)) return false;
            m.SetTexture(albedo, set.Albedo);
            if (normal != null && set.Normal != null && m.HasProperty(normal)) m.SetTexture(normal, set.Normal);
            if (rough != null && set.Roughness != null && m.HasProperty(rough)) m.SetTexture(rough, set.Roughness);
            SetFloat(m, useFlag, 1f);
            SetFloat(m, tilingProp, tiling);
            return true;
        }

        private Material Pbr(string name, Color baseColor, float metallic, float smoothness, Color emission = default,
                             float nightEmission = 0f, bool useVertexColor = true)
        {
            Material m = Make(_shaders.pbr, name, baseColor);
            SetColor(m, "_BaseColor", baseColor);
            SetFloat(m, "_Metallic", metallic);
            SetFloat(m, "_Smoothness", smoothness);
            SetFloat(m, "_UseVertexColor", useVertexColor ? 1f : 0f);
            SetColor(m, "_EmissionColor", emission);
            SetFloat(m, "_NightEmission", nightEmission);
            if (m.shader != _shaders.pbr && emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                SetColor(m, "_EmissionColor", emission);
            }
            return m;
        }

        private static Material Unlit(Shader shader, string name, Color color, bool transparent = true)
        {
            Material m = Make(shader, name, color, transparent);
            SetColor(m, "_Color", color);
            return m;
        }

        private static Material Make(Shader shader, string name, Color fallbackColor, bool transparent = false)
        {
            Material m;
            if (shader != null && shader.isSupported)
            {
                m = new Material(shader);
            }
            else
            {
                string fallback = transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit";
                Shader fs = Shader.Find(fallback) ?? Shader.Find("Universal Render Pipeline/Lit");
                Debug.LogWarning($"[TaxiSim] Shader for {name} unavailable, using {fallback}. Check the Console for shader compile errors.");
                m = new Material(fs);
                SetColor(m, "_BaseColor", fallbackColor);
            }
            m.name = name;
            m.enableInstancing = true;
            return m;
        }

        public static void SetColor(Material m, string prop, Color c)
        {
            if (m != null && m.HasProperty(prop)) m.SetColor(prop, c);
        }

        public static void SetFloat(Material m, string prop, float v)
        {
            if (m != null && m.HasProperty(prop)) m.SetFloat(prop, v);
        }
    }
}
