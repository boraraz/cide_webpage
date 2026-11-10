using System.Text.Json;
using System.Text.Json.Nodes;

namespace Commerce.Domain.Content;

public static class HomepageDocument
{
    public const int MaxBlocks = 40;

    public const int MaxBytes = 64 * 1024;

    private static readonly HashSet<string> Types =
    [
        "hero",
        "featuredProducts",
        "featuredCollection",
        "editorial",
        "banner",
        "spacer",
    ];

    private static readonly JsonSerializerOptions Serializer = new() { WriteIndented = false };

    public static bool TryNormalize(
        string? json,
        out string normalized,
        out Dictionary<string, string[]> errors,
        out List<Guid> mediaIds)
    {
        normalized = "{}";
        errors = [];
        mediaIds = [];
        if (string.IsNullOrWhiteSpace(json) || System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes)
        {
            errors["configuration"] = ["Homepage content must be JSON of at most 64 KB."];
            return false;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            errors["configuration"] = ["Homepage content must be JSON."];
            return false;
        }

        if (root is not JsonObject document || document["blocks"] is not JsonArray blocks)
        {
            errors["blocks"] = ["Homepage content needs a blocks array."];
            return false;
        }

        if (blocks.Count > MaxBlocks)
        {
            errors["blocks"] = [$"Use at most {MaxBlocks} blocks."];
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < blocks.Count; index++)
        {
            if (blocks[index] is not JsonObject block)
            {
                errors[$"blocks[{index}]"] = ["Each block must be an object."];
                continue;
            }

            var id = block["id"]?.GetValue<string>();
            var type = block["type"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id) || id.Length > 80 || !ids.Add(id))
            {
                errors[$"blocks[{index}].id"] = ["Each block needs a unique id."];
            }

            if (type is null || !Types.Contains(type))
            {
                errors[$"blocks[{index}].type"] = ["Choose a supported block type."];
                continue;
            }

            try
            {
                _ = block["visible"]?.GetValue<bool>();
                if (block["visible"] is null)
                {
                    errors[$"blocks[{index}].visible"] = ["Each block needs a visible flag."];
                }
            }
            catch (InvalidOperationException)
            {
                errors[$"blocks[{index}].visible"] = ["Each block needs a visible flag."];
            }

            if (block["config"] is not JsonObject config)
            {
                errors[$"blocks[{index}].config"] = ["Each block needs a config object."];
                continue;
            }

            ValidateConfig(type, config, index, errors, mediaIds);
        }

        if (errors.Count > 0)
        {
            return false;
        }

        normalized = document.ToJsonString(Serializer);
        return true;
    }

    public static bool TryDestination(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("..", StringComparison.Ordinal) || value.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(value, "^/(?:[A-Za-z0-9\\-./]+)?$");
    }

    private static void ValidateConfig(
        string type,
        JsonObject config,
        int index,
        Dictionary<string, string[]> errors,
        List<Guid> mediaIds)
    {
        RequireText(config, "heading", index, errors, required: type is not "spacer");
        if (type is "editorial" or "banner")
        {
            RequireText(config, "body", index, errors, required: true);
        }

        if (config["destination"] is JsonNode destination)
        {
            var value = destination.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value) && !TryDestination(value))
            {
                errors[$"blocks[{index}].config.destination"] = ["Use a path on this site."];
            }
        }

        if (config["mediaId"] is JsonNode media)
        {
            if (!Guid.TryParse(media.GetValue<string>(), out var mediaId))
            {
                errors[$"blocks[{index}].config.mediaId"] = ["Choose an uploaded image."];
            }
            else
            {
                mediaIds.Add(mediaId);
            }
        }
        else if (type is "hero" or "banner" or "editorial")
        {
            errors[$"blocks[{index}].config.mediaId"] = ["Choose an uploaded image."];
        }

        if (type == "featuredProducts")
        {
            if (config["productIds"] is not JsonArray products || products.Count is 0 or > 12)
            {
                errors[$"blocks[{index}].config.productIds"] = ["Choose 1 to 12 products."];
            }
        }

        if (type == "featuredCollection" && !Guid.TryParse(config["collectionId"]?.GetValue<string>(), out _))
        {
            errors[$"blocks[{index}].config.collectionId"] = ["Choose a collection."];
        }

        if (type == "spacer" && config["size"]?.GetValue<string>() is not ("sm" or "md" or "lg"))
        {
            errors[$"blocks[{index}].config.size"] = ["Choose sm, md, or lg."];
        }
    }

    private static void RequireText(JsonObject config, string name, int index, Dictionary<string, string[]> errors, bool required)
    {
        if (!required && config[name] is null)
        {
            return;
        }

        var value = config[name]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 500 || value.Contains('<') || value.Contains('>'))
        {
            errors[$"blocks[{index}].config.{name}"] = ["Enter plain text up to 500 characters."];
        }
    }
}
