// Full-screen blood that creeps in from the screen edges. Drawn on a UI RawImage by PlayerDamageVignette.
// _Coverage 0 shows nothing, 1 reaches the centre. The edge is warped by drifting value noise so it reads
// as blood soaking in rather than a smooth vignette.
Shader "Kneel/BloodVignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Blood Colour", Color) = (0.32, 0.01, 0.01, 1)
        _DarkColor ("Deep Colour", Color) = (0.07, 0, 0.003, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.9
        _Coverage ("Coverage", Range(0, 1.5)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.08
        _Depth ("Deep Colour Depth", Range(0.01, 1)) = 0.25
        _NoiseScale ("Noise Scale", Float) = 3
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.25
        _NoiseOffset ("Noise Offset", Vector) = (0, 0, 0, 0)
        _ShadowWidth ("Shadow Width", Range(0, 0.5)) = 0.12
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        // Leaves destination alpha alone, so screenshots (which keep alpha) show what the game shows.
        Blend SrcAlpha OneMinusSrcAlpha, Zero One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _DarkColor;
            float _Opacity;
            float _Coverage;
            float _EdgeSoftness;
            float _Depth;
            float _NoiseScale;
            float _NoiseStrength;
            float4 _NoiseOffset;
            float _ShadowWidth;
            float _ShadowStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Integer hash: exact, so neighbouring cells agree on their shared corners. A float hash showed
            // seams, because rounding differences between pixels were amplified into different values.
            float Hash(int2 cell)
            {
                uint h = (uint)cell.x * 1597334677u ^ (uint)cell.y * 3812015801u;
                h = (h ^ (h >> 16)) * 2246822519u;
                h ^= h >> 13;
                return h * (1.0 / 4294967295.0);
            }

            float ValueNoise(float2 p)
            {
                int2 i = (int2)floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + int2(1, 0));
                float c = Hash(i + int2(0, 1));
                float d = Hash(i + int2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Rounded-rectangle distance (superellipse), so the blood hugs all four edges evenly.
                float2 p = abs(i.uv * 2.0 - 1.0);
                float boxDistance = pow(pow(p.x, 4.0) + pow(p.y, 4.0), 0.25);
                float inward = 1.0 - boxDistance;

                // Noise in aspect-corrected screen space so blotches stay round on wide screens.
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 noiseUV = float2(i.uv.x * aspect, i.uv.y) * _NoiseScale + _NoiseOffset.xy;
                float noise = ValueNoise(noiseUV) * 0.5 + ValueNoise(noiseUV * 2.3 + 17.1) * 0.3 + ValueNoise(noiseUV * 5.7 + 41.7) * 0.2;

                // Fade the noise in with coverage, so zero coverage is truly empty (corners included).
                float threshold = _Coverage + (noise - 0.5) * _NoiseStrength * saturate(_Coverage * 10.0);
                float mask = smoothstep(threshold + _EdgeSoftness, threshold - _EdgeSoftness, inward);

                // Bright at the leading edge, darker the further it has soaked in, mottled by fine noise.
                float depth = saturate((threshold - inward) / _Depth);
                float mottle = ValueNoise(noiseUV * 9.1 + 73.3);
                float3 color = lerp(_Color.rgb, _DarkColor.rgb, depth) * lerp(0.7, 1.15, mottle);

                // A soft shadow just ahead of the blood, so its edge sits on the image instead of cutting a hole.
                float shadow = smoothstep(threshold + _EdgeSoftness + _ShadowWidth, threshold, inward) * _ShadowStrength * saturate(_Coverage * 10.0);

                float blood = mask * _Opacity;
                float alpha = blood + shadow * (1.0 - blood);
                return float4(color * (blood / max(alpha, 1e-4)), alpha);
            }
            ENDCG
        }
    }
}
