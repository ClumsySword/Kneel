// Far-off landmarks of the next (or previous) level, seen across a seam: one flat colour, unlit, taking only part of
// the scene's fog so a shape 300 m away still reads against the sky instead of vanishing into it. Optionally lighter
// toward its foot, as if haze lay low on the ground. No shadows, no lighting, no glow.
Shader "Kneel/Silhouette"
{
    Properties
    {
        _Color ("Colour", Color) = (0.05, 0.055, 0.07, 1)
        _FogAmount ("Fog taken", Range(0, 1)) = 0.35
        _HazeColor ("Ground haze colour", Color) = (0.2, 0.2, 0.22, 1)
        _HazeTop ("Ground haze top (world y)", Float) = 2
        _HazeBottom ("Ground haze bottom (world y)", Float) = -4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Silhouette"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _FogAmount;
                half4 _HazeColor;
                float _HazeTop;
                float _HazeBottom;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half haze = saturate((_HazeTop - i.positionWS.y) / max(_HazeTop - _HazeBottom, 0.001));
                half3 c = lerp(_Color.rgb, _HazeColor.rgb, haze * haze);
                c = lerp(c, MixFog(c, i.fog), _FogAmount);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
