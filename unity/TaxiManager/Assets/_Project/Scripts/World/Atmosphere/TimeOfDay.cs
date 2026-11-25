using System.Collections.Generic;
using UnityEngine;

namespace TaxiSim.World
{
    public enum VisualStyle
    {
        Auto,       // realistic GTA-style day, subtle neon after dark (default)
        Neon,       // Neon Mumbai 2077 at all times
        Realistic   // never neon
    }

    /// <summary>
    /// Mumbai day/night cycle. Moves the sun (east -> south -> west, ~19°N) and moon, and drives the
    /// sky shader, ambient light, coastal haze/fog, window lights (_TS_Night), street lights,
    /// vehicle headlights, post-processing white balance and reflection-probe refreshes.
    /// </summary>
    public sealed class TimeOfDay : MonoBehaviour
    {
        [Range(0f, 24f)] public float Hour = 17.4f;
        public bool TimeLapse = true;
        public float HoursPerSecond = 0.012f;

        public Light SkyLight;
        public Material SkyMaterial;
        public ReflectionProbe Probe;
        public List<ReflectionProbe> LocalProbes = new List<ReflectionProbe>();
        /// <summary>V key: Auto = realistic GTA-style day, subtle neon at night; Neon = always; Realistic = never.</summary>
        public VisualStyle Style = VisualStyle.Auto;
        /// <summary>0..1 strength of the Neon Mumbai 2077 look right now (depends on Style and time of day).</summary>
        public float NeonWeight { get; private set; }
        /// <summary>Optional photographed HDRI sky used in daytime (TextureLibrary).</summary>
        public Material HdriSkyMaterial;
        private bool _usingHdri;
        private bool _forceProbe;
        public PostFxRig Post;
        public StreetLights Lamps;
        public TrafficAmbient Traffic;
        public WeatherSystem Weather;

        public float Night { get; private set; }
        public float SunElevation { get; private set; }

        private float _lastProbeHour = -10f;
        private WeatherMode _lastProbeWeather;

        private static readonly int NightId = Shader.PropertyToID("_TS_Night");
        private static readonly int SunDirId = Shader.PropertyToID("_TS_SunDir");
        private static readonly int SunColorId = Shader.PropertyToID("_TS_SunColor");
        private static readonly int MoonDirId = Shader.PropertyToID("_TS_MoonDir");
        private static readonly int StyleId = Shader.PropertyToID("_TS_Style");
        private static readonly int FogDensityId = Shader.PropertyToID("_TS_FogDensity");
        private static readonly int FogHeightId = Shader.PropertyToID("_TS_FogHeight");
        private static readonly int FogColorId = Shader.PropertyToID("_TS_FogColor");
        private static readonly int NeonFogId = Shader.PropertyToID("_TS_NeonFog");
        private static readonly int SkyZenithId = Shader.PropertyToID("_TS_SkyZenith");
        private static readonly int SkyHorizonId = Shader.PropertyToID("_TS_SkyHorizon");

        // Elevation keys (degrees) and matching palettes: noon, golden hour, sunset, blue hour, night
        private static readonly float[] Elev = { 30f, 8f, 0f, -6f, -16f };
        private static readonly Color[] LightCol = { new Color(1f, 0.96f, 0.9f), new Color(1f, 0.78f, 0.52f), new Color(1f, 0.5f, 0.25f), new Color(0.5f, 0.55f, 0.85f), new Color(0.55f, 0.65f, 1f) };
        private static readonly Color[] Zenith = { new Color(0.2f, 0.42f, 0.78f), new Color(0.25f, 0.4f, 0.7f), new Color(0.18f, 0.24f, 0.45f), new Color(0.05f, 0.08f, 0.2f), new Color(0.01f, 0.015f, 0.04f) };
        private static readonly Color[] Horizon = { new Color(0.68f, 0.78f, 0.88f), new Color(0.98f, 0.72f, 0.48f), new Color(0.95f, 0.45f, 0.25f), new Color(0.25f, 0.22f, 0.35f), new Color(0.1f, 0.07f, 0.08f) };
        private static readonly Color[] Haze = { new Color(0.86f, 0.82f, 0.74f), new Color(1f, 0.62f, 0.38f), new Color(0.9f, 0.42f, 0.3f), new Color(0.35f, 0.25f, 0.33f), new Color(0.22f, 0.13f, 0.08f) };

        public string ClockText
        {
            get
            {
                int h = Mathf.FloorToInt(Hour) % 24;
                int m = Mathf.FloorToInt((Hour - Mathf.Floor(Hour)) * 60f);
                return $"{h:00}:{m:00}";
            }
        }

