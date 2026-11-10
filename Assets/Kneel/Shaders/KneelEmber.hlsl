#ifndef KNEEL_EMBER_INCLUDED
#define KNEEL_EMBER_INCLUDED

// The Master's tell glint (Kneel/Ember Body, forward pass): a white-hot band that runs up his ember cracks.
// Globals, not material properties, so the material keeps URP Lit's layout and stays SRP Batcher compatible; there is
// only ever one boss. BossTell sets them; strength 0 (the default) means no glint.
//   _KneelGlintColor : rgb = colour (HDR), a = strength
//   _KneelGlintBand  : x = band centre (world height, m), y = half width (m)
float4 _KneelGlintColor;
float4 _KneelGlintBand;

void KneelEmberFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    LitPassFragment(input, outColor
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );
#if defined(_EMISSION)
    if (_KneelGlintColor.a > 0.0)
    {
        half3 cracks = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
        half mask = max(cracks.r, max(cracks.g, cracks.b));
        float band = saturate(1.0 - abs(input.positionWS.y - _KneelGlintBand.x) / max(_KneelGlintBand.y, 0.001));
        outColor.rgb += _KneelGlintColor.rgb * (_KneelGlintColor.a * mask * band * band);
    }
#endif
}

#endif
