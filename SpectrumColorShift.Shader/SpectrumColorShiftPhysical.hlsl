Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

static const int MaxSteps = 64;
static const int TableSize = 128;
static const int Unroll = 4;
static const int ModeRadial = 2;
static const int ModeRotational = 4;
static const float MaxRadialShift = 0.5;
static const float InteriorGuard = 0.51;
static const float MinimumAlpha = 0.0001;

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

float4 Linearize(float4 color)
{
    float3 clamped = min(color.rgb, color.a);
    return float4(clamped * clamped * rcp(max(color.a, MinimumAlpha)), color.a);
}

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

void Accumulate(float4 gain, float3 basis, float4 shifted, inout float3 color, inout float alpha)
{
    float4 lit = Linearize(shifted);
    color += gain.rgb * dot(basis, lit.rgb);
    alpha += gain.w * lit.a;
}

float4 Resolve(float3 color, float alpha)
{
    float3 positive = max(color, 0.0);
    float resolved = saturate(max(alpha, max(positive.r, max(positive.g, positive.b))));
    return float4(sqrt(min(positive, resolved) * resolved), resolved);
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
    Accumulate(gain, basis.rgb, SourceTexture.SampleLevel(SourceSampler, uv - offsetStep * scale, 0), color, alpha);
}

void AccumulateRotational(int index, float2 uv, float2 offset, float2 pixelStep, float rate, inout float3 color, inout float alpha)
{
    float4 gain = samples[index * 2];
    float4 basis = samples[index * 2 + 1];
    float sine;
    float cosine;
    sincos(basis.w * rate, sine, cosine);
    float2 delta = float2((cosine - 1.0) * offset.x + sine * offset.y, (cosine - 1.0) * offset.y - sine * offset.x);
    Accumulate(gain, basis.rgb, SourceTexture.SampleLevel(SourceSampler, uv + delta * pixelStep, 0), color, alpha);
}

void AccumulateLinear(int index, float2 uv, float2 stride, inout float3 color, inout float alpha)
{
    float4 gain = samples[index * 2];
    float4 basis = samples[index * 2 + 1];
    Accumulate(gain, basis.rgb, SourceTexture.SampleLevel(SourceSampler, uv - basis.w * stride, 0), color, alpha);
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

    if (mode == ModeRadial)
    {
        float2 offsetStep = offset * pixelStep;
        float rate = shiftDistance / radius;

        [loop]
        for (int index = 0; index < steps; index += Unroll)
        {
            AccumulateRadial(index, uv, offsetStep, rate, color, alpha);
            AccumulateRadial(index + 1, uv, offsetStep, rate, color, alpha);
            AccumulateRadial(index + 2, uv, offsetStep, rate, color, alpha);
            AccumulateRadial(index + 3, uv, offsetStep, rate, color, alpha);
        }
    }
    else if (mode == ModeRotational)
    {
        float rate = shiftDistance / radius;

        [loop]
        for (int index = 0; index < steps; index += Unroll)
        {
            AccumulateRotational(index, uv, offset, pixelStep, rate, color, alpha);
            AccumulateRotational(index + 1, uv, offset, pixelStep, rate, color, alpha);
            AccumulateRotational(index + 2, uv, offset, pixelStep, rate, color, alpha);
            AccumulateRotational(index + 3, uv, offset, pixelStep, rate, color, alpha);
        }
    }
    else
    {
        float2 stride = shiftDistance * direction * pixelStep;

        [loop]
        for (int index = 0; index < steps; index += Unroll)
        {
            AccumulateLinear(index, uv, stride, color, alpha);
            AccumulateLinear(index + 1, uv, stride, color, alpha);
            AccumulateLinear(index + 2, uv, stride, color, alpha);
            AccumulateLinear(index + 3, uv, stride, color, alpha);
        }
    }

    return Resolve(color, alpha);
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
