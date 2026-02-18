using ItemRecognition.Application.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace ItemRecognition.Infrastructure.Ai;

public static class GigaChatServiceCollectionExtensions
{
    public static IServiceCollection AddGigaChatAiVisionClient(
        this IServiceCollection services,
        Action<GigaChatOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new GigaChatOptions();
        configureOptions(options);

        services.AddSingleton(options);
        services.AddHttpClient<IAiVisionClient, GigaChatAiVisionClient>()
            .ConfigureHttpClient(client =>
            {
                var timeoutSeconds = options.RequestTimeoutSeconds <= 0 ? 100 : options.RequestTimeoutSeconds;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });

        return services;
    }
}
