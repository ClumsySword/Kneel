// The village ground: URP-lit (main light, shadows, Forward+ lights, SSAO, fog) blend of two tiled layers in
// world space over the painted layout map: muddy earth everywhere, cobbles where the paving mask says so.
// The blend is by height, so at the edge of the paving the stones stand up out of the mud instead of fading.
// The layout map's RGB is the large-scale colour (overlays are centred on 0.5, x2), its alpha how trodden and
// wet the ground is. Where it is wet, puddles stand in world-space patches: in the mud they fill the hollows, on
// the cobbles the joints first and, in the deeper ones, whole stones. Moss creeps through the cobbles in patches,
// thickest toward the paving's worn edges: in the joints first, over the stones where a patch is thick.
Shader "Kneel/Ground"
{
    Properties
    {
        [MainTexture] _BaseMap("Layout (RGB colour, A wetness)", 2D) = "grey" {}
        _PaveMask("Paving mask (A)", 2D) = "black" {}
        _MudMap("Mud overlay (RGB, A height)", 2D) = "grey" {}
        [Normal] _MudNormal("Mud normal", 2D) = "bump" {}
        _CobbleMap("Cobble overlay (RGB, A height)", 2D) = "grey" {}
        [Normal] _CobbleNormal("Cobble normal", 2D) = "bump" {}
        _MudTile("Mud tile (m)", Float) = 4
        _CobbleTile("Cobble tile (m)", Float) = 3
        _MudNormalScale("Mud normal strength", Float) = 1
        _CobbleNormalScale("Cobble normal strength", Float) = 1.35
        _Parallax("Cobble parallax (m)", Range(0, 0.08)) = 0.03
        _PuddleScale("Puddle patch size (m)", Float) = 4.5
        _PuddleAmount("Puddle cover", Range(0, 1)) = 0.37
        _PuddleDepth("Puddle depth", Range(0, 1)) = 0.66
        _PuddleSky("Puddle sky sheen", Color) = (0.035, 0.045, 0.065, 1)
        _MossColor("Moss colour", Color) = (0.33, 0.42, 0.25, 1)
        _MossAmount("Moss cover", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _PaveMask_ST;
            float _MudTile;
            float _CobbleTile;
            float _MudNormalScale;
            float _CobbleNormalScale;
            float _Parallax;
            float _PuddleScale;
            float _PuddleAmount;
            float _PuddleDepth;
            half4 _PuddleSky;
            half4 _MossColor;
            float _MossAmount;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_PaveMask); SAMPLER(sampler_PaveMask);
        TEXTURE2D(_MudMap); SAMPLER(sampler_MudMap);
        TEXTURE2D(_MudNormal); SAMPLER(sampler_MudNormal);
        TEXTURE2D(_CobbleMap); SAMPLER(sampler_CobbleMap);
        TEXTURE2D(_CobbleNormal); SAMPLER(sampler_CobbleNormal);

        struct GroundSample
        {
            half3 albedo;
            half3 normalTS;
            half smoothness;
            half occlusion;
            half3 emission;
        };

        // Soft world-space value noise (no texture): blobs about a metre per unit of p.
        float GroundHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float GroundNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(GroundHash(i), GroundHash(i + float2(1, 0)), f.x), lerp(GroundHash(i + float2(0, 1)), GroundHash(i + 1.0), f.x), f.y);
        }

        // Both layers, blended by height; viewTS is the view direction in the ground's tangent space (x = world x,
        // y = world z, z = up), used for the cobbles' parallax.
        GroundSample SampleGround(float2 uv, float3 positionWS, half3 viewTS)
        {
            GroundSample g;
            half4 layout = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            half pave = SAMPLE_TEXTURE2D(_PaveMask, sampler_PaveMask, uv).a;
            half wet = saturate((layout.a - 0.08h) / 0.12h);

            float2 mudUV = positionWS.xz / _MudTile;
            half4 mud = SAMPLE_TEXTURE2D(_MudMap, sampler_MudMap, mudUV);

            float2 cobUV = positionWS.xz / _CobbleTile;
            half h0 = SAMPLE_TEXTURE2D(_CobbleMap, sampler_CobbleMap, cobUV).a;
            cobUV += viewTS.xy / max(viewTS.z, 0.35h) * (h0 - 0.5h) * (_Parallax / _CobbleTile);
            half4 cob = SAMPLE_TEXTURE2D(_CobbleMap, sampler_CobbleMap, cobUV);

            // Mud rises over the stones as the paving thins: a stone shows where it stands above the mud line.
            half mudLine = (1.0h - pave) * 1.05h + (mud.a - 0.5h) * 0.25h;
            half w = smoothstep(-0.06h, 0.06h, cob.a - mudLine);

            half3 mudN = UnpackNormalScale(SAMPLE_TEXTURE2D(_MudNormal, sampler_MudNormal, mudUV), _MudNormalScale);
            half3 cobN = UnpackNormalScale(SAMPLE_TEXTURE2D(_CobbleNormal, sampler_CobbleNormal, cobUV), _CobbleNormalScale);

            // Puddles: a patchy water level over the wet ground. Where the patch noise is high the water rises; it
            // fills whatever lies below it (the mud's hollows, the joints between setts, then the setts themselves).
            float2 xz = positionWS.xz;
            half basin = GroundNoise(xz / _PuddleScale) * 0.7h + GroundNoise(xz / (_PuddleScale * 0.37) + 17.0) * 0.3h;
            // Water gathers along the edges of the paths (where the trodden, wet band of the layout fades out),
            // seldom in the middle of the way: a small detail round the path, not a flooded street.
            half edge = saturate(1.0h - abs(wet - 0.5h) * 2.4h);
            half level = saturate((basin - (1.0h - _PuddleAmount)) / 0.1h) * _PuddleDepth * lerp(0.4h, 1.0h, edge) * wet;
            // Mud puddles follow the ground's broad dips (a blurred height), not every pebble; mud holds water more
            // readily than paving.
            half mudBroad = SAMPLE_TEXTURE2D_BIAS(_MudMap, sampler_MudMap, mudUV, 3.5).a;
            half surface = lerp(lerp(mud.a, mudBroad, 0.75h) - 0.15h, cob.a * 0.85h + 0.1h, w);
            half puddle = saturate((level - surface) / 0.045h);
            half damp = saturate((level + 0.1h - surface) / 0.1h);   // a darker wet halo round each puddle

            // Moss: patches across the paving, more where it is worn at its edges, in the joints before the tops.
            half patch = GroundNoise(xz / 5.5 + 41.0) * 0.65h + GroundNoise(xz / 1.6 + 9.0) * 0.35h;
            patch = saturate(patch + (1.0h - pave) * 0.25h - (1.0h - _MossAmount) * 0.55h);
            half moss = smoothstep(0.0h, 0.15h, patch * 1.7h - cob.a * 0.75h) * w * (1.0h - puddle);

            half3 overlay = lerp(mud.rgb, cob.rgb, w);
            half3 albedo = layout.rgb * overlay * 2.0h;
            albedo = lerp(albedo, _MossColor.rgb * 0.34h, moss * 0.9h);
            albedo *= lerp(1.0h, 0.82h, damp);
            g.albedo = albedo * lerp(1.0h, 0.42h, puddle);

            half3 n = lerp(lerp(mudN, cobN, w), half3(0, 0, 1), moss * 0.35h);
            g.normalTS = normalize(lerp(n, half3(0, 0, 1), puddle));
            half dry = max(lerp(0.12h, 0.26h, wet), w * 0.2h) * (1.0h - moss * 0.8h);
            g.smoothness = lerp(lerp(dry, 0.5h, damp * 0.5h), 0.88h, smoothstep(0.25h, 1.0h, puddle));
            // The water mirrors the lights' glints, but only a little of the sky probe (a bright one would turn
            // every puddle into a pale sheet).
            g.occlusion = lerp(1.0h, 0.3h, puddle);
            // Standing water mirrors the night sky faintly, so a puddle reads even away from the moon and fires.
            g.emission = _PuddleSky.rgb * smoothstep(0.4h, 1.0h, puddle);
            return g;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
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
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                // Tangent frame for ground laid out in world x/z: T along +x, B along +z.
                half3 t = normalize(half3(1, 0, 0) - n * n.x);
                half3 b = cross(t, n);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 viewTS = half3(dot(viewWS, t), dot(viewWS, b), dot(viewWS, n));

                GroundSample g = SampleGround(input.uv, input.positionWS, viewTS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalize(t * g.normalTS.x + b * g.normalTS.y + n * g.normalTS.z);
                inputData.viewDirectionWS = viewWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = g.albedo;
                s.metallic = 0;
                s.specular = half3(0, 0, 0);
                s.smoothness = g.smoothness;
                s.normalTS = g.normalTS;
                s.occlusion = g.occlusion;
                s.emission = g.emission;
                s.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, s);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half Frag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
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
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            void Frag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                half3 n = normalize(input.normalWS);
                half3 t = normalize(half3(1, 0, 0) - n * n.x);
                half3 b = cross(t, n);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                GroundSample g = SampleGround(input.uv, input.positionWS, half3(dot(viewWS, t), dot(viewWS, b), dot(viewWS, n)));
                half3 normalWS = normalize(t * g.normalTS.x + b * g.normalTS.y + n * g.normalTS.z);
                outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
