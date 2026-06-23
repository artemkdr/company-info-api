using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CompanyInfo.Api.Shared.Filters;

/// <summary>
/// Adds the optional <c>format</c> query parameter to feature endpoints in Swagger.
/// </summary>
public class FormatQueryParameterOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controllerType = context.MethodInfo.DeclaringType;
        var controllerNamespace = controllerType?.Namespace ?? string.Empty;

        var isFeatureController = controllerNamespace.StartsWith(
            "CompanyInfo.Api.Application.Features.",
            StringComparison.Ordinal
        );

        var isHealthController = controllerNamespace.Equals(
            "CompanyInfo.Api.Application.Features.Health",
            StringComparison.Ordinal
        );

        if (!isFeatureController || isHealthController)
        {
            return;
        }

        operation.Parameters ??= new List<OpenApiParameter>();

        var formatParameterExists = operation.Parameters.Any(parameter =>
            parameter.In == ParameterLocation.Query
            && parameter.Name.Equals("format", StringComparison.OrdinalIgnoreCase)
        );

        if (formatParameterExists)
        {
            return;
        }

        operation.Parameters.Add(
            new OpenApiParameter
            {
                Name = "format",
                In = ParameterLocation.Query,
                Required = false,
                Description = "Response format: json or xml. Defaults to json.",
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Default = new OpenApiString("json"),
                    Enum = new List<IOpenApiAny>
                    {
                        new OpenApiString("json"),
                        new OpenApiString("xml"),
                    },
                },
            }
        );
    }
}
