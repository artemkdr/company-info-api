using System.Text.Json;
using Asp.Versioning.ApiExplorer;
using CompanyInfo.Api.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CompanyInfo.Api.Shared.Extensions;

public static class SwaggerExtensions
{
    public static WebApplication SetupSwagger(this WebApplication app, IConfiguration configuration)
    {
        var swaggerUrlPrefix = configuration.GetValue<string>("SwaggerUrlPrefix");
        app.UseSwagger(c =>
        {
            if (swaggerUrlPrefix != null)
            {
                c.PreSerializeFilters.Add(
                    (swaggerDoc, httpReq) =>
                    {
                        var paths = new OpenApiPaths();
                        foreach (var path in swaggerDoc.Paths)
                        {
                            paths.Add((swaggerUrlPrefix + path.Key).Replace("//", "/"), path.Value);
                        }
                        swaggerDoc.Paths = paths;
                    }
                );
            }
        });
        app.UseSwaggerUI(options =>
        {
            var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            // add swagger endpoints jsons for each discovered API version
            var apiVersionDescriptions = provider.ApiVersionDescriptions.OrderByDescending(d =>
                d.ApiVersion
            );
            foreach (var description in apiVersionDescriptions)
            {
                options.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    description.GroupName.ToUpperInvariant()
                );
            }
        });
        return app;
    }

    public static IServiceCollection SetupSwagger(this IServiceCollection services)
    {
        services
            .AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>()
            .AddSwaggerGen(options => ConfigureSwaggerOptions.AddSwaggerGen(options));

        return services;
    }
}

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    readonly IApiVersionDescriptionProvider provider;

    // must be the same as in your middleware for API key authentication
    public const string ApiKeyHeaderName = "X-Api-Key";

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) =>
        this.provider = provider;

    public void Configure(SwaggerGenOptions options)
    {
        // add a swagger document for each discovered API version
        // note: you might choose to skip or document deprecated API versions differently
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
        }
    }

    static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        var info = new OpenApiInfo()
        {
            Title = "CompanyInfo API",
            Version = description.ApiVersion.ToString(),
            Description =
                @"
The CompanyInfo API provides information about companies based on their unique identifiers (e.g., SIRET for France, UID for Switzerland). 
It aggregates data from multiple sources to offer a unified interface for company information retrieval.
It also has VAT number validation and French VAT builder features.
",
        };

        if (description.IsDeprecated)
        {
            info.Description += " This API version has been deprecated.";
        }

        return info;
    }

    public static void AddSwaggerGen(SwaggerGenOptions options)
    {
        // Include XML comments for Swagger documentation
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }

        // https://github.com/swagger-api/swagger-ui/issues/7911
        options.CustomSchemaIds(type => type.FullName?.Replace("+", ".")); // Use full type names in schema definitions);
        options.OperationFilter<SwaggerDefaultValues>();

        // add API key authorization to swagger UI
        options.AddSecurityDefinition(
            ApiKeyHeaderName,
            new OpenApiSecurityScheme
            {
                Description = "API Key header using the ApiKey scheme",
                In = ParameterLocation.Header,
                Name = ApiKeyHeaderName,
                Type = SecuritySchemeType.ApiKey,
                Scheme = ApiKeyAuthenticationOptions.DefaultScheme,
            }
        );
    }
}

public class SwaggerDefaultValues : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;

        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1752#issue-663991077
        foreach (var responseType in context.ApiDescription.SupportedResponseTypes)
        {
            // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/b7cf75e7905050305b115dd96640ddd6e74c7ac9/src/Swashbuckle.AspNetCore.SwaggerGen/SwaggerGenerator/SwaggerGenerator.cs#L383-L387
            var responseKey = responseType.IsDefaultResponse
                ? "default"
                : responseType.StatusCode.ToString();
            var response = operation.Responses[responseKey];

            foreach (var contentType in response.Content.Keys)
            {
                if (!responseType.ApiResponseFormats.Any(x => x.MediaType == contentType))
                {
                    response.Content.Remove(contentType);
                }
            }
        }

        if (operation.Parameters == null)
        {
            return;
        }

        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/412
        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/pull/413
        foreach (var parameter in operation.Parameters)
        {
            var description = apiDescription.ParameterDescriptions.First(p =>
                p.Name == parameter.Name
            );

            if (parameter.Description == null)
            {
                parameter.Description = description.ModelMetadata?.Description;
            }

            if (parameter.Schema.Default == null && description.DefaultValue != null)
            {
                // REF: https://github.com/Microsoft/aspnet-api-versioning/issues/429#issuecomment-605402330
                var json = JsonSerializer.Serialize(
                    description.DefaultValue,
                    description.ModelMetadata?.ModelType ?? typeof(object)
                );
                parameter.Schema.Default = OpenApiAnyFactory.CreateFromJson(json);
            }

            parameter.Required |= description.IsRequired;
        }

        // apply security (Authorization: ApiKey <key>) to all methods that have the Authorize attribute
        if (
            context.MethodInfo.GetCustomAttributes(true).Any(x => x is AuthorizeAttribute)
            || (
                context
                    .MethodInfo.DeclaringType?.GetCustomAttributes(true)
                    .Any(x => x is AuthorizeAttribute)
                ?? false
            )
        )
        {
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = ConfigureSwaggerOptions.ApiKeyHeaderName,
                            },
                        },
                        new string[] { }
                    },
                },
            };
        }
    }
}
