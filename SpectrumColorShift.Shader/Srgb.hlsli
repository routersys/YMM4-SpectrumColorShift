static const float DecodeThreshold = 0.04045;
static const float EncodeThreshold = 0.0031308;
static const float LowSlope = 12.92;
static const float Offset = 0.055;
static const float Scale = 1.055;
static const float Exponent = 2.4;

float3 Decode(float3 encoded)
{
    float3 low = encoded / LowSlope;
    float3 high = pow((encoded + Offset) / Scale, Exponent);
    return encoded <= DecodeThreshold ? low : high;
}

float3 Encode(float3 linearColor)
{
    float3 low = linearColor * LowSlope;
    float3 high = Scale * pow(linearColor, 1.0 / Exponent) - Offset;
    return linearColor <= EncodeThreshold ? low : high;
}

float3 Unpremultiply(float4 premultiplied)
{
    return premultiplied.a > 0.0 ? saturate(premultiplied.rgb / premultiplied.a) : 0.0;
}

float4 DecodePremultiplied(float4 encoded)
{
    return float4(Decode(Unpremultiply(encoded)) * encoded.a, encoded.a);
}

float4 EncodePremultiplied(float4 linearColor)
{
    return float4(Encode(Unpremultiply(linearColor)) * linearColor.a, linearColor.a);
}
