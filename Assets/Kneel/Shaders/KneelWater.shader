// The creek: murky, slow water. The colour comes from what lies under the surface (the opaque texture, bent a
// little by the ripples) sunk into a muddy brown-green within a few centimetres, so the bed shows only at the very
// edge. The shore fades out on the depth difference so the water meets the banks without a hard polygon line.
// The surface itself is near-black glass: lights and the sky glint off two layers of soft ripples flowing down
// the stream, eastward (uv0 = world x/z in metres, uv1.x = distance from the creek's centre line). The surface is
// a flat grid with world-space ripples, so there is no seam anywhere. Needs the depth and opaque textures (PC).
Shader "Kneel/Water"
{
    Properties
    {
        [Normal] _RippleMap("Ripple normal", 2D) = "bump" {}
        _RippleTile("Ripple tile (m)", Float) = 3
        _RippleStrength("Ripple strength", Range(0, 1)) = 0.35
        _FlowSpeed("Flow speed (m/s)", Float) = 0.22
        _DeepColor("Murk colour", Color) = (0.07, 0.075, 0.055, 1)
        _ShallowColor("Shallow tint", Color) = (0.42, 0.38, 0.28, 1)
        _Murk("Murk (per m)", Float) = 9
        _ShoreFade("Shore fade (m)", Float) = 0.12
        _Refraction("Refraction", Range(0, 0.05)) = 0.012
        _Smoothness("Smoothness", Range(0, 1)) = 0.86
        _Reflection("Sky reflection", Range(0, 1)) = 0.45
        _Glint("Light glints", Range(0, 2)) = 0.4
        _ScumColor("Shore scum", Color) = (0.3, 0.28, 0.22, 1)
        _HalfWidth("Strip half width (m)", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RippleMap_ST;
                float _RippleTile;
                float _RippleStrength;
                float _FlowSpeed;
                half4 _DeepColor;
                half4 _ShallowColor;
                float _Murk;
                float _ShoreFade;
                float _Refraction;
                half _Smoothness;
                half _Reflection;
                half _Glint;
                half4 _ScumColor;
                float _HalfWidth;
            CBUFFER_END

            TEXTURE2D(_RippleMap); SAMPLER(sampler_RippleMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float centreDistance : TEXCOORD2;
                float4 positionNDC : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.positionNDC = p.positionNDC;
                o.centreDistance = input.uv1.x;
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Eye depth of the opaque scene behind a screen point.
            float SceneEyeDepth(float2 screenUV)
            {
                return LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Two layers of soft ripples drifting downstream (east) at different speeds and scales. The ripple
                // map's v runs along the flow, so it is laid with v on world x and u on world z.
                float t = _Time.y * _FlowSpeed;
                float2 flowUV = input.uv.yx;
                float2 uvA = flowUV / _RippleTile * float2(1.0, 0.6) + float2(0.0, -t / _RippleTile);
                float2 uvB = flowUV / (_RippleTile * 0.55) + float2(0.31, 0.17) + float2(t * 0.15, -t * 1.4) / (_RippleTile * 0.55);
                half3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, uvA), _RippleStrength);
                half3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, uvB), _RippleStrength * 0.7);
                half3 normalTS = normalize(half3(nA.xy + nB.xy, nA.z * nB.z));

                // The surface is level: texture u (world z) and v (world x) tilt the up vector directly.
                half3 normalWS = normalize(half3(normalTS.y, normalTS.z, normalTS.x));

                // How much water lies between the surface and the bed along this view ray.
                float2 screenUV = input.positionNDC.xy / input.positionNDC.w;
                float surfaceDepth = input.positionNDC.w;
                float thickness = max(SceneEyeDepth(screenUV) - surfaceDepth, 0.0);

                // Bend the view of the bed a little; never pick up something standing in front of the water.
                float2 refractUV = screenUV + normalTS.xy * _Refraction * saturate(thickness * 4.0);
                float refractThickness = SceneEyeDepth(refractUV) - surfaceDepth;
                if (refractThickness < 0.0)
                {
                    refractUV = screenUV;
                    refractThickness = thickness;
                }

                half3 bed = SampleSceneColor(refractUV) * _ShallowColor.rgb;
                half clarity = exp(-max(refractThickness, 0.0) * _Murk);
                half3 body = lerp(_DeepColor.rgb, bed, clarity);

                // A broken line of scum where the water laps the bank.
                half scumNoise = SAMPLE_TEXTURE2D(_RippleMap, sampler_RippleMap, input.uv * 0.9 + float2(0.0, -t * 0.4)).r;
                half scum = saturate(1.0 - thickness / (_ShoreFade * 1.6)) * saturate(thickness / (_ShoreFade * 0.35)) * smoothstep(0.45, 0.65, scumNoise);
                body = lerp(body, _ScumColor.rgb, scum * 0.45);

                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = half3(0, 0, 0);
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowMask = half4(1, 1, 1, 1);

                // Black glass: no diffuse, so only the lights' glints and the sky's sheen (scaled through occlusion)
                // sit on top of the body colour, toned down so the murk still reads under the moon.
                SurfaceData s = (SurfaceData)0;
                s.albedo = half3(0, 0, 0);
                s.metallic = 0;
                s.smoothness = _Smoothness;
                s.normalTS = normalTS;
                s.occlusion = _Reflection;
                s.alpha = 1;
                half fresnel = Pow4(1.0h - saturate(dot(normalWS, viewWS)));

                half4 color = UniversalFragmentPBR(inputData, s);
                color.rgb = body * (1.0h - fresnel * _Reflection) + color.rgb * _Glint;
                color.rgb = MixFog(color.rgb, inputData.fogCoord);

                // Fade into the banks: by depth at the waterline, and towards the strip's own sides.
                half shore = saturate(thickness / _ShoreFade);
                half sides = smoothstep(_HalfWidth, _HalfWidth * 0.8, input.centreDistance);
                color.a = shore * sides;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
