using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TaxiSim.World
{
    public enum GraphicsTier
    {
        Low,
        Medium,
        High,
        Ultra,
        Cinematic
    }

    /// <summary>
    /// Runtime graphics presets (F1–F5). Adjusts the active URP asset (shadows, MSAA, render scale),
    /// SSAO, camera anti-aliasing, the number of real street lights and the reflection probe resolution.
    /// Note: in the Editor, changes to the URP asset persist after Play mode (it is an asset).
    /// </summary>
    public static class GraphicsQuality
    {
        public const string PrefKey = "TaxiSim.GraphicsTier";

        public static GraphicsTier Load() => (GraphicsTier)PlayerPrefs.GetInt(PrefKey, (int)GraphicsTier.Ultra);

        public static int StreetLightCount(GraphicsTier t) => t == GraphicsTier.Low ? 0 : t == GraphicsTier.Medium ? 24 : t == GraphicsTier.High ? 64 : 120;
        public static int TrafficCount(GraphicsTier t) => t == GraphicsTier.Low ? 60 : t == GraphicsTier.Medium ? 110 : t == GraphicsTier.High ? 170 : 240;
        public static int PedestrianCount(GraphicsTier t) => t == GraphicsTier.Low ? 60 : t == GraphicsTier.Medium ? 140 : t == GraphicsTier.High ? 260 : 400;
        public static int AirTaxiCount(GraphicsTier t) => t == GraphicsTier.Low ? 0 : t == GraphicsTier.Medium ? 4 : t == GraphicsTier.High ? 8 : 14;
        public static bool LocalReflectionProbes(GraphicsTier t) => t >= GraphicsTier.Ultra;
        public static float Density(GraphicsTier t) => t == GraphicsTier.Low ? 0.5f : t == GraphicsTier.Medium ? 0.75f : 1f;

        public static void Apply(GraphicsTier tier, Camera cam, ReflectionProbe probe, PostFxRig post)
        {
            PlayerPrefs.SetInt(PrefKey, (int)tier);
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.shadowDistance = tier switch { GraphicsTier.Low => 180f, GraphicsTier.Medium => 280f, GraphicsTier.High => 420f, _ => 600f };
                urp.shadowCascadeCount = tier == GraphicsTier.Low ? 1 : tier == GraphicsTier.Medium ? 2 : 4;
                // Cinematic uses Temporal AA (needs MSAA off); the other tiers use MSAA + SMAA
                urp.msaaSampleCount = tier == GraphicsTier.Low || tier == GraphicsTier.Cinematic ? 1 : tier == GraphicsTier.Medium ? 2 : 4;
                urp.renderScale = tier == GraphicsTier.Low ? 0.85f : tier == GraphicsTier.Cinematic ? 1.5f : 1f;
                urp.supportsHDR = true;
                urp.supportsCameraDepthTexture = true;
                SetPrivate(urp, "m_MainLightShadowmapResolution", tier == GraphicsTier.Low ? 1024 : tier == GraphicsTier.Medium ? 2048 : 4096);
                SetPrivate(urp, "m_SoftShadowsSupported", tier != GraphicsTier.Low);
                SetPrivate(urp, "m_SoftShadowQuality", tier >= GraphicsTier.Ultra ? 3 : 2);
                SetSsao(urp, tier >= GraphicsTier.High);
            }

            if (cam != null)
            {
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.requiresDepthTexture = true;
                data.renderShadows = true;
                data.antialiasing = tier == GraphicsTier.Low ? AntialiasingMode.FastApproximateAntialiasing
                    : tier == GraphicsTier.Cinematic ? AntialiasingMode.TemporalAntiAliasing
                    : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = tier >= GraphicsTier.High ? AntialiasingQuality.High : AntialiasingQuality.Medium;
                cam.allowHDR = true;
                cam.allowMSAA = tier != GraphicsTier.Low && tier != GraphicsTier.Cinematic;
            }

            if (probe != null) probe.resolution = tier >= GraphicsTier.Ultra ? 512 : tier == GraphicsTier.High ? 256 : 128;
            if (post != null) post.SetCinematicExtras(tier >= GraphicsTier.High);
            if (post != null) post.SetMotionBlur(tier >= GraphicsTier.Ultra);
            QualitySettings.lodBias = tier >= GraphicsTier.High ? 2f : 1f;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            try
            {
                FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f == null) return;
                object v = f.FieldType.IsEnum ? Enum.ToObject(f.FieldType, value) : Convert.ChangeType(value, f.FieldType);
                f.SetValue(target, v);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TaxiSim] Could not set URP setting {field}: {e.Message}");
            }
        }

        /// <summary>Turns the Screen Space Ambient Occlusion renderer feature on/off if the renderer has one.</summary>
        private static void SetSsao(UniversalRenderPipelineAsset urp, bool enabled)
        {
            try
            {
                FieldInfo listField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
                if (!(listField?.GetValue(urp) is ScriptableRendererData[] list)) return;
                foreach (ScriptableRendererData data in list)
                {
                    if (data == null) continue;
                    foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                        if (feature != null && feature.GetType().Name.Contains("AmbientOcclusion")) feature.SetActive(enabled);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TaxiSim] Could not toggle SSAO: {e.Message}");
            }
        }
    }
}
