// Holographic markers: charging-station beacons, hospital beacons, request pins, location rings.
// Additive fresnel glow + moving scanlines + pulse; fades out towards uv.y = 1 (top of beacon).
Shader "TaxiSim/Holo"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.2, 1.4, 3.0, 1)
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _ScanDensity ("Scanline Density", Float) = 7.0
        _Intensity ("Intensity", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" }

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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _PulseSpeed;
                float _ScanDensity;
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                float rim = pow(1.0 - abs(dot(normalize(i.normalWS), viewDir)), 2.0);
                float scan = 0.65 + 0.35 * step(0.5, frac(i.positionWS.y * _ScanDensity - _Time.y * 1.5));
                float pulse = 0.75 + 0.25 * sin(_Time.y * _PulseSpeed);
                float fade = pow(saturate(1.0 - i.uv.y), 1.5);
                half3 color = _Color.rgb * (rim * 1.3 + 0.18) * scan * pulse * fade * _Intensity;
                color = MixFogColor(color, half3(0, 0, 0), i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
