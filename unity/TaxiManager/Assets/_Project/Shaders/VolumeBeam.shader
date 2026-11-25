// Fake volumetric light: street-lamp light cones, rooftop searchlights and hologram projector beams.
// Soft edges (view-angle falloff), drifting dust/smog noise, soft fade where the beam meets
// geometry (camera depth texture), brightest at the light source. Mostly visible at night.
// uv0.y runs along the beam (0 = cylinder base, 1 = top). _SourceAtTop = 1 for lamps (light above).
Shader "TaxiSim/VolumeBeam"
{
    Properties
    {
        [HDR] _Color ("Colour (multiplied by vertex colour)", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 0.6
        _SourceAtTop ("Source At Top (0/1)", Float) = 1
        _SoftFade ("Soft Intersection Fade", Float) = 0.6
        _DayVisibility ("Daytime Visibility", Range(0, 1)) = 0.0
        _StyleWeight ("Neon Style Weight (0 = always, 1 = neon only)", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+15" "RenderPipeline" = "UniversalPipeline" }

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "TaxiSimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _SourceAtTop;
                half _SoftFade;
                half _DayVisibility;
                half _StyleWeight;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                float fogFactor : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                float facing = abs(dot(normalize(i.normalWS), viewDir));
                float edgeSoft = pow(facing, 1.6);

                float along = _SourceAtTop > 0.5 ? i.uv.y : 1.0 - i.uv.y;
                float falloff = pow(saturate(along), 1.8);

                float dust = 0.7 + 0.3 * TS_ValueNoise(i.positionWS.xz * 0.6 + float2(_Time.y * 0.15, i.positionWS.y * 0.8));

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float soft = saturate((sceneDepth - i.screenPos.w) / max(_SoftFade, 0.01));

                float night = lerp(_DayVisibility, 1.0, smoothstep(0.25, 0.7, _TS_Night));
                float style = lerp(1.0, _TS_Style, _StyleWeight);
                float boost = 1.0 + 0.6 * _TS_Wetness; // rain / wet air scatters more light

                half3 color = _Color.rgb * i.color.rgb * _Intensity * edgeSoft * falloff * dust * soft * night * style * boost;
                color = MixFogColor(color, half3(0, 0, 0), i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
