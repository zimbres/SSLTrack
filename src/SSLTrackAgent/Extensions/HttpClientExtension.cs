namespace SSLTrackAgent.Extensions;

public static class HttpClientExtension
{
    public static IServiceCollection AddConfiguredHttpClient(this IServiceCollection services, string clientName = "Default")
    {
        services.AddHttpClient(clientName)
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var config = sp.GetRequiredService<IOptions<HttpClientConfiguration>>().Value;

                var handler = new HttpClientHandler();

                if (config.UseProxy && !string.IsNullOrWhiteSpace(config.Address))
                {
                    handler.Proxy = new WebProxy($"{config.Address}:{config.Port}");
                    handler.UseProxy = true;
                }

                if (config.IgnoreSsl)
                {
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                }

                return handler;
            });

        return services;
    }

    public static void ApplyBasicAuth(this HttpClient client, string username, string password)
    {
        var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
    }
}
