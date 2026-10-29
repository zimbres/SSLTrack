using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SSLTrack.Tests;

public sealed class OidcCertificateTrustTests
{
    [Theory]
    [InlineData(false, "/missing/oidc-ca.pem")]
    [InlineData(true, " ")]
    public void Disabled_OIDC_or_blank_path_does_not_load_a_custom_handler(bool enabled, string path)
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions { EnvironmentName = "Testing" });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Oidc:Enabled"] = enabled.ToString(),
            ["Authentication:Oidc:TrustedCaPath"] = path,
            ["Authentication:Oidc:Authority"] = "https://provider.example",
            ["Authentication:Oidc:ClientId"] = "ssltrack"
        }).Build();
        builder.Services.AddAppAuthentication(configuration, builder.Environment);
        using var services = builder.Services.BuildServiceProvider();
        if (enabled)
        {
            var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>>().Get("oidc");
            Assert.Null(options.BackchannelHttpHandler);
            Assert.True(options.RequireHttpsMetadata);
        }
    }

    [Fact]
    public void Invalid_or_missing_CA_file_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => OidcCertificateTrust.CreateHandler(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pem")));
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not a certificate");
            Assert.Throws<InvalidOperationException>(() => OidcCertificateTrust.CreateHandler(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("localhost", false, false, true, false)]
    [InlineData("wrong.example", false, false, false, false)]
    [InlineData("localhost", true, false, false, false)]
    [InlineData("localhost", false, true, false, false)]
    [InlineData("localhost", false, false, true, true)]
    public async Task Backchannel_validates_CA_hostname_and_expiration(string hostname, bool expired, bool unrelatedCa, bool accepted, bool selfSigned)
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Test OIDC CA", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(10));
        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=" + hostname, leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(hostname);
        leafRequest.CertificateExtensions.Add(san.Build());
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
        using var publicLeaf = selfSigned
            ? leafRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(1))
            : leafRequest.Create(root, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(expired ? -1 : 1), RandomNumberGenerator.GetBytes(16));
        using var publicOnly = X509CertificateLoader.LoadCertificate(publicLeaf.RawData);
        using var keyedLeaf = publicOnly.CopyWithPrivateKey(leafKey);
        // Windows Schannel needs a persisted key for the local TLS server.
        using var leaf = X509CertificateLoader.LoadPkcs12(keyedLeaf.Export(X509ContentType.Pkcs12), null);
        using var otherKey = RSA.Create(2048);
        var otherRequest = new CertificateRequest("CN=Other CA", otherKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var other = otherRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var path = Path.GetTempFileName();
        File.WriteAllText(path, (unrelatedCa ? other : selfSigned ? publicLeaf : root).ExportCertificatePem());
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            using var tls = new SslStream(connection.GetStream());
            try
            {
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = leaf }, timeout.Token);
                using var reader = new StreamReader(tls, leaveOpen: true);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token))) { }
                await tls.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK"), timeout.Token);
            }
            catch (Exception e) when (e is System.Security.Authentication.AuthenticationException or IOException) { }
        });
        try
        {
            using var client = new HttpClient(OidcCertificateTrust.CreateHandler(path));
            var url = $"https://localhost:{((IPEndPoint)listener.LocalEndpoint).Port}/";
            if (accepted)
                Assert.Equal("OK", await client.GetStringAsync(url, timeout.Token));
            else
                await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync(url, timeout.Token));
            await server;
        }
        finally { File.Delete(path); }
    }
}
