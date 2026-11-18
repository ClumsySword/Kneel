// The full-screen grade while Kael's Ash meter is full, drawn after post-processing by a FullScreenPassRendererFeature
// on the PC and Mobile renderers. Relative to whatever the level's own grade made: it drains colour from everything
// that isn't red or orange (so the arm and the enemies' wind-ups are the only colour on screen), warms the shadows a
// little, and closes a faint ember vignette. _KneelAshGrade (global, set by AshPresenter) is 0 when off.
Shader "Kneel/Ash Grade"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "AshGrade"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _KneelAshGrade;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float grade = _KneelAshGrade;
                if (grade <= 0.0001)
                {
                    return color;
                }

                float luma = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
                float top = max(color.r, max(color.g, color.b));
                // How much a pixel is red-orange: red leads, blue trails.
                float warm = saturate((color.r - max(color.g * 0.75, color.b)) / max(top, 0.0001) * 2.5);
                float3 graded = lerp(color.rgb, luma.xxx, saturate(grade) * (1.0 - warm));

                // A slight warm lift in the darks.
                graded += (1.0 - saturate(luma * 5.0)) * float3(0.012, 0.005, 0.0) * grade;

                // A faint ember vignette.
                float edge = smoothstep(0.45, 1.0, length((uv - 0.5) * float2(1.6, 1.2)));
                graded = lerp(graded, graded * float3(0.55, 0.32, 0.22), edge * 0.45 * saturate(grade));

                return half4(graded, color.a);
            }
            ENDHLSL
        }
    }
}
