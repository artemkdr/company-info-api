namespace CompanyInfo.Api.Shared.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection SetupCors(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddCors(options =>
        {
            // it parses the configuration 'AllowedOrigins' and adds them to the CORS policy
            // it must be a comma separated list of origins
            // e.g. "http://localhost:3000,http://example.com"
            options.AddDefaultPolicy(corsBuilder =>
            {
                var allowedOrigins = configuration["AllowedOrigins"]
                    ?.Split(",", StringSplitOptions.RemoveEmptyEntries);
                if (allowedOrigins?.Any() == true)
                {
                    corsBuilder.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
                }
            });
        });

        return services;
    }
}
