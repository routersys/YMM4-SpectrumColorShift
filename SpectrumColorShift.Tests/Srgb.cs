using System.Numerics;

namespace SpectrumColorShift.Tests;

internal static class Srgb
{
    public static double Decode(double encoded)
        => encoded <= 0.04045 ? encoded / 12.92 : Math.Pow((encoded + 0.055) / 1.055, 2.4);

    public static double Encode(double linear)
        => linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(linear, 1d / 2.4) - 0.055;

    public static Vector3 Decode(Vector3 encoded)
        => new((float)Decode(encoded.X), (float)Decode(encoded.Y), (float)Decode(encoded.Z));

    public static Vector3 Encode(Vector3 linear)
        => new((float)Encode(linear.X), (float)Encode(linear.Y), (float)Encode(linear.Z));

    public static Vector4 DecodePremultiplied(Vector4 encoded)
        => encoded.W <= 0f ? Vector4.Zero : new Vector4(Decode(Straight(encoded)) * encoded.W, encoded.W);

    public static Vector4 EncodePremultiplied(Vector4 linear)
        => linear.W <= 0f ? Vector4.Zero : new Vector4(Encode(Straight(linear)) * linear.W, linear.W);

    static Vector3 Straight(Vector4 premultiplied)
        => Vector3.Clamp(new Vector3(premultiplied.X, premultiplied.Y, premultiplied.Z) / premultiplied.W, Vector3.Zero, Vector3.One);
}
