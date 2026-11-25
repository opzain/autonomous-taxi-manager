// Ground / grass / paving / track-bed / beach surfaces, fully procedural (no textures).
//  * Two-colour fBm variation, optional tile grid (paving), optional grass detail
//  * River banks: blends to wet Mithi mud below street level (_BankBlend)
//  * Monsoon wetness and puddles
Shader "TaxiSim/Surface"
{
    Properties
    {
        _ColorA ("Colour A", Color) = (0.36, 0.34, 0.3, 1)
        _ColorB ("Colour B", Color) = (0.27, 0.27, 0.25, 1)
        _NoiseScale ("Noise Scale", Float) = 0.15
        _TileSize ("Tile Size (0 = none)", Float) = 0
        _TileLine ("Tile Grout Strength", Range(0, 1)) = 0.6
        _GrassDetail ("Grass Detail (0/1)", Float) = 0
        _BankBlend ("River Bank Mud (0/1)", Float) = 0
        _MudColor ("Mud Colour", Color) = (0.2, 0.17, 0.12, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        [NoScaleOffset] _AlbedoMap ("Albedo (optional)", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("Normal (optional)", 2D) = "bump" {}
        [NoScaleOffset] _RoughMap ("Roughness (optional)", 2D) = "white" {}
        _UseTex ("Use Textures (0/1)", Float) = 0
        _TexTiling ("Texture Size (units)", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _ColorA;
            half4 _ColorB;
            float _NoiseScale;
            float _TileSize;
            half _TileLine;
            half _GrassDetail;
            half _BankBlend;
            half4 _MudColor;
            half _Smoothness;
            half _UseTex;
            float _TexTiling;
        CBUFFER_END
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
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog

            #include "TaxiSimLighting.hlsl"

            TEXTURE2D(_AlbedoMap); SAMPLER(sampler_AlbedoMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughMap);  SAMPLER(sampler_RoughMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 wp = i.positionWS.xz;
                float wet = _TS_Wetness;
                float nz = TS_Fbm(wp * _NoiseScale);
                half3 albedo = lerp(_ColorA.rgb, _ColorB.rgb, nz);

                // Optional photographed texture (world-space tiling); large-scale noise breaks up repetition
                float2 tuv = wp / max(_TexTiling, 0.01);
                half3 stex = SAMPLE_TEXTURE2D(_AlbedoMap, sampler_AlbedoMap, tuv).rgb;
                half srough = SAMPLE_TEXTURE2D(_RoughMap, sampler_RoughMap, tuv).r;
                half3 sn = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, tuv));
                albedo = lerp(albedo, stex * (0.8 + 0.4 * nz), _UseTex);

                if (_GrassDetail > 0.5)
                {
                    float blades = TS_ValueNoise(wp * 18.0);
                    albedo *= 0.85 + 0.3 * blades;
                    float dry = smoothstep(0.55, 0.8, TS_Fbm(wp * 0.12 + 5.0));
                    albedo = lerp(albedo, albedo * half3(1.25, 1.1, 0.7), dry * 0.6);
                }

                // Paving tiles (evaluated unconditionally: fwidth must not sit inside a branch)
                float tilesOn = step(0.001, _TileSize);
                float2 tc = wp / max(_TileSize, 0.001);
                float2 b = abs(frac(tc + 0.5) - 0.5);
                float grout = max(TS_Line(b.x, 0.03), TS_Line(b.y, 0.03));
                albedo *= lerp(1.0, 0.9 + 0.2 * TS_Hash12(floor(tc)), tilesOn);
                albedo *= 1.0 - grout * _TileLine * 0.4 * tilesOn;

                // v0.10 bare-earth patches and litter on open ground (not on lawns or paving)
                float bare = (1.0 - tilesOn) * (1.0 - step(0.5, _GrassDetail));
                float dirt = smoothstep(0.5, 0.78, TS_Fbm(wp * 0.07 + 2.0)) * bare;
                albedo = lerp(albedo, half3(0.36, 0.29, 0.21) * (0.8 + 0.4 * nz), dirt * 0.45);
                float2 lc = floor(wp * 14.0);
                float litter = step(0.988, TS_Hash12(lc)) * dirt;
                half3 litterCol = lerp(half3(0.85, 0.85, 0.82), half3(0.2, 0.45, 0.8), step(0.5, TS_Hash12(lc + 3.3)));
                albedo = lerp(albedo, litterCol, litter * 0.7);

                float localWet = wet;
                if (_BankBlend > 0.5)
                {
                    float mud = 1.0 - smoothstep(-0.32, -0.02, i.positionWS.y);
                    albedo = lerp(albedo, _MudColor.rgb * (0.8 + 0.4 * nz), mud);
                    localWet = max(localWet, mud * 0.8);
                }

                float puddle = smoothstep(0.62, 0.75, TS_Fbm(wp * 0.25 + 7.7)) * wet * (1.0 - _GrassDetail);
                half smoothness = lerp(lerp(_Smoothness, (1.0 - srough) * 0.5, _UseTex), 0.7, localWet);
                smoothness = lerp(smoothness, 0.95, puddle);
                albedo *= lerp(1.0, 0.6, localWet);
                float2 rip = TS_RainRipples(wp, _Time.y, _TS_Rain * puddle);
                float3 baseN = normalize(i.normalWS);
                float3 texN = normalize(float3(1, 0, 0) * sn.x + float3(0, 0, 1) * sn.y + baseN * sn.z);
                baseN = normalize(lerp(baseN, texN, _UseTex * (1.0 - puddle)));
                float3 nrm = normalize(baseN + float3(rip.x, 0.0, rip.y));

                return TS_ShadePBR(i.positionWS, nrm, i.positionCS, i.fogFactor, albedo, 0, smoothness, 1, half3(0, 0, 0), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TS_ShadowVert
            #pragma fragment TS_ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "TaxiSimPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TS_DepthVert
            #pragma fragment TS_DepthFrag
            #include "TaxiSimPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TS_DepthVert
            #pragma fragment TS_DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "TaxiSimPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
