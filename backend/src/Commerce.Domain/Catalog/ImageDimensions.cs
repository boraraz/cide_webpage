namespace Commerce.Domain.Catalog;

public readonly record struct ImageDimensions(int Width, int Height);

public static class ImageHeader
{
    public static ImageDimensions? Read(ReadOnlySpan<byte> content, string contentType)
    {
        try
        {
            return contentType switch
            {
                "image/png" => ReadPng(content),
                "image/jpeg" => ReadJpeg(content),
                "image/webp" => ReadWebp(content),
                _ => null,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static ImageDimensions? ReadPng(ReadOnlySpan<byte> content)
    {
        if (content.Length < 24)
        {
            return null;
        }

        var width = ReadInt32Big(content[16..]);
        var height = ReadInt32Big(content[20..]);
        return width > 0 && height > 0 ? new ImageDimensions(width, height) : null;
    }

    private static ImageDimensions? ReadJpeg(ReadOnlySpan<byte> content)
    {
        var index = 2;
        while (index + 8 < content.Length)
        {
            if (content[index] != 0xFF)
            {
                index++;
                continue;
            }

            var marker = content[index + 1];
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                var height = ReadInt16Big(content[(index + 5)..]);
                var width = ReadInt16Big(content[(index + 7)..]);
                return width > 0 && height > 0 ? new ImageDimensions(width, height) : null;
            }

            if (marker is 0xD8 or 0xD9)
            {
                index += 2;
                continue;
            }

            var length = ReadInt16Big(content[(index + 2)..]);
            if (length < 2)
            {
                return null;
            }

            index += 2 + length;
        }

        return null;
    }

    private static ImageDimensions? ReadWebp(ReadOnlySpan<byte> content)
    {
        if (content.Length < 30)
        {
            return null;
        }

        if (content[12] == (byte)'V' && content[13] == (byte)'P' && content[14] == (byte)'8' && content[15] == (byte)'X')
        {
            var width = 1 + (content[24] | (content[25] << 8) | ((content[26] & 0x3F) << 16));
            var height = 1 + (content[27] | (content[28] << 8) | ((content[29] & 0x3F) << 16));
            return new ImageDimensions(width, height);
        }

        if (content[12] == (byte)'V' && content[13] == (byte)'P' && content[14] == (byte)'8' && content[15] == (byte)' ')
        {
            if (content.Length < 30)
            {
                return null;
            }

            var width = content[26] | (content[27] << 8);
            var height = content[28] | (content[29] << 8);
            return width > 0 && height > 0 ? new ImageDimensions(width & 0x3FFF, height & 0x3FFF) : null;
        }

        return null;
    }

    private static int ReadInt32Big(ReadOnlySpan<byte> content) =>
        (content[0] << 24) | (content[1] << 16) | (content[2] << 8) | content[3];

    private static int ReadInt16Big(ReadOnlySpan<byte> content) => (content[0] << 8) | content[1];
}
