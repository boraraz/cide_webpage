namespace Commerce.Api.Auth;

internal static class SessionCookies
{
    public static string Name(IHostEnvironment environment) =>
        IsLocalHttp(environment) ? "cide-session" : "__Host-cide-session";

    public static void Append(HttpResponse response, IHostEnvironment environment, string token, DateTimeOffset expires)
    {
        response.Cookies.Append(Name(environment), token, Options(environment, expires));
    }

    public static void Delete(HttpResponse response, IHostEnvironment environment)
    {
        response.Cookies.Delete(Name(environment), Options(environment, expires: null));
    }

    public static string? Read(HttpRequest request, IHostEnvironment environment) =>
        request.Cookies.TryGetValue(Name(environment), out var token) ? token : null;

    private static bool IsLocalHttp(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Testing");

    private static CookieOptions Options(IHostEnvironment environment, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = !IsLocalHttp(environment),
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires,
        IsEssential = true,
    };
}
