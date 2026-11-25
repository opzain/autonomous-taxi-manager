// Compile-check stand-ins for URP 17 / Core RP / Input System APIs used by TaxiSim (NOT shipped).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering
{
    public abstract class VolumeParameter { public bool overrideState; }
    public class VolumeParameter<T> : VolumeParameter { public virtual T value { get; set; } }
    public class FloatParameter : VolumeParameter<float> { }
    public class MinFloatParameter : VolumeParameter<float> { }
    public class ClampedFloatParameter : VolumeParameter<float> { }
    public class BoolParameter : VolumeParameter<bool> { }
    public class ColorParameter : VolumeParameter<Color> { }
    public class Vector4Parameter : VolumeParameter<Vector4> { }
    public class TextureParameter : VolumeParameter<Texture> { }
    public class VolumeComponent : ScriptableObject { public bool active = true; }
    public class VolumeProfile : ScriptableObject
    {
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
        public VolumeComponent Add(Type type, bool overrides = false) => null;
    }
    public class Volume : MonoBehaviour { public bool isGlobal; public float priority { get; set; } public VolumeProfile sharedProfile; }
}

namespace UnityEngine.Rendering.Universal
{
    public enum TonemappingMode { None, Neutral, ACES }
    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }
    public enum FilmGrainLookup { Thin1, Thin2, Medium1 }
    public sealed class FilmGrainLookupParameter : VolumeParameter<FilmGrainLookup> { }
    public enum MotionBlurMode { CameraOnly, CameraAndObjects }
    public sealed class MotionBlurModeParameter : VolumeParameter<MotionBlurMode> { }
    public enum MotionBlurQuality { Low, Medium, High }
    public sealed class MotionBlurQualityParameter : VolumeParameter<MotionBlurQuality> { }
    public enum DepthOfFieldMode { Off, Gaussian, Bokeh }
    public sealed class DepthOfFieldModeParameter : VolumeParameter<DepthOfFieldMode> { }

    public sealed class Bloom : VolumeComponent { public MinFloatParameter threshold, intensity, dirtIntensity; public ClampedFloatParameter scatter; public BoolParameter highQualityFiltering; public TextureParameter dirtTexture; }
    public sealed class Tonemapping : VolumeComponent { public TonemappingModeParameter mode; }
    public sealed class ColorAdjustments : VolumeComponent { public FloatParameter postExposure; public ClampedFloatParameter contrast, saturation, hueShift; public ColorParameter colorFilter; }
    public sealed class WhiteBalance : VolumeComponent { public ClampedFloatParameter temperature, tint; }
    public sealed class ShadowsMidtonesHighlights : VolumeComponent { public Vector4Parameter shadows, midtones, highlights; }
    public sealed class SplitToning : VolumeComponent { public ColorParameter shadows, highlights; public ClampedFloatParameter balance; }
    public sealed class Vignette : VolumeComponent { public ColorParameter color; public ClampedFloatParameter intensity, smoothness; }
    public sealed class FilmGrain : VolumeComponent { public FilmGrainLookupParameter type; public ClampedFloatParameter intensity; }
    public sealed class ChromaticAberration : VolumeComponent { public ClampedFloatParameter intensity; }
    public sealed class MotionBlur : VolumeComponent { public MotionBlurModeParameter mode; public MotionBlurQualityParameter quality; public ClampedFloatParameter intensity; }
    public sealed class DepthOfField : VolumeComponent { public DepthOfFieldModeParameter mode; public MinFloatParameter focusDistance; public ClampedFloatParameter aperture, focalLength; }

    public abstract class ScriptableRendererFeature : ScriptableObject { public void SetActive(bool active) { } }
    public abstract class ScriptableRendererData : ScriptableObject { public List<ScriptableRendererFeature> rendererFeatures => null; }
    public class UniversalRendererData : ScriptableRendererData { }
    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public float shadowDistance { get; set; }
        public int shadowCascadeCount { get; set; }
        public int msaaSampleCount { get; set; }
        public float renderScale { get; set; }
        public bool supportsHDR { get; set; }
        public bool supportsCameraDepthTexture { get; set; }
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => null;
        public override RenderPipeline CreatePipeline() => null;
    }
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
    public enum AntialiasingQuality { Low, Medium, High }
    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderPostProcessing { get; set; }
        public AntialiasingMode antialiasing { get; set; }
        public AntialiasingQuality antialiasingQuality { get; set; }
        public bool requiresDepthTexture { get; set; }
        public bool renderShadows { get; set; }
    }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null; }
}

namespace UnityEngine.InputSystem
{
    public enum Key { None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0, LeftShift, RightShift, LeftAlt, RightAlt, LeftCtrl, RightCtrl,
        LeftMeta, RightMeta, ContextMenu, Escape, LeftArrow, RightArrow, UpArrow, DownArrow, Backspace, PageDown, PageUp, Home, End, Insert, Delete,
        CapsLock, NumLock, PrintScreen, ScrollLock, Pause, NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals,
        Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }
    public class InputControl<T> where T : struct { public T ReadValue() => default; }
    namespace Controls
    {
        public class ButtonControl : InputControl<float> { public bool isPressed => false; public bool wasPressedThisFrame => false; }
        public class KeyControl : ButtonControl { }
        public class Vector2Control : InputControl<Vector2> { }
    }
    public class Keyboard { public static Keyboard current => null; public Controls.KeyControl this[Key key] => null; }
    public class Mouse { public static Mouse current => null; public Controls.Vector2Control scroll => null; public Controls.Vector2Control delta => null; public Controls.ButtonControl rightButton => null; public Controls.ButtonControl middleButton => null; }
}
