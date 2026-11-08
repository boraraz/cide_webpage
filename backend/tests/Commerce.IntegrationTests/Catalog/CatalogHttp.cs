using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.IntegrationTests.Identity;

namespace Commerce.IntegrationTests.Catalog;

internal static class CatalogHttp
{
    public const string AdminEmail = "admin@example.com";

    public const string Password = "Password123";

    public static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    ];

    public static async Task<HttpClient> AdminClientAsync(CatalogApiFactory factory)
    {
        var client = factory.CreateClient();
        await AuthHttp.PrepareAsync(client);
        var response = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new { email = AdminEmail, password = Password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
    {
        await AuthHttp.PrepareAsync(client);
        return await client.PostAsJsonAsync(path, body);
    }

    public static async Task<HttpResponseMessage> PatchAsync(HttpClient client, string path, object body)
    {
        await AuthHttp.PrepareAsync(client);
        return await client.PatchAsJsonAsync(path, body);
    }

    public static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string path)
    {
        await AuthHttp.PrepareAsync(client);
        return await client.DeleteAsync(path);
    }

    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string path, byte[] bytes, string altText)
    {
        await AuthHttp.PrepareAsync(client);
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "photo.png");
        content.Add(new StringContent(altText), "altText");
        return await client.PostAsync(path, content);
    }

    public static string Token() => $"idem-{Guid.NewGuid():N}";
}
