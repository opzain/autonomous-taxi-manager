// Procedural Mumbai sky: gradient + coastal haze band, sun disc & glow, moon, twinkling stars and
// drifting fBm clouds. All colours are driven by TimeOfDay.cs / WeatherSystem.cs.
Shader "TaxiSim/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.18, 0.38, 0.75, 1)
        _HorizonColor ("Horizon", Color) = (0.72, 0.78, 0.85, 1)
        _GroundColor ("Below Horizon", Color) = (0.22, 0.22, 0.24, 1)
        _HazeColor ("Haze", Color) = (0.85, 0.75, 0.62, 1)
        _HazeStrength ("Haze Strength", Range(0, 1)) = 0.55
        [HDR] _SunColor ("Sun", Color) = (12, 10, 8, 1)
        [HDR] _MoonColor ("Moon", Color) = (2.2, 2.4, 2.8, 1)
        _StarIntensity ("Star Intensity", Float) = 2.0
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.42
        _CloudColor ("Cloud Colour", Color) = (0.95, 0.95, 0.97, 1)
        _CloudSpeed ("Cloud Speed", Float) = 0.004
        [HDR] _CityGlow ("City Light Pollution (night)", Color) = (0.35, 0.18, 0.1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TaxiSimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half4 _HazeColor;
                half _HazeStrength;
                half4 _SunColor;
                half4 _MoonColor;
                half _StarIntensity;
                half _CloudCover;
                half4 _CloudColor;
                float _CloudSpeed;
                half4 _CityGlow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(h), 0.45));
                sky = lerp(sky, _GroundColor.rgb, saturate(-h * 4.0));
                sky = lerp(sky, _HazeColor.rgb, exp(-abs(h) * 9.0) * _HazeStrength);

                float3 sunDir = normalize(_TS_SunDir.xyz);
                float sd = dot(d, sunDir);
                float sunDisc = smoothstep(0.9994, 0.99975, sd);
                half3 sunGlow = _SunColor.rgb * (pow(saturate(sd), 48.0) * 0.06 + pow(saturate(sd), 6.0) * 0.025);
                sky += sunGlow * saturate(sunDir.y * 4.0 + 0.6);

                float3 moonDir = normalize(_TS_MoonDir.xyz);
                float md = dot(d, moonDir);
                float moonDisc = smoothstep(0.99935, 0.99965, md);
                float moonGlow = pow(saturate(md), 90.0) * 0.25;

                // Stars
                float3 sp = d * 230.0;
                float3 cell = floor(sp);
                float starRand = TS_Hash13(cell);
                float starMask = step(0.9975, starRand);
                float starShape = 1.0 - smoothstep(0.05, 0.35, length(frac(sp) - 0.5));
                float twinkle = 0.6 + 0.4 * sin(_Time.y * (2.0 + starRand * 6.0) + starRand * 50.0);
                float stars = starMask * starShape * twinkle * _StarIntensity * _TS_Night * saturate(h * 3.0);

                // Clouds on a virtual plane above the city
                float2 cp = d.xz / max(h, 0.06) * 0.55 + _Time.y * _CloudSpeed * float2(1.0, 0.35);
                float c = TS_Fbm(cp * 1.4);
                float cloud = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.32, c) * saturate(h * 6.0);
                half3 cloudLit = _CloudColor.rgb * (0.55 + 0.65 * saturate(sd * 0.5 + 0.5));
                cloudLit += _SunColor.rgb * 0.012 * pow(saturate(sd), 4.0) * (1.0 - _TS_Night);

                // City light pollution: lights the smog band and the underside of clouds at night
                float glowBand = exp(-max(h, 0.0) * 6.0) * _TS_Night;
                sky += _CityGlow.rgb * glowBand * 0.6;
                cloudLit += _CityGlow.rgb * _TS_Night * (0.6 + 0.8 * (1.0 - saturate(h * 2.0)));

                sky += stars * (1.0 - cloud);
                sky += _MoonColor.rgb * (moonDisc + moonGlow) * _TS_Night * (1.0 - cloud * 0.8);
                sky = lerp(sky, cloudLit, cloud * 0.88);
                sky += _SunColor.rgb * sunDisc * (1.0 - cloud * 0.9) * saturate(sunDir.y * 8.0 + 0.5);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
