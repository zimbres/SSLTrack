using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;

namespace SSLTrack.Tests;

public class KnownProxiesTests
{
    [Theory]
    [InlineData("10.42.0.1", "10.42.0.1", "10.42.0.2")]
    [InlineData("10.42.0.1/32", "10.42.0.1", "10.42.0.2")]
    [InlineData("10.42.0.0/16", "10.42.255.255", "10.43.0.0")]
    [InlineData("10.42.0.0/23", "10.42.1.255", "10.42.2.0")]
    [InlineData("10.42.0.0/31", "10.42.0.1", "10.42.0.2")]
    [InlineData("2001:db8::1", "2001:db8::1", "2001:db8::2")]
    [InlineData("2001:db8::1/128", "2001:db8::1", "2001:db8::2")]
    [InlineData("2001:db8::/48", "2001:db8:0:ffff::1", "2001:db8:1::1")]
    [InlineData("0.0.0.0/0", "203.0.113.1", "2001:db8::1")]
    [InlineData("::/0", "2001:db8::1", "203.0.113.1")]
    public void Configuration_trusts_only_addresses_in_configured_range(string entry, string included, string excluded)
    {
        WithApp(entry, app =>
        {
            var options = app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
            bool Trusted(string value)
            {
                var address = IPAddress.Parse(value);
                return options.KnownProxies.Contains(address) || options.KnownIPNetworks.Any(network => network.Contains(address));
            }

            Assert.True(Trusted(included));
            Assert.False(Trusted(excluded));
        });
    }

    [Theory]
    [InlineData("10.42.0.0/33")]
    [InlineData("2001:db8::/129")]
    [InlineData("10.42.0.0/-1")]
    [InlineData("10.42.0.0/abc")]
    [InlineData("10.42.0.0/16/24")]
    [InlineData("proxy.example.com")]
    [InlineData("")]
    public void Invalid_entries_fail_startup(string entry)
    {
        WithApp(entry, app => Assert.Throws<InvalidOperationException>(() => _ = app.Server));
    }

    private static void WithApp(string entry, Action<WebApplicationFactory<ApplicationDbContext>> action)
    {
        var file = Path.Combine(Path.GetTempPath(), $"ssltrack-proxies-{Guid.NewGuid()}.db");
        try
        {
            using var app = new WebApplicationFactory<ApplicationDbContext>().WithWebHostBuilder(builder =>
                builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Data"] = $"Data Source={file}",
                        ["ReverseProxy:KnownProxies:0"] = entry,
                        ["Authentication:Bootstrap:Email"] = "",
                        ["Authentication:Bootstrap:Password"] = ""
                    })));
            action(app);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }
}
