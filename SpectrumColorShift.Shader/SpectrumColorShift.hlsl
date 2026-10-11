Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

static const int MaxSteps = 64;
static const int ModeRadial = 2;
static const int ModeRotational = 4;
static const float MaxRadialShift = 0.5;

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

float4 SampleCovered(float2 uv, float2 pixelStep, float2 scenePosition, float2 target)
{
    float2 minimum = inputBounds.xy + 0.5;
    float2 maximum = inputBounds.zw - 0.5;
    float2 clamped = clamp(target, minimum, maximum);
    float4 color = SourceTexture.SampleLevel(SourceSampler, uv + (clamped - scenePosition) * pixelStep, 0);
    float2 coverage = saturate(min(target - inputBounds.xy, inputBounds.zw - target) + 0.5);
    return color * (coverage.x * coverage.y);
}

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    float4 source = SampleCovered(uv0.xy, uv0.zw, scenePosition.xy, scenePosition.xy);
    if (shiftDistance == 0.0 || strength <= 0.0)
        return source;

    float2 offset = scenePosition.xy - center;
    float3 color = 0.0;
    float3 coverage = 0.0;

    [loop]
    for (int index = 0; index < steps; index++)
    {
        float4 entry = samples[index];
        float4 shifted = SampleCovered(uv0.xy, uv0.zw, scenePosition.xy, SamplePosition(scenePosition.xy, offset, entry.w));
        color += entry.rgb * shifted.rgb;
        coverage += entry.rgb * shifted.a;
    }

    float alpha = max(coverage.r, max(coverage.g, coverage.b));
    float4 result = float4(min(color, alpha), alpha);
    return lerp(source, result, strength);
}
