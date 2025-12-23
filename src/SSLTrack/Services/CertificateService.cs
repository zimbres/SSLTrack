namespace SSLTrack.Services;

public class CertificateService
{
    private readonly ILogger<CertificateService> _logger;
    private readonly RemoteCertificateValidationCallback _remoteCertificate = (_, _, _, _) => true;
    private readonly LogService _logService;

    public CertificateService(ILogger<CertificateService> logger, LogService logService)
    {
        _logger = logger;
        _logService = logService;
    }

    public async Task<X509Certificate2> GetCertificateAsync(string domain, int port = 443)
    {
        try
        {
            using var client = new TcpClient(domain, port);
            using var sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: true, _remoteCertificate);
            await sslStream.AuthenticateAsClientAsync(domain).ConfigureAwait(false);
            var serverCertificate = sslStream.RemoteCertificate;
            if (serverCertificate != null)
            {
                return new X509Certificate2(serverCertificate);
            }
        }
        catch (Exception ex)
        {
            await _logService.PushLog(ex.Message, domain);
            _logger.LogError("Error on connection to address {address} on port {port}. {ex}", domain, port, ex.Message);
        }
        return null!;
    }
}
