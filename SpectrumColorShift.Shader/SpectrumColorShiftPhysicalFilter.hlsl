#ifndef TapCount
#error TapCount must be defined by the build.
#endif

#include "Physical.hlsli"

void AccumulateTap(int tap, float2 position, inout float3 color, inout float alpha)
{
    float4 shifted = LightTexture.SampleLevel(LightSampler, position, 0);
    float4 red = taps[tap * 3];
    float4 green = taps[tap * 3 + 1];
    float4 blue = taps[tap * 3 + 2];
    color += float3(dot(red.rgb, shifted.rgb), dot(green.rgb, shifted.rgb), dot(blue.rgb, shifted.rgb));
    alpha += red.w * shifted.a;
}

float4 GatherInside(float2 uv, float2 pixelStep, float2 scenePosition)
{
    float2 tapStep = filterVertical != 0 ? float2(0.0, pixelStep.y) : float2(pixelStep.x, 0.0);
    float2 first = uv + filterOrigin * tapStep;
    float3 color = 0.0;
    float alpha = 0.0;

    [unroll]
    for (int tap = 0; tap < TapCount; tap++)
        AccumulateTap(tap, first + tap * tapStep, color, alpha);

    return Resolve(color, alpha);
}
