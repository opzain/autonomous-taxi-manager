using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TaxiSim.World
{
    /// <summary>
    /// URP post-processing stack built at runtime: HDR bloom with a procedural lens-dirt texture,
    /// ACES tonemapping, colour grading per time of day, teal/magenta split toning for the
    /// Neon Mumbai 2077 look, vignette, film grain, chromatic aberration, camera motion blur,
    /// screen-space lens flares (when the URP version supports them) and tilt-shift depth of field.
    /// </summary>
    public sealed class PostFxRig : MonoBehaviour
    {
        public bool Miniature;
        /// <summary>0..1 strength of the neon grade (set by TimeOfDay from the visual style).</summary>
        public float NeonWeight;

        private Volume _volume;
        private Bloom _bloom;
        private ColorAdjustments _color;
        private WhiteBalance _white;
        private SplitToning _split;
        private ShadowsMidtonesHighlights _smh;
        private Vignette _vignette;
        private FilmGrain _grain;
        private ChromaticAberration _chroma;
        private MotionBlur _motion;
        private DepthOfField _dof;
        private VolumeComponent _lensFlare;
        private bool _cinematicExtras = true;

        public void Init()
        {
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "TaxiSim_PostFX";
            _volume.sharedProfile = profile;

            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.value = 1.0f;
            _bloom.intensity.value = 0.85f;
            _bloom.scatter.value = 0.72f;
            _bloom.highQualityFiltering.value = true;
            _bloom.dirtTexture.value = CreateLensDirt(256);
            _bloom.dirtIntensity.value = 0f;

            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;

            _color = profile.Add<ColorAdjustments>(true);
            _color.contrast.value = 12f;
            _color.saturation.value = 8f;
            _color.postExposure.value = 0f;

            _white = profile.Add<WhiteBalance>(true);
            _white.temperature.value = 0f;

            _split = profile.Add<SplitToning>(true);
            _split.shadows.value = new Color(0.25f, 0.55f, 0.75f);
            _split.highlights.value = new Color(1f, 0.45f, 0.65f);
            _split.balance.value = -15f;
            _split.active = false;

            // v0.10 filmic grading: cool shadows / warm highlights, retuned every frame by the time of day
            _smh = profile.Add<ShadowsMidtonesHighlights>(true);

            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.value = 0.22f;
            _vignette.smoothness.value = 0.45f;

            _grain = profile.Add<FilmGrain>(true);
            _grain.type.value = FilmGrainLookup.Thin1;
            _grain.intensity.value = 0.12f;

            _chroma = profile.Add<ChromaticAberration>(true);
            _chroma.intensity.value = 0.04f;

            _motion = profile.Add<MotionBlur>(true);
            _motion.mode.value = MotionBlurMode.CameraOnly;
            _motion.quality.value = MotionBlurQuality.High;
            _motion.intensity.value = 0.25f;
            _motion.active = false;

            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.value = DepthOfFieldMode.Off;
            _dof.focalLength.value = 150f;
            _dof.aperture.value = 3.2f;

            _lensFlare = TryAddLensFlare(profile);
        }

        /// <summary>Screen Space Lens Flare exists in newer URP versions only; added via reflection so older ones still compile.</summary>
        private static VolumeComponent TryAddLensFlare(VolumeProfile profile)
        {
            try
            {
                Type t = typeof(Bloom).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceLensFlare");
                if (t == null) return null;
                VolumeComponent c = profile.Add(t, true);
                SetParameter(c, "intensity", 0.6f);
                return c;
            }
            catch (Exception e)
            {
                Debug.Log($"[TaxiSim] Screen-space lens flare not available: {e.Message}");
                return null;
            }
        }

        private static void SetParameter(VolumeComponent component, string field, float value)
        {
            FieldInfo f = component.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public);
            object parameter = f?.GetValue(component);
            PropertyInfo valueProp = parameter?.GetType().GetProperty("value");
            if (valueProp != null && valueProp.PropertyType == typeof(float)) valueProp.SetValue(parameter, value);
        }

        /// <summary>Procedural lens dirt: soft smudges and a few hexagonal bokeh blobs.</summary>
        private static Texture2D CreateLensDirt(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TaxiSim_LensDirt", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            var rng = new TaxiSim.Core.Pcg32(77UL);
            var blobs = new Vector4[40];
            for (int i = 0; i < blobs.Length; i++)
                blobs[i] = new Vector4(rng.NextFloat(), rng.NextFloat(), rng.Range(0.01f, 0.08f), rng.Range(0.2f, 0.8f));
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float smudge = Mathf.PerlinNoise(u * 6f, v * 6f) * 0.25f + Mathf.PerlinNoise(u * 21f + 3f, v * 21f) * 0.1f;
                    float b = 0f;
                    foreach (Vector4 blob in blobs)
                    {
                        float d = Vector2.Distance(new Vector2(u, v), new Vector2(blob.x, blob.y)) / blob.z;
                        b += Mathf.Clamp01(1f - d * d) * blob.w;
                    }
                    float edge = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 1.4f);
                    float a = Mathf.Clamp01((smudge + b) * (0.4f + edge));
                    px[y * size + x] = new Color(a, a * 0.96f, a * 0.9f, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public void SetCinematicExtras(bool on)
        {
            _cinematicExtras = on;
            if (_grain != null) _grain.active = on;
            if (_chroma != null) _chroma.active = on;
        }

        public void SetMotionBlur(bool on)
        {
            if (_motion != null) _motion.active = on;
        }

        public void SetTimeOfDay(float night, float golden, WeatherMode weather)
        {
            if (_color == null) return;
            float exposure = Mathf.Lerp(0f, 0.45f, night) + (weather == WeatherMode.Monsoon ? 0.25f : 0f);
            _color.postExposure.value = exposure;
            float nw = NeonWeight;
            float neonNight = night * nw;

            _color.saturation.value = weather == WeatherMode.Monsoon && nw < 0.5f ? -8f : Mathf.Lerp(8f, 14f, golden) + neonNight * 10f;
            _color.contrast.value = 12f + neonNight * 8f;
            _white.temperature.value = golden * 16f - night * Mathf.Lerp(10f, 16f, nw) + (weather == WeatherMode.Monsoon ? -8f : 0f);
            _split.active = nw > 0.05f;
            _split.balance.value = Mathf.Lerp(-60f, -15f, nw);
            _bloom.intensity.value = Mathf.Lerp(0.75f, Mathf.Lerp(1.25f, 1.6f, nw), night);
            _bloom.scatter.value = Mathf.Lerp(0.72f, 0.8f, neonNight);
            _bloom.threshold.value = Mathf.Lerp(1.0f, 0.9f, neonNight);
            _bloom.dirtIntensity.value = 2f * neonNight;
            _vignette.intensity.value = 0.2f + 0.08f * neonNight;
            _chroma.intensity.value = 0.03f + 0.07f * neonNight;

            if (_smh != null)
            {
                // Day: slightly cool shadows, neutral-warm highlights. Golden hour: amber highlights, teal shadows.
                // Blue hour / night: deep blue shadows, sodium-warm highlights (realistic Mumbai streets).
                float blue = Mathf.Clamp01(1f - Mathf.Abs(night - 0.55f) / 0.35f) * (1f - nw);
                Vector4 shadows = Vector4.Lerp(new Vector4(0.97f, 1f, 1.04f, 0f), new Vector4(0.92f, 1f, 1.08f, 0f), golden);
                shadows = Vector4.Lerp(shadows, new Vector4(0.86f, 0.95f, 1.18f, -0.02f), Mathf.Max(night * 0.8f, blue));
                Vector4 highlights = Vector4.Lerp(new Vector4(1.02f, 1f, 0.97f, 0f), new Vector4(1.12f, 1.01f, 0.86f, 0.02f), golden);
                highlights = Vector4.Lerp(highlights, new Vector4(1.08f, 0.99f, 0.88f, 0f), night * (1f - nw));
                if (weather == WeatherMode.Monsoon) { shadows = Vector4.Lerp(shadows, new Vector4(0.95f, 1f, 1.03f, -0.01f), 0.6f); highlights = Vector4.Lerp(highlights, Vector4.one, 0.5f); }
                _smh.shadows.value = shadows;
                _smh.highlights.value = highlights;
                _smh.midtones.value = new Vector4(1f, 1f, 1f, golden * 0.03f);
            }
        }

        /// <summary>Called every frame by the camera rig so the tilt-shift focus stays on the pivot.</summary>
        public void SetFocus(float distance)
        {
            if (_dof == null) return;
            if (_cineLens)
            {
                _dof.mode.value = DepthOfFieldMode.Bokeh;
                _dof.focusDistance.value = _cineFocus;
                _dof.aperture.value = _cineAperture;
                _dof.focalLength.value = _cineFocal;
                return;
            }
            _dof.mode.value = Miniature ? DepthOfFieldMode.Bokeh : DepthOfFieldMode.Off;
            _dof.focusDistance.value = Mathf.Max(1f, distance);
            _dof.aperture.value = Mathf.Lerp(2.0f, 5.6f, Mathf.InverseLerp(20f, 300f, distance));
        }

        public bool CinematicExtras => _cinematicExtras;
        public bool MotionBlurOn => _motion != null && _motion.active;

        private bool _cineLens;
        private float _cineFocus = 10f, _cineAperture = 2.8f, _cineFocal = 50f;

        /// <summary>Cinematic director lens: shallow depth of field focused on the shot's subject.</summary>
        public void SetCinematicLens(bool on, float focusDistance = 10f, float aperture = 2.8f, float focalLength = 50f)
        {
            _cineLens = on;
            _cineFocus = Mathf.Max(0.3f, focusDistance);
            _cineAperture = aperture;
            _cineFocal = focalLength;
        }
    }
}
