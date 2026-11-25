#ifndef TAXISIM_LIGHTING_INCLUDED
#define TAXISIM_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "TaxiSimCommon.hlsl"

// Height fog with drifting smog and neon scattering near street level (Neon Mumbai 2077 look).
// Applied on top of Unity's distance fog. With _TS_FogDensity = 0 it does nothing.
half3 TS_Atmosphere(half3 color, float3 positionWS)
{
    float3 cam = _WorldSpaceCameraPos;
    float dist = distance(positionWS, cam);
    float y = max(positionWS.y, 0.0);
    float avgY = (y + max(cam.y, 0.0)) * 0.5;
    float heightFactor = exp(-avgY / max(_TS_FogHeight, 0.1));
    float drift = 0.75 + 0.5 * TS_ValueNoise(positionWS.xz * 0.015 + _Time.y * 0.02);
    float fog = 1.0 - exp(-dist * _TS_FogDensity * heightFactor * drift);
    half3 fogColor = lerp(_TS_FogColor.rgb, _TS_NeonFog.rgb, saturate(exp(-y * 0.25)) * _TS_Style * _TS_Night);
    // v0.10 Mie in-scattering: the haze glows warm towards the sun (golden hour, hazy noon)
    float3 viewDir = (positionWS - cam) / max(dist, 1e-3);
    float cosSun = saturate(dot(viewDir, _TS_SunDir.xyz));
    float mie = pow(cosSun, 10.0) * 0.9 + pow(cosSun, 3.0) * 0.25;
    fogColor += _TS_SunColor.rgb * mie;
    return lerp(color, fogColor, saturate(fog));
}

// Full URP physically-based shading (main light + shadows, additional lights, SSAO, reflection
// probes, spherical-harmonics ambient, fog) for our procedural surfaces.
half4 TS_ShadePBR(float3 positionWS, float3 normalWS, float4 positionCS, float fogFactor,
                  half3 albedo, half metallic, half smoothness, half occlusion, half3 emission, half alpha)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.positionCS = positionCS;
    inputData.normalWS = NormalizeNormalPerPixel(normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    inputData.fogCoord = fogFactor;
    inputData.vertexLighting = half3(0, 0, 0);
    inputData.bakedGI = SampleSH(inputData.normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo;
    surface.metallic = metallic;
    surface.specular = half3(0, 0, 0);
    surface.smoothness = smoothness;
    surface.normalTS = half3(0, 0, 1);
    surface.emission = emission;
    surface.occlusion = occlusion;
    surface.alpha = alpha;

    half4 color = UniversalFragmentPBR(inputData, surface);
    color.rgb = MixFog(color.rgb, fogFactor);
    color.rgb = TS_Atmosphere(color.rgb, positionWS);
    return color;
}

#endif
