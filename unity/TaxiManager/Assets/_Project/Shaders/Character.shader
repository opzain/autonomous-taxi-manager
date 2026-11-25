// Pedestrians (v0.7): low-poly people with a GPU walk cycle.
// Mesh data (PedestrianFactory.cs): colour = clothing / skin, uv1.x = limb id
// (0 body, 1 left leg, 2 right leg, 3 left arm, 4 right arm), uv1.y = pivot height (hip / shoulder).
// _Walk (per renderer, MaterialPropertyBlock): 0 = standing, 1 = walking. Phase comes from the
// object's world position so people don't walk in step.
Shader "TaxiSim/Character"
{
    Properties
    {
        _WalkSpeed ("Steps Per Second", Float) = 1.8
        _Swing ("Limb Swing (radians)", Float) = 0.5
        _Walk ("Walking (0/1)", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _WalkSpeed;
            float _Swing;
            float _Walk;
            half _Smoothness;
        CBUFFER_END

        struct CAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float4 uv1        : TEXCOORD1;
            half4  color      : COLOR;
        };

        // Rotates limbs about the X axis at their pivot (object space), returns animated position.
        float3 TS_Animate(float3 p, inout float3 n, float4 uv1)
        {
            float3 objPos = float3(UNITY_MATRIX_M[0].w, UNITY_MATRIX_M[1].w, UNITY_MATRIX_M[2].w);
            float phase = _Time.y * _WalkSpeed * 6.2831 + frac(sin(dot(objPos.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2831;
            float s = sin(phase) * _Swing * _Walk;
            float limb = uv1.x;
            float angle = limb < 0.5 ? 0.0
                        : limb < 1.5 ? s
                        : limb < 2.5 ? -s
                        : limb < 3.5 ? -s * 0.8
                        : s * 0.8;
            float pivot = uv1.y;
            float cs = cos(angle), sn = sin(angle);
            float y = p.y - pivot;
            p = float3(p.x, pivot + y * cs - p.z * sn, y * sn + p.z * cs);
            n = float3(n.x, n.y * cs - n.z * sn, n.y * sn + n.z * cs);
            p.y += abs(sin(phase)) * 0.006 * _Walk; // body bob
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "TaxiSimLighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half4  color      : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(CAttributes v)
            {
                Varyings o;
                float3 n = v.normalOS;
                float3 p = TS_Animate(v.positionOS.xyz, n, v.uv1);
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(n);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = i.color.rgb * lerp(1.0, 0.75, _TS_Wetness);
                return TS_ShadePBR(i.positionWS, i.normalWS, i.positionCS, i.fogFactor, albedo, 0, _Smoothness, 1, half3(0, 0, 0), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            float4 vertShadow(CAttributes v) : SV_POSITION
            {
                float3 n = v.normalOS;
                float3 p = TS_Animate(v.positionOS.xyz, n, v.uv1);
                float3 positionWS = TransformObjectToWorld(p);
                float3 normalWS = TransformObjectToWorldNormal(n);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 fragShadow() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            float4 vertDepth(CAttributes v) : SV_POSITION
            {
                float3 n = v.normalOS;
                float3 p = TS_Animate(v.positionOS.xyz, n, v.uv1);
                return TransformObjectToHClip(p);
            }

            half fragDepth(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