        private void Update()
        {
            if (TimeLapse) Hour = Mathf.Repeat(Hour + HoursPerSecond * Time.deltaTime, 24f);
            Apply();
        }

        public void Apply()
        {
            float elev = 72f * Mathf.Sin((Hour - 6f) / 12f * Mathf.PI);
            float az = 90f + (Hour - 6f) / 12f * 180f;
            SunElevation = elev;
            Vector3 toSun = Quaternion.Euler(-elev, az, 0f) * Vector3.forward;
            Vector3 toMoon = new Vector3(-toSun.x, Mathf.Abs(toSun.y) * 0.8f + 0.35f, -toSun.z).normalized;

            Night = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-6f, 6f, elev));
            float day = 1f - Night;

            WeatherMode mode = Weather != null ? Weather.Mode : WeatherMode.Clear;
            float weatherLight = Weather != null ? Weather.LightMultiplier : 1f;
            float flash = Weather != null ? Weather.Lightning : 0f;

            // Directional light: sun by day, moon by night
            Color sunCol = Palette(LightCol, elev);
            float sunI = 1.65f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, 18f, elev));
            float moonI = 0.22f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-3f, -12f, elev));
            if (SkyLight != null)
            {
                bool useSun = sunI >= moonI;
                SkyLight.transform.rotation = Quaternion.LookRotation(-(useSun ? toSun : toMoon));
                SkyLight.color = useSun ? sunCol : new Color(0.6f, 0.7f, 1f);
                SkyLight.intensity = (useSun ? sunI : moonI) * weatherLight + flash * 2.5f;
                SkyLight.shadowStrength = useSun ? 0.92f : 0.55f;
            }
            // Haze in-scattering colour: strong and warm near the horizon, weaker overhead, none at night
            float lowSun = 1f - Mathf.InverseLerp(5f, 50f, elev);
            Shader.SetGlobalVector(SunColorId, (Vector4)(sunCol * sunI * Mathf.Lerp(0.25f, 0.65f, lowSun) * weatherLight));

            Color zenith = Palette(Zenith, elev);
            Color horizon = Palette(Horizon, elev);
            Color haze = Palette(Haze, elev);
            if (mode == WeatherMode.Monsoon)
            {
                Color grey = new Color(0.42f, 0.45f, 0.48f) * Mathf.Lerp(0.15f, 1f, day);
                zenith = Color.Lerp(zenith, grey * 0.8f, 0.75f);
                horizon = Color.Lerp(horizon, grey, 0.7f);
                haze = Color.Lerp(haze, grey * 1.1f, 0.7f);
            }
            else if (mode == WeatherMode.Haze)
            {
                horizon = Color.Lerp(horizon, haze, 0.4f);
            }
            NeonWeight = Style == VisualStyle.Neon ? 1f
                       : Style == VisualStyle.Auto ? 0.45f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.95f, Night))
                       : 0f;
            float nw = NeonWeight;
            if (nw > 0f)
            {
                // Night City grade: violet-blue sky, magenta smog glow near the horizon
                zenith = Color.Lerp(zenith, new Color(0.03f, 0.02f, 0.08f), Night * 0.8f * nw);
                horizon = Color.Lerp(horizon, new Color(0.28f, 0.08f, 0.25f), Night * 0.6f * nw);
                haze = Color.Lerp(haze, new Color(0.45f, 0.1f, 0.4f), Night * 0.6f * nw);
            }
            zenith += Color.white * flash * 0.6f;

            if (SkyMaterial != null)
            {
                MaterialLibrary.SetColor(SkyMaterial, "_ZenithColor", zenith);
                MaterialLibrary.SetColor(SkyMaterial, "_HorizonColor", horizon);
                MaterialLibrary.SetColor(SkyMaterial, "_HazeColor", haze);
                MaterialLibrary.SetColor(SkyMaterial, "_GroundColor", horizon * 0.35f);
                MaterialLibrary.SetColor(SkyMaterial, "_SunColor", sunCol * 14f * Mathf.Clamp01(day + 0.3f));
                Color cloud = Color.Lerp(new Color(0.09f, 0.08f, 0.1f) + haze * 0.25f, Color.Lerp(Color.white, sunCol, 0.35f), day);
                if (mode == WeatherMode.Monsoon) cloud *= 0.55f;
                MaterialLibrary.SetColor(SkyMaterial, "_CloudColor", cloud);
                MaterialLibrary.SetFloat(SkyMaterial, "_CloudCover", Weather != null ? Weather.CloudCover : 0.4f);
                MaterialLibrary.SetFloat(SkyMaterial, "_StarIntensity", mode == WeatherMode.Monsoon ? 0f : 2.2f);
                MaterialLibrary.SetFloat(SkyMaterial, "_HazeStrength", mode == WeatherMode.Clear ? 0.5f : 0.75f);
                MaterialLibrary.SetColor(SkyMaterial, "_CityGlow", Color.Lerp(new Color(0.35f, 0.2f, 0.1f) * 0.3f, new Color(0.9f, 0.22f, 0.7f) * 0.4f, nw));
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = zenith * 0.9f + new Color(0.02f, 0.025f, 0.04f) + Color.white * flash * 0.5f;
            RenderSettings.ambientEquatorColor = horizon * 0.55f + new Color(0.03f, 0.025f, 0.02f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.13f) * Mathf.Lerp(0.2f, 1f, day);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(horizon, haze, 0.5f) * 0.9f;
            RenderSettings.fogDensity = 0.0019f * (Weather != null ? Weather.FogMultiplier : 1f) * Mathf.Lerp(1f, 0.75f, nw);
            RenderSettings.ambientSkyColor += new Color(0.05f, 0.02f, 0.08f) * Night * nw;

            // Custom height fog (smog layer hugging the streets) used by all TaxiSim shaders
            float fogBase = Mathf.Lerp(0.0005f, Mathf.Lerp(0.0008f, 0.0022f, Night), nw);
            if (mode == WeatherMode.Monsoon) fogBase *= 1.8f;
            Shader.SetGlobalFloat(StyleId, nw);
            Shader.SetGlobalColor(SkyZenithId, zenith);
            Shader.SetGlobalColor(SkyHorizonId, Color.Lerp(horizon, haze, 0.3f));
            Shader.SetGlobalFloat(FogDensityId, fogBase);
            Shader.SetGlobalFloat(FogHeightId, Mathf.Lerp(12f, 7f, nw));
            Shader.SetGlobalColor(FogColorId, RenderSettings.fogColor);
            Shader.SetGlobalColor(NeonFogId, new Color(0.42f, 0.14f, 0.45f));
            if (Weather != null) Weather.BaselineWetness = 0.55f * nw;

            Shader.SetGlobalFloat(NightId, Night);
            Shader.SetGlobalVector(SunDirId, toSun);
            Shader.SetGlobalVector(MoonDirId, toMoon);

            if (Lamps != null) Lamps.SetLevel(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, Night)));
            if (Traffic != null) Traffic.SetHeadlights(Night > 0.4f);
            if (Post != null) Post.NeonWeight = nw;

            // Daytime: photographed HDRI sky if the team added one; night: the procedural star sky
            bool wantHdri = HdriSkyMaterial != null && Night < 0.35f && mode != WeatherMode.Monsoon;
            if (wantHdri)
            {
                MaterialLibrary.SetFloat(HdriSkyMaterial, "_Exposure", Mathf.Lerp(0.35f, 1.05f, Mathf.InverseLerp(0f, 25f, elev)) * weatherLight);
                MaterialLibrary.SetColor(HdriSkyMaterial, "_Tint", Color.Lerp(new Color(0.5f, 0.42f, 0.36f), new Color(0.5f, 0.5f, 0.5f), Mathf.InverseLerp(4f, 20f, elev)));
            }
            if (wantHdri != _usingHdri)
            {
                _usingHdri = wantHdri;
                RenderSettings.skybox = wantHdri ? HdriSkyMaterial : SkyMaterial;
                _forceProbe = true; // refresh reflections with the new sky
            }
            if (Post != null) Post.SetTimeOfDay(Night, Mathf.Clamp01(1f - Mathf.Abs(elev - 4f) / 12f), mode);

            if (Probe != null && (_forceProbe || Mathf.Abs(Mathf.DeltaAngle(Hour * 15f, _lastProbeHour * 15f)) > 7.5f || mode != _lastProbeWeather))
            {
                _forceProbe = false;
                _lastProbeHour = Hour;
                _lastProbeWeather = mode;
                Probe.RenderProbe();
                foreach (ReflectionProbe p in LocalProbes) if (p != null) p.RenderProbe();
            }
        }

        private static Color Palette(Color[] cols, float elev)
        {
            if (elev >= Elev[0]) return cols[0];
            for (int i = 0; i < Elev.Length - 1; i++)
            {
                if (elev <= Elev[i] && elev >= Elev[i + 1])
                    return Color.Lerp(cols[i + 1], cols[i], Mathf.InverseLerp(Elev[i + 1], Elev[i], elev));
            }
            return cols[cols.Length - 1];
        }
    }
}
