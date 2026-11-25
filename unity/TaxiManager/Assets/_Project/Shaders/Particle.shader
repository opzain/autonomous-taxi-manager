// Texture-free particle shader for monsoon rain streaks, fountain spray and festival sparkles.
// Uses the particle vertex colour; the streak/disc shape is generated from the uv.
Shader "TaxiSim/Particle"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (0.75, 0.82, 0.95, 0.35)
        _Shape ("Shape (0 = streak, 1 = soft disc)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+30" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha [_DstBlend]
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
                half _Shape;
                float _DstBlend;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float fogFactor : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                float streak = (1.0 - smoothstep(0.08, 0.5, abs(c.x))) * (1.0 - smoothstep(0.2, 0.5, abs(c.y)));
                float disc = 1.0 - smoothstep(0.1, 0.5, length(c));
                float mask = lerp(streak, disc, _Shape);
                half4 col = _Color * i.color;
                col.a *= mask;
                col.rgb = MixFog(col.rgb, i.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
