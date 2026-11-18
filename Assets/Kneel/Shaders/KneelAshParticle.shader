// Soft round particles for the Ash effects (AshEffects, AshPresenter): no texture, a disc that fades to its edge,
// tinted by the particle colour. _SrcBlend/_DstBlend pick additive (embers: One One) or alpha (ash: SrcAlpha
// OneMinusSrcAlpha).
Shader "Kneel/Ash Particle"
{
    Properties
    {
        [HDR] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Softness ("Edge softness", Range(0.5, 4)) = 1.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "AshParticle"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Softness;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float d = length(input.uv * 2.0 - 1.0);
                half alpha = pow(saturate(1.0 - d), _Softness) * input.color.a;
                // Additive materials use the colour as light, so it is premultiplied either way.
                return half4(input.color.rgb * (_DstBlend == 1 ? alpha : 1.0), alpha);
            }
            ENDHLSL
        }
    }
}
