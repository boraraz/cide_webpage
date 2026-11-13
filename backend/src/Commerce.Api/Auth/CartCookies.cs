namespace Commerce.Api.Auth;

internal static class CartCookies
{
    public const string AccessHeader = "X-Order-Access";

    public static string Name(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Testing") ? "cide-cart" : "__Host-cide-cart";

    public static void Append(HttpResponse response, IHostEnvironment environment, string token, DateTimeOffset expires)
    {
        response.Cookies.Append(Name(environment), token, Options(environment, expires));
    }

    public static string? Read(HttpRequest request, IHostEnvironment environment) =>
        request.Cookies.TryGetValue(Name(environment), out var token) ? token : null;

    private static CookieOptions Options(IHostEnvironment environment, DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = !(environment.IsDevelopment() || environment.IsEnvironment("Testing")),
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires,
        IsEssential = true,
    };
}

internal static class ConfirmationCookies
{
    public static string Name(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Testing") ? "cide-confirm" : "__Host-cide-confirm";

    public static void Append(HttpResponse response, IHostEnvironment environment, string token, DateTimeOffset expires)
    {
        response.Cookies.Append(Name(environment), token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !(environment.IsDevelopment() || environment.IsEnvironment("Testing")),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expires,
            IsEssential = true,
        });
    }

    public static string? Read(HttpRequest request, IHostEnvironment environment) =>
        request.Cookies.TryGetValue(Name(environment), out var token) ? token : null;
}
