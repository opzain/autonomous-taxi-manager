// Glowing animated route line (dispatch routes, rival routes, data overlays).
// uv0 = (0..1 across, distance along in units). Additive + HDR so URP Bloom makes it glow.
Shader "TaxiSim/FlowLine"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.2, 1.6, 3.0, 1)
        _Speed ("Scroll Speed", Float) = 1.4
        _Spacing ("Chevron Spacing", Float) = 1.2
        _Intensity ("Intensity", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }

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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Speed;
                float _Spacing;
                half _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float across = abs(i.uv.x - 0.5);
                float core = 1.0 - smoothstep(0.0, 0.5, across);
                float chevron = frac(i.uv.y / _Spacing - _Time.y * _Speed + across * 0.8);
                float arrow = smoothstep(0.0, 0.15, chevron) * (1.0 - smoothstep(0.3, 0.5, chevron));
                float edge = smoothstep(0.36, 0.5, across) * (1.0 - smoothstep(0.49, 0.5, across));
                half3 color = _Color.rgb * (0.22 * core + arrow * 0.9 * (1.0 - across) + edge * 0.7) * _Intensity;
                color = MixFogColor(color, half3(0, 0, 0), i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
