namespace SSLTrack.Security;

public static class OidcCertificateTrust
{
    public static SocketsHttpHandler CreateHandler(string path)
    {
        var certificates = new X509Certificate2Collection();
        try
        {
            certificates.ImportFromPemFile(path);
            if (certificates.Count == 0)
            {
                throw new CryptographicException("No PEM certificates were found.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            foreach (var certificate in certificates)
            {
                certificate.Dispose();
            }
            throw new InvalidOperationException("Authentication:Oidc:TrustedCaPath must point to a readable PEM certificate file.", ex);
        }

        // Replace system roots only for this OIDC backchannel. TLS still validates
        // the hostname, validity dates, server-auth usage and certificate chain.
        var policy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck
        };
        policy.CustomTrustStore.AddRange(certificates);
        return new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions { CertificateChainPolicy = policy }
        };
    }
}
