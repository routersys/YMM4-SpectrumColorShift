#ifndef StepCount
#error StepCount must be defined by the build.
#endif

#ifndef ShaderMode
#error ShaderMode must be defined by the build.
#endif

#include "Srgb.hlsli"

Texture2D LinearTexture : register(t1);
SamplerState LinearSampler : register(s1);

static const int MaxSteps = 64;
static const int TableSize = 128;
static const int ModeRadial = 2;
static const int ModeRotational = 4;
static const int CompiledMode = ShaderMode;
static const float MaxRadialShift = 0.5;
static const float InteriorGuard = 0.51;

cbuffer Constants : register(b0)
{
    float4 inputBounds : packoffset(c0);
    float2 direction : packoffset(c1.x);
    float2 center : packoffset(c1.z);
    float shiftDistance : packoffset(c2.x);
    float radius : packoffset(c2.y);
    float strength : packoffset(c2.z);
    int mode : packoffset(c2.w);
    int steps : packoffset(c3.x);
    float spreadScale : packoffset(c3.y);
    float4 samples[TableSize] : packoffset(c4);
};

float2 SamplePosition(float2 scenePosition, float2 offset, float coefficient)
{
    float shift = coefficient * shiftDistance;

    if (CompiledMode == ModeRadial)
    {
        float scale = 1.0 - clamp(shift / radius, -MaxRadialShift, MaxRadialShift);
        return center + offset * scale;
    }

    if (CompiledMode == ModeRotational)
    {
        float sine;
        float cosine;
        sincos(shift / radius, sine, cosine);
        return center + float2(cosine * offset.x + sine * offset.y, cosine * offset.y - sine * offset.x);
    }

    return scenePosition - shift * direction;
}

float2 Spread(float2 offset)
{
    if (CompiledMode == ModeRadial)
        return abs(offset) * spreadScale;

    if (CompiledMode == ModeRotational)
        return length(offset) * spreadScale;

    return abs(shiftDistance) * abs(direction);
}

float4 SampleCovered(float2 uv, float2 pixelStep, float2 scenePosition, float2 target)
{
    float2 minimum = inputBounds.xy + 0.5;
    float2 maximum = inputBounds.zw - 0.5;
    float2 clamped = clamp(target, minimum, maximum);
    float4 color = LinearTexture.SampleLevel(LinearSampler, uv + (clamped - scenePosition) * pixelStep, 0);
    float2 coverage = saturate(min(target - inputBounds.xy, inputBounds.zw - target) + 0.5);
    return color * (coverage.x * coverage.y);
}

void Accumulate(float4 gain, float3 basis, float4 shifted, inout float3 color, inout float alpha)
{
    color += gain.rgb * dot(basis, shifted.rgb);
    alpha += gain.w * shifted.a;
}

float4 Resolve(float3 color, float alpha)
{
    float3 positive = max(color, 0.0);
    float resolved = saturate(max(alpha, max(positive.r, max(positive.g, positive.b))));
    return EncodePremultiplied(float4(positive, resolved));
}

void AccumulateCovered(int index, float2 uv, float2 pixelStep, float2 scenePosition, float2 offset, inout float3 color, inout float alpha)
{
    float4 gain = samples[index * 2];
    float4 basis = samples[index * 2 + 1];
    Accumulate(gain, basis.rgb, SampleCovered(uv, pixelStep, scenePosition, SamplePosition(scenePosition, offset, basis.w)), color, alpha);
}

void AccumulateRadial(int index, float2 uv, float2 offsetStep, float rate, inout float3 color, inout float alpha)
{
    float4 gain = samples[index * 2];
    float4 basis = samples[index * 2 + 1];
    float scale = clamp(basis.w * rate, -MaxRadialShift, MaxRadialShift);
    Accumulate(gain, basis.rgb, LinearTexture.SampleLevel(LinearSampler, uv - offsetStep * scale, 0), color, alpha);
}

void AccumulateRotational(int index, float2 uv, float2 offset, float2 pixelStep, float rate, inout float3 color, inout float alpha)
{
    float4 gain = samples[index * 2];
    float4 basis = samples[index * 2 + 1];
    float sine;
    float cosine;
    sincos(basis.w * rate, sine, cosine);
    float2 delta = float2((cosine - 1.0) * offset.x + sine * offset.y, (cosine - 1.0) * offset.y - sine * offset.x);
    Accumulate(gain, basis.rgb, LinearTexture.SampleLevel(LinearSampler, uv + delta * pixelStep, 0), color, alpha);
}

void AccumulateLinear(int index, float2 uv, float2 stride, inout float3 color, inout float alpha)
{
    float4 gain = samples[index * 2];
    float4 basis = samples[index * 2 + 1];
    Accumulate(gain, basis.rgb, LinearTexture.SampleLevel(LinearSampler, uv - basis.w * stride, 0), color, alpha);
}

float4 GatherCovered(float2 uv, float2 pixelStep, float2 scenePosition)
{
    float2 offset = scenePosition - center;
    float3 color = 0.0;
    float alpha = 0.0;

    [loop]
    for (int index = 0; index < steps; index++)
        AccumulateCovered(index, uv, pixelStep, scenePosition, offset, color, alpha);

    return Resolve(color, alpha);
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

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0,
    float4 uv1 : TEXCOORD1
) : SV_TARGET
{
    if (shiftDistance == 0.0 || strength <= 0.0)
        return EncodePremultiplied(SampleCovered(uv1.xy, uv1.zw, scenePosition.xy, scenePosition.xy));

    float2 margin = Spread(scenePosition.xy - center) + InteriorGuard;
    bool inside = all(scenePosition.xy >= inputBounds.xy + margin) && all(scenePosition.xy <= inputBounds.zw - margin);

    float4 result;
    [branch]
    if (inside)
        result = GatherInside(uv1.xy, uv1.zw, scenePosition.xy);
    else
        result = GatherCovered(uv1.xy, uv1.zw, scenePosition.xy);

    if (strength >= 1.0)
        return result;

    return lerp(EncodePremultiplied(SampleCovered(uv1.xy, uv1.zw, scenePosition.xy, scenePosition.xy)), result, strength);
}
