using Asp.Versioning;

namespace CompanyInfo.Api.Shared.Extensions;

public static class ApiVersioningExtensions
{
    public static IServiceCollection SetupApiVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                // Default to v1 — callers that omit the version get v1 automatically.
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;

                // Report supported/deprecated versions in response headers
                // (api-supported-versions, api-deprecated-versions).
                options.ReportApiVersions = true;

                // Accept version from multiple sources (first match wins):
                //   1. URL segment  – /api/v1/…
                //   2. Query string – ?api-version=1
                //   3. Header       – api-version: 1
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new QueryStringApiVersionReader("api-version"),
                    new HeaderApiVersionReader("api-version")
                );
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "VV";

                // Replaces the {version:apiVersion} route token so Swagger/OpenAPI
                // shows concrete version paths (e.g. /api/v1/…).
                options.SubstituteApiVersionInUrl = true;

                // When there is only one version the explorer injects the default
                // so callers never need to think about versioning.
                options.AssumeDefaultVersionWhenUnspecified = true;
            });

        return services;
    }
}
