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

    public static async Task ApplyAuthAsync(this HttpClient client, AuthService authService)
    {
        var header = await authService.GetAuthHeaderAsync();
        if (header != null)
        {
            client.DefaultRequestHeaders.Authorization = header;
        }
    }
}
