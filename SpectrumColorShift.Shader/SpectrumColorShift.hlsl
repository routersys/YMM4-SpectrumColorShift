#ifndef StepCount
#error StepCount must be defined by the build.
#endif

Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

static const int MaxSteps = 64;
static const int ModeRadial = 2;
static const int ModeRotational = 4;
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
    float4 samples[MaxSteps] : packoffset(c4);
};

float2 SamplePosition(float2 scenePosition, float2 offset, float coefficient)
{
    float shift = coefficient * shiftDistance;

    if (mode == ModeRadial)
    {
        float scale = 1.0 - clamp(shift / radius, -MaxRadialShift, MaxRadialShift);
        return center + offset * scale;
    }

    if (mode == ModeRotational)
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
    if (mode == ModeRadial)
        return abs(offset) * spreadScale;

    if (mode == ModeRotational)
        return length(offset) * spreadScale;

    return abs(shiftDistance) * abs(direction);
}

float4 SampleCovered(float2 uv, float2 pixelStep, float2 scenePosition, float2 target)
{
    float2 minimum = inputBounds.xy + 0.5;
    float2 maximum = inputBounds.zw - 0.5;
    float2 clamped = clamp(target, minimum, maximum);
    float4 color = SourceTexture.SampleLevel(SourceSampler, uv + (clamped - scenePosition) * pixelStep, 0);
    float2 coverage = saturate(min(target - inputBounds.xy, inputBounds.zw - target) + 0.5);
    return color * (coverage.x * coverage.y);
}

void Accumulate(float4 entry, float4 shifted, inout float3 color, inout float3 coverage)
{
    color += entry.rgb * shifted.rgb;
    coverage += entry.rgb * shifted.a;
}

void AccumulateRadial(int index, float2 uv, float2 offsetStep, float rate, inout float3 color, inout float3 coverage)
{
    float4 entry = samples[index];
    float scale = clamp(entry.w * rate, -MaxRadialShift, MaxRadialShift);
    Accumulate(entry, SourceTexture.SampleLevel(SourceSampler, uv - offsetStep * scale, 0), color, coverage);
}

void AccumulateRotational(int index, float2 uv, float2 offset, float2 pixelStep, float rate, inout float3 color, inout float3 coverage)
{
    float4 entry = samples[index];
    float sine;
    float cosine;
    sincos(entry.w * rate, sine, cosine);
    float2 delta = float2((cosine - 1.0) * offset.x + sine * offset.y, (cosine - 1.0) * offset.y - sine * offset.x);
    Accumulate(entry, SourceTexture.SampleLevel(SourceSampler, uv + delta * pixelStep, 0), color, coverage);
}

void AccumulateLinear(int index, float2 uv, float2 stride, inout float3 color, inout float3 coverage)
{
    float4 entry = samples[index];
    Accumulate(entry, SourceTexture.SampleLevel(SourceSampler, uv - entry.w * stride, 0), color, coverage);
}

float4 Resolve(float3 color, float3 coverage)
{
    float alpha = max(coverage.r, max(coverage.g, coverage.b));
    return float4(min(color, alpha), alpha);
}

float4 GatherCovered(float2 uv, float2 pixelStep, float2 scenePosition)
{
    float2 offset = scenePosition - center;
    float3 color = 0.0;
    float3 coverage = 0.0;

    [loop]
    for (int index = 0; index < steps; index++)
    {
        float4 entry = samples[index];
        Accumulate(entry, SampleCovered(uv, pixelStep, scenePosition, SamplePosition(scenePosition, offset, entry.w)), color, coverage);
    }

    return Resolve(color, coverage);
}

float4 GatherInside(float2 uv, float2 pixelStep, float2 scenePosition)
{
    float2 offset = scenePosition - center;
    float3 color = 0.0;
    float3 coverage = 0.0;

    if (mode == ModeRadial)
    {
        float2 offsetStep = offset * pixelStep;
        float rate = shiftDistance / radius;

        [unroll]
        for (int index = 0; index < StepCount; index++)
            AccumulateRadial(index, uv, offsetStep, rate, color, coverage);
    }
    else if (mode == ModeRotational)
    {
        float rate = shiftDistance / radius;

        [unroll]
        for (int index = 0; index < StepCount; index++)
            AccumulateRotational(index, uv, offset, pixelStep, rate, color, coverage);
    }
    else
    {
        float2 stride = shiftDistance * direction * pixelStep;

        [unroll]
        for (int index = 0; index < StepCount; index++)
            AccumulateLinear(index, uv, stride, color, coverage);
    }

    return Resolve(color, coverage);
}

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    if (shiftDistance == 0.0 || strength <= 0.0)
        return SampleCovered(uv0.xy, uv0.zw, scenePosition.xy, scenePosition.xy);

    float2 margin = Spread(scenePosition.xy - center) + InteriorGuard;
    bool inside = all(scenePosition.xy >= inputBounds.xy + margin) && all(scenePosition.xy <= inputBounds.zw - margin);

    float4 result;
    [branch]
    if (inside)
        result = GatherInside(uv0.xy, uv0.zw, scenePosition.xy);
    else
        result = GatherCovered(uv0.xy, uv0.zw, scenePosition.xy);

    if (strength >= 1.0)
        return result;

    return lerp(SampleCovered(uv0.xy, uv0.zw, scenePosition.xy, scenePosition.xy), result, strength);
}
