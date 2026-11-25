#ifndef TAXISIM_PASSES_INCLUDED
#define TAXISIM_PASSES_INCLUDED

// Shared ShadowCaster / DepthOnly / DepthNormals passes for every opaque TaxiSim shader.
// Each shader includes this after declaring its own UnityPerMaterial CBUFFER (in HLSLINCLUDE),
// so all passes share the same CBUFFER and the shader stays SRP Batcher compatible.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

struct TSDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TSDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS   : TEXCOORD0;
};

float3 _LightDirection;
float3 _LightPosition;

TSDepthVaryings TS_ShadowVert(TSDepthAttributes input)
{
    TSDepthVaryings o;
    UNITY_SETUP_INSTANCE_ID(input);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
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
    o.positionCS = positionCS;
    o.normalWS = normalWS;
    return o;
}

half4 TS_ShadowFrag(TSDepthVaryings input) : SV_Target
{
    return 0;
}

TSDepthVaryings TS_DepthVert(TSDepthAttributes input)
{
    TSDepthVaryings o;
    UNITY_SETUP_INSTANCE_ID(input);
    o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    o.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return o;
}

half TS_DepthFrag(TSDepthVaryings input) : SV_Target
{
    return input.positionCS.z;
}

half4 TS_DepthNormalsFrag(TSDepthVaryings input) : SV_Target
{
    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
    half3 packedNormalWS = PackFloat2To888(remapped);
    return half4(packedNormalWS, 0.0);
#else
    return half4(normalWS, 0.0);
#endif
}

#endif
