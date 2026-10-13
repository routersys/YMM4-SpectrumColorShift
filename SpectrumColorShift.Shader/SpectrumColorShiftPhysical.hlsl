#ifndef StepCount
#error StepCount must be defined by the build.
#endif

#include "Physical.hlsli"

void AccumulateRadial(int index, float2 uv, float2 offsetStep, float rate, inout float3 color, inout float alpha)
{
    float4 gain = samples[index * 2];
    float4 basis = samples[index * 2 + 1];
    float scale = clamp(basis.w * rate, -MaxRadialShift, MaxRadialShift);
    Accumulate(gain, basis.rgb, LightTexture.SampleLevel(LightSampler, uv - offsetStep * scale, 0), color, alpha);
}

void AccumulateRotational(int index, float2 uv, float2 offset, float2 pixelStep, float rate, inout float3 color, inout float alpha)
{
    float4 gain = samples[index * 2];
    float4 basis = samples[index * 2 + 1];
    float sine;
    float cosine;
    sincos(basis.w * rate, sine, cosine);
    float2 delta = float2((cosine - 1.0) * offset.x + sine * offset.y, (cosine - 1.0) * offset.y - sine * offset.x);
    Accumulate(gain, basis.rgb, LightTexture.SampleLevel(LightSampler, uv + delta * pixelStep, 0), color, alpha);
}

void AccumulateLinear(int index, float2 uv, float2 stride, inout float3 color, inout float alpha)
{
    float4 gain = samples[index * 2];
    float4 basis = samples[index * 2 + 1];
    Accumulate(gain, basis.rgb, LightTexture.SampleLevel(LightSampler, uv - basis.w * stride, 0), color, alpha);
}

float4 GatherInside(float2 uv, float2 pixelStep, float2 scenePosition)
{
    float2 offset = scenePosition - center;
    float3 color = 0.0;
    float alpha = 0.0;

    if (CompiledMode == ModeRadial)
    {
        float2 offsetStep = offset * pixelStep;
        float rate = shiftDistance / radius;

        [unroll]
        for (int index = 0; index < StepCount; index++)
            AccumulateRadial(index, uv, offsetStep, rate, color, alpha);
    }
    else if (CompiledMode == ModeRotational)
    {
        float rate = shiftDistance / radius;

        [unroll]
        for (int index = 0; index < StepCount; index++)
            AccumulateRotational(index, uv, offset, pixelStep, rate, color, alpha);
    }
    else
    {
        float2 stride = shiftDistance * direction * pixelStep;

        [unroll]
        for (int index = 0; index < StepCount; index++)
            AccumulateLinear(index, uv, stride, color, alpha);
    }

    return Resolve(color, alpha);
}
