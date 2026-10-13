static const float SrgbDecodeThreshold = 0.04045;
static const float SrgbEncodeThreshold = 0.0031308;
static const float SrgbLowSlope = 12.92;
static const float SrgbOffset = 0.055;
static const float SrgbScale = 1.055;
static const float SrgbExponent = 2.4;

float3 SrgbDecode(float3 encoded)
{
    float3 low = encoded / SrgbLowSlope;
    float3 high = pow((encoded + SrgbOffset) / SrgbScale, SrgbExponent);
    return encoded <= SrgbDecodeThreshold ? low : high;
}

float3 SrgbEncode(float3 linearColor)
{
    float3 low = linearColor * SrgbLowSlope;
    float3 high = SrgbScale * pow(linearColor, 1.0 / SrgbExponent) - SrgbOffset;
    return linearColor <= SrgbEncodeThreshold ? low : high;
}

float3 Unpremultiply(float4 premultiplied)
{
    return premultiplied.a > 0.0 ? saturate(premultiplied.rgb / premultiplied.a) : 0.0;
}

float4 DecodePremultiplied(float4 encoded)
{
    return float4(SrgbDecode(Unpremultiply(encoded)) * encoded.a, encoded.a);
}

float4 EncodePremultiplied(float4 linearColor)
{
    return float4(SrgbEncode(Unpremultiply(linearColor)) * linearColor.a, linearColor.a);
}
