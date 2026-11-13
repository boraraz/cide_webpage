using Commerce.Api.Security;
using Microsoft.AspNetCore.HttpLogging;

namespace Commerce.UnitTests.Orders;

public class RequestLoggingTests
{
    [Fact]
    public void Request_logs_keep_method_path_status_and_duration()
    {
        var options = new HttpLoggingOptions();
        RequestLogging.Configure(options);

        Assert.True(options.LoggingFields.HasFlag(HttpLoggingFields.RequestMethod));
        Assert.True(options.LoggingFields.HasFlag(HttpLoggingFields.RequestPath));
        Assert.True(options.LoggingFields.HasFlag(HttpLoggingFields.ResponseStatusCode));
        Assert.True(options.LoggingFields.HasFlag(HttpLoggingFields.Duration));
        Assert.False(options.LoggingFields.HasFlag(HttpLoggingFields.RequestHeaders));
        Assert.False(options.LoggingFields.HasFlag(HttpLoggingFields.ResponseHeaders));
        Assert.False(options.LoggingFields.HasFlag(HttpLoggingFields.RequestQuery));
        Assert.False(options.LoggingFields.HasFlag(HttpLoggingFields.RequestBody));
        Assert.False(options.LoggingFields.HasFlag(HttpLoggingFields.ResponseBody));
    }
}
