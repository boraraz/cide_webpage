using System.Net.Http.Json;

namespace Commerce.IntegrationTests.Identity;

internal static class AuthHttp
{
    public const string Password = "Password123";

    public static async Task PrepareAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/csrf");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CsrfBody>();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", body!.Token);
    }

    public static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string password = Password)
    {
        await PrepareAsync(client);
        return await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email,
            password,
            passwordConfirmation = password,
        });
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = Password, string path = "/api/v1/auth/login")
    {
        return SendAsync(client, path, new { email, password });
    }

    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, object body)
    {
        await PrepareAsync(client);
        return await client.PostAsJsonAsync(path, body);
    }

    public static string EmailAddress() => $"user-{Guid.NewGuid():N}@example.com";

    private sealed record CsrfBody(string Token);
}
