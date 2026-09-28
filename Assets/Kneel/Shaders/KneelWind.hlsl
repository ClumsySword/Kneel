#ifndef KNEEL_WIND_INCLUDED
#define KNEEL_WIND_INCLUDED

// Slow wind for grass, brush and trees (used by Kneel/Lit Wind).
// Each vertex leans downwind in proportion to its height above the object's pivot (its base), so trunks and
// grass roots stay planted while tips and crowns sway. Short things lean further per metre than tall ones.
// No per-material properties: the shader stays SRP Batcher compatible with URP Lit's material layout.

// Blowing north-east, the same way the ash drifts.
#define KNEEL_WIND_DIRECTION float3(0.35, 0.0, 0.94)

float3 KneelApplyWind(float3 positionOS)
{
    float4x4 objectToWorld = GetObjectToWorldMatrix();
    float3 pivotWS = float3(objectToWorld._m03, objectToWorld._m13, objectToWorld._m23);
    float3 positionWS = mul(objectToWorld, float4(positionOS, 1.0)).xyz;
    float height = max(positionWS.y - pivotWS.y, 0.0);

    // Neighbouring plants move out of step; a slow swell with a lighter secondary gust and a faint flutter.
    float t = _Time.y;
    float phase = dot(pivotWS.xz, float2(0.13, 0.21));
    float gust = sin(t * 0.8 + phase) * 0.55 + sin(t * 1.9 + phase * 1.7) * 0.25 + 0.3;
    float flutter = sin(t * 3.7 + dot(positionWS, float3(1.3, 0.7, 1.1))) * 0.07 * saturate(1.0 - height / 4.0);

    // Metres of lean per metre of height: ~0.15 for grass, ~0.08 for a bush, ~0.03 for a tree crown.
    float lean = lerp(0.2, 0.03, sqrt(saturate(height / 3.0)));
    float sway = (gust + flutter) * lean * height;

    float3 offsetWS = KNEEL_WIND_DIRECTION * sway;
    offsetWS.y = -abs(sway) * 0.1;
    return positionOS + mul((float3x3)GetWorldToObjectMatrix(), offsetWS);
}

#endif
