// Holographic billboards and vertical blade signs (Neon Mumbai 2077).
// Animated procedural signage (fake text rows, rings, bars, scanlines, glitch bursts) with an
// RGB-split edge. Additive + HDR so URP Bloom makes it glow. Seed per sign = uv1.x.
Shader "TaxiSim/HoloAd"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 2.0
        _DayVisibility ("Daytime Visibility", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TaxiSimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
                half _DayVisibility;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 uv1 : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; float fogFactor : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.seed = v.uv1.x;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                // Slight chromatic split for the hologram feel
                half3 c;
                c.r = TS_AdPattern(i.uv + float2(0.004, 0), i.seed, t).r;
                c.g = TS_AdPattern(i.uv, i.seed, t).g;
                c.b = TS_AdPattern(i.uv - float2(0.004, 0), i.seed, t).b;
                float powerFlicker = 0.85 + 0.15 * step(0.1, TS_Hash12(float2(floor(t * 20.0), i.seed * 17.0)));
                float visible = lerp(_DayVisibility, 1.0, smoothstep(0.1, 0.6, _TS_Night)) * _TS_Style;
                half3 color = c * _Intensity * powerFlicker * visible;
                color = MixFogColor(color, half3(0, 0, 0), i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
