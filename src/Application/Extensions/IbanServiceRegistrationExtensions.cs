using CompanyInfo.Api.Application.Features.Iban.Services;
using CompanyInfo.Api.Application.Features.Iban.Services.ExternalProviders;

namespace CompanyInfo.Api.Application.Extensions;

/// <summary>
/// Extension methods for registering IBAN-related services.
/// </summary>
/// <remarks>
/// <strong>DI Lifetime Strategy:</strong>
/// - Scoped Services: IIbanService, IExternalBicLookupService
///   (per-request services that depend on request context)
/// </remarks>
public static class IbanServiceRegistrationExtensions
{
    /// <summary>
    /// Registers IBAN validation and BIC lookup services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIbanServices(this IServiceCollection services)
    {
        // OpenIbanBicLookupService requires a typed HttpClient, which cannot be registered
        // via [RegisterService] attribute. Register it here with its named HttpClient,
        // then expose it as IExternalBicLookupService for DI resolution.
        services
            .AddHttpClient<OpenIbanBicLookupService>(
                (sp, client) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var baseUrl = config["ExternalApis:OpenIban:BaseUrl"] ?? "https://openiban.com";
                    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                    client.Timeout = TimeSpan.FromSeconds(5);
                }
            )
            .SetHandlerLifetime(TimeSpan.FromMinutes(10));

        services.AddScoped<IExternalBicLookupService>(sp =>
            sp.GetRequiredService<OpenIbanBicLookupService>()
        );

        return services;
    }
}
