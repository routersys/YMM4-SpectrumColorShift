#include "Srgb.hlsli"

Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    return DecodePremultiplied(SourceTexture.SampleLevel(SourceSampler, uv0.xy, 0));
}
