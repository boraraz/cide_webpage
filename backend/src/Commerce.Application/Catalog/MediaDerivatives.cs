using System.Text.Json;

namespace Commerce.Application.Catalog;

public sealed record ImageDerivative(int Width, byte[] Content);

public interface IImageDerivatives
{
    IReadOnlyList<ImageDerivative> Create(ReadOnlyMemory<byte> content, int knownWidth);
}

public static class StockPresentation
{
    public static string Label(int available, int threshold) =>
        available <= 0 ? "out_of_stock" : available <= Math.Max(0, threshold) ? "limited" : "in_stock";
}

public static class MediaDerivativePaths
{
    public const string PublicCache = "public, max-age=86400, immutable";

    public const string ShortCache = "public, max-age=300";

    public static readonly int[] Targets = [480, 960, 1600];

    public static bool IsTarget(int width) => width is 480 or 960 or 1600;

    public static string SiblingKey(string objectKey, int width)
    {
        var (directory, name) = Split(objectKey);
        return $"{directory}/{name}/w{width}.webp";
    }

    public static string IndexKey(string objectKey)
    {
        var (directory, name) = Split(objectKey);
        return $"{directory}/{name}/sources.json";
    }

    public static IReadOnlyList<MediaSource> Sources(
        string path,
        string contentType,
        int? width,
        IReadOnlyList<int> derivativeWidths)
    {
        var sources = new List<MediaSource> { new(path, width, contentType) };
        if (width is not int known || known <= 0)
        {
            return sources;
        }

        foreach (var candidate in derivativeWidths.Distinct().Order())
        {
            if (IsTarget(candidate) && candidate <= known)
            {
                sources.Add(new MediaSource($"{path}?w={candidate}", candidate, "image/webp"));
            }
        }

        return sources;
    }

    private static (string Directory, string Name) Split(string objectKey)
    {
        var slash = objectKey.LastIndexOf('/');
        var dot = objectKey.LastIndexOf('.');
        if (slash < 0 || dot <= slash)
        {
            throw new ArgumentException("Object key must include a directory and an extension.", nameof(objectKey));
        }

        return (objectKey[..slash], objectKey[(slash + 1)..dot]);
    }
}

public static class DerivativeIndex
{
    public static byte[] Write(IReadOnlyList<int> widths) =>
        JsonSerializer.SerializeToUtf8Bytes(new Payload(widths.ToArray()));

    public static IReadOnlyList<int> Read(ReadOnlySpan<byte> content)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(content);
            return payload?.Widths?.Where(MediaDerivativePaths.IsTarget).Distinct().Order().ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record Payload(int[]? Widths);
}
