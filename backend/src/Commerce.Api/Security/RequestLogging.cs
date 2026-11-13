using Microsoft.AspNetCore.HttpLogging;

namespace Commerce.Api.Security;

public static class RequestLogging
{
    public static void Configure(HttpLoggingOptions options)
    {
        options.LoggingFields = HttpLoggingFields.RequestMethod
            | HttpLoggingFields.RequestPath
            | HttpLoggingFields.ResponseStatusCode
            | HttpLoggingFields.Duration;
        options.CombineLogs = true;
    }
}
