using SkiaSharp;

namespace MiMascota.Web.Services;

/// <summary>
/// Fully decodes an uploaded photo and re-encodes it as WebP: a corrupt or truncated file is rejected,
/// the camera orientation is applied, big photos are scaled down, and all metadata (EXIF, GPS location
/// from phone cameras) is dropped because only the pixels are written back.
/// </summary>
public static class PhotoNormalizer
{
    public const int MaxSide = 1000;
    public const int WebpQuality = 82;

    /// <summary>The WebP bytes, or null if the data is not a complete, decodable image.</summary>
    public static byte[]? ToWebp(byte[] data, int maxSide = MaxSide)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(data));
        if (codec is null) return null;

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        if (info.Width <= 0 || info.Height <= 0) return null;

        using var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success) return null;

        // Size of the (scaled) source, then of the output once the orientation is applied.
        var scale = Math.Min(1.0, (double)maxSide / Math.Max(info.Width, info.Height));
        var sw = Math.Max(1, (int)Math.Round(info.Width * scale));
        var sh = Math.Max(1, (int)Math.Round(info.Height * scale));
        var origin = codec.EncodedOrigin;
        var swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                           or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (ow, oh) = swaps ? (sh, sw) : (sw, sh);

        using var surface = SKSurface.Create(new SKImageInfo(ow, oh, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface is null) return null;
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(OrientationMatrix(origin, sw, sh));

        using var image = SKImage.FromBitmap(bitmap);
        canvas.DrawImage(image, new SKRect(0, 0, sw, sh), new SKSamplingOptions(SKCubicResampler.Mitchell));
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Webp, WebpQuality);
        return encoded?.ToArray();
    }

    // x' = scaleX*x + skewX*y + transX ; y' = skewY*x + scaleY*y + transY
    private static SKMatrix OrientationMatrix(SKEncodedOrigin origin, float w, float h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),     // mirror horizontally
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), // rotate 180°
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),   // mirror vertically
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),       // transpose
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),     // rotate 90° clockwise
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), // transverse
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),   // rotate 270° clockwise
        _ => SKMatrix.Identity
    };
}
