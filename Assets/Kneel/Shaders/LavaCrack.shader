// Glowing lava cracks on the floor (the throne hall's collapsing panels and the Ash Colossus's Hammerfall). A decal
// quad, additive and unlit, so the cracks light the dark stone like L4's crust. The crack texture's red channel is the
// crack (bright core, soft glow round it); its green channel is when each bit of crack is reached as it spreads (0 at
// its seed, 1 at its tips). _Reveal runs the cracks out from their seeds; _Intensity brightens them (HDR, for the
// bloom); _Pulse adds a flicker at the junctions before they burst.
Shader "Kneel/LavaCrack"
{
    Properties
    {
        _CrackTex ("Cracks (R crack, G spread order)", 2D) = "black" {}
        [HDR] _Color ("Colour", Color) = (1, 0.42, 0.1, 1)
        [HDR] _HotColor ("Hot colour (the cores)", Color) = (1, 0.85, 0.45, 1)
        _Intensity ("Intensity", Float) = 1
        _Reveal ("Reveal (0-1)", Range(0, 1.2)) = 1
        _Edge ("Reveal edge softness", Range(0.005, 0.2)) = 0.04
        _Pulse ("Pulse", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "LavaCrack"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CrackTex);
            SAMPLER(sampler_CrackTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CrackTex_ST;
                float4 _Color;
                float4 _HotColor;
                float _Intensity;
                float _Reveal;
                float _Edge;
                float _Pulse;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _CrackTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 t = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, input.uv);
                half crack = t.r;
                // Spread: a bit of crack shows once the reveal has passed its order, brightest right at the front.
                half shown = saturate((_Reveal - t.g) / _Edge);
                half front = saturate(1 - abs(_Reveal - t.g) / (_Edge * 3)) * step(t.g, _Reveal);
                // The cores (crack near 1) run hot; the glow round them stays orange.
                half core = smoothstep(0.55, 1.0, crack);
                half3 colour = lerp(_Color.rgb, _HotColor.rgb, core);
                half flicker = 1 + _Pulse * 0.6 * sin(_Time.y * 37 + t.g * 23) * core;
                half3 c = colour * crack * shown * (_Intensity * flicker + front * 2.5);
                return half4(max(c, 0), 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
