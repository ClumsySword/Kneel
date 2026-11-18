// The Ash glow on Kael's right arm: an additive shell just outside the skin of the forearm and hand (AshPresenter),
// also used for Surge's afterimages. Bright at the silhouette (fresnel) so it reads as heat coming off the arm, not a
// painted tube, with thin vein-like lines crawling along it in the shell's own space, so they don't swim.
// _Fill reveals it from the hand (0) toward the elbow (1); _FillRange maps this shell's length into that span.
// _Pulse flares the whole shell regardless of fill.
Shader "Kneel/Ember Glow"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (3, 0.9, 0.25, 1)
        _Intensity ("Intensity", Float) = 1
        _Fill ("Fill", Range(0, 1)) = 1
        _FillRange ("Fill range (x at the +Y end, y at the -Y end)", Vector) = (0, 1, 0, 0)
        _Pulse ("Pulse", Float) = 0
        _Flicker ("Flicker", Range(0, 1)) = 0.15
        _RimPower ("Rim power", Float) = 2.2
        _VeinScale ("Vein scale", Vector) = (5, 2.2, 5, 0)
        _VeinSharpness ("Vein sharpness", Float) = 9
        _Crawl ("Vein crawl speed", Float) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "EmberGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Fill;
                float4 _FillRange;
                float _Pulse;
                float _Flicker;
                float _RimPower;
                float4 _VeinScale;
                float _VeinSharpness;
                float _Crawl;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
            };

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ValueNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y),
                    f.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float along = lerp(_FillRange.x, _FillRange.y, saturate((1.0 - input.positionOS.y) * 0.5));
                float fillMask = saturate((_Fill - along) / 0.08 + 0.5) * step(0.001, _Fill);

                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewWS);
                float rim = pow(1.0 - saturate(abs(dot(n, v))), _RimPower);

                float3 p = input.positionOS * _VeinScale.xyz + float3(0, -_Time.y * _Crawl, 0);
                float noise = ValueNoise(p) * 0.65 + ValueNoise(p * 2.1 + 7.3) * 0.35;
                float vein = pow(saturate(1.0 - abs(noise * 2.0 - 1.0)), _VeinSharpness);

                float flicker = 1.0 - _Flicker + _Flicker * 2.0 * ValueNoise(float3(_Time.y * 7.0, 0.5, 0.5));
                float glow = (rim * 0.9 + vein * 0.9 + 0.05) * fillMask * _Intensity * flicker
                    + _Pulse * (rim * 0.8 + vein * 0.6);

                return half4(_Color.rgb * glow, 1);
            }
            ENDHLSL
        }
    }
}
