using System.Net;
using Commerce.Api.Security;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Commerce.UnitTests.Orders;

public sealed class ProductionSettingsTests
{
    [Fact]
    public void Development_accepts_local_defaults()
    {
        var ok = ProductionSettings.IsAcceptable(new StubEnvironment("Development"), new ConfigurationBuilder().Build(), out var error);
        Assert.True(ok);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Production_rejects_localhost_and_accepts_same_origin_https()
    {
        var rejected = ProductionSettings.IsAcceptable(new StubEnvironment("Production"), Config(("AllowedHosts", "localhost")), out var error);
        Assert.False(rejected);
        Assert.Contains("AllowedHosts", error, StringComparison.Ordinal);

        var values = new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "shop.example",
            ["PublicUrls:StorefrontUrl"] = "https://shop.example",
            ["PublicUrls:Mode"] = "SameOrigin",
            ["ConnectionStrings:Database"] = "Host=db;Database=shop",
            ["ObjectStorage:AccessKey"] = "key",
            ["ObjectStorage:SecretKey"] = "secret",
            ["ObjectStorage:BucketName"] = "media",
            ["ObjectStorage:ServiceUrl"] = "http://storage:9000",
            ["Smtp:Host"] = "smtp.example",
            ["Commerce:ShippingFee"] = "149",
            ["Commerce:FreeShippingThreshold"] = "2500",
            ["Identity:BootstrapAdmin"] = "false",
        };
        Assert.True(ProductionSettings.IsAcceptable(new StubEnvironment("Production"), Config(values), out _));
    }

    [Fact]
    public void Split_hosts_on_different_sites_are_rejected()
    {
        var values = new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "shop.example",
            ["PublicUrls:StorefrontUrl"] = "https://shop.example",
            ["PublicUrls:AdminUrl"] = "https://admin.other",
            ["PublicUrls:ApiPublicUrl"] = "https://api.other",
            ["PublicUrls:Mode"] = "SplitHosts",
            ["ConnectionStrings:Database"] = "Host=db;Database=shop",
            ["ObjectStorage:AccessKey"] = "key",
            ["ObjectStorage:SecretKey"] = "secret",
            ["ObjectStorage:BucketName"] = "media",
            ["ObjectStorage:ServiceUrl"] = "http://storage:9000",
            ["Smtp:Host"] = "smtp.example",
            ["Commerce:ShippingFee"] = "149",
            ["Commerce:FreeShippingThreshold"] = "2500",
        };
        Assert.False(ProductionSettings.IsAcceptable(new StubEnvironment("Production"), Config(values), out var error));
        Assert.Contains("registrable domain", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Untrusted_peer_cannot_supply_the_client_ip()
    {
        var ignored = await Forward(proxies: [], peer: "203.0.113.10");
        Assert.Equal("203.0.113.10", ignored.Connection.RemoteIpAddress!.ToString());

        var untrusted = await Forward(proxies: ["192.0.2.10"], peer: "203.0.113.10");
        Assert.Equal("203.0.113.10", untrusted.Connection.RemoteIpAddress!.ToString());

        var trusted = await Forward(proxies: ["203.0.113.10"], peer: "203.0.113.10");
        Assert.Equal("198.51.100.9", trusted.Connection.RemoteIpAddress!.ToString());
    }

    private static async Task<DefaultHttpContext> Forward(IReadOnlyList<string> proxies, string peer)
    {
        var options = new ForwardedHeadersOptions();
        options.KnownProxies.Add(IPAddress.Loopback);
        SecurityHeaders.ApplyForwarding(options, proxies);
        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(options));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.9";
        await middleware.Invoke(context);
        return context;
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(item => new KeyValuePair<string, string?>(item.Key, item.Value))).Build();

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class StubEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Commerce.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
