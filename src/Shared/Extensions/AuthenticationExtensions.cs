using CompanyInfo.Api.Shared.Security;

namespace CompanyInfo.Api.Shared.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddCustomAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Configure API Key Authentication
        // Add API key authentication
        services
            .AddAuthentication("ApiKeyScheme")
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                "ApiKeyScheme",
                options => { }
            );

        return services;
    }
}
