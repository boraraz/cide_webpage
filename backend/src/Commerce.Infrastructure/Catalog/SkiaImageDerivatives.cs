using Commerce.Application.Catalog;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Commerce.Infrastructure.Catalog;

internal sealed partial class SkiaImageDerivatives(ILogger<SkiaImageDerivatives> logger) : IImageDerivatives
{
    public const int MaximumEdge = 8000;

    public const long MaximumPixels = 24_000_000;

    public IReadOnlyList<ImageDerivative> Create(ReadOnlyMemory<byte> content, int knownWidth)
    {
        if (knownWidth <= 0 || content.Length == 0)
        {
            return [];
        }

        try
        {
            using var data = SKData.CreateCopy(content.Span);
            using var codec = SKCodec.Create(data);
            if (codec is null)
            {
                LogRejected(logger, knownWidth, "decode");
                return [];
            }

            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || info.Width > MaximumEdge || info.Height > MaximumEdge
                || (long)info.Width * info.Height > MaximumPixels)
            {
                LogRejected(logger, knownWidth, "limits");
                return [];
            }

            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap is null)
            {
                LogRejected(logger, knownWidth, "decode");
                return [];
            }

            var limit = Math.Min(knownWidth, bitmap.Width);
            var images = new List<ImageDerivative>();
            foreach (var target in MediaDerivativePaths.Targets)
            {
                if (target > limit)
                {
                    continue;
                }

                try
                {
                    var height = Math.Max(1, (int)Math.Round(bitmap.Height * (target / (double)bitmap.Width)));
                    using var resized = bitmap.Resize(new SKImageInfo(target, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
                    if (resized is null)
                    {
                        LogRejected(logger, target, "resize");
                        continue;
                    }

                    using var encoded = SKWebpEncoder.Encode(
                        resized.PeekPixels(),
                        new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, 75));
                    if (encoded is null || encoded.Size == 0)
                    {
                        LogRejected(logger, target, "encode");
                        continue;
                    }

                    images.Add(new ImageDerivative(target, encoded.ToArray()));
                }
                catch (Exception exception)
                {
                    LogFailed(logger, exception, target);
                }
            }

            return images;
        }
        catch (Exception exception)
        {
            LogFailed(logger, exception, knownWidth);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image derivative {Width} was skipped ({Reason}).")]
    private static partial void LogRejected(ILogger logger, int width, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Image derivative {Width} failed.")]
    private static partial void LogFailed(ILogger logger, Exception exception, int width);
}
