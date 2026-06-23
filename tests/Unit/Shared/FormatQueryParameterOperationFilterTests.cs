using CompanyInfo.Api.Shared.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using NSubstitute;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Shared
{
    public class FormatQueryParameterOperationFilterTests
    {
        private readonly FormatQueryParameterOperationFilter _filter = new();

        [Fact(DisplayName = "Should add format query parameter for feature controllers")]
        public void Apply_FeatureController_AddsFormatParameter()
        {
            var operation = new OpenApiOperation { Parameters = new List<OpenApiParameter>() };
            var context = CreateContext(
                typeof(CompanyInfo.Api.Application.Features.TestFeature.SwaggerFeatureProbeController).GetMethod(
                    nameof(
                        CompanyInfo
                            .Api
                            .Application
                            .Features
                            .TestFeature
                            .SwaggerFeatureProbeController
                            .Get
                    )
                )!
            );

            _filter.Apply(operation, context);

            operation
                .Parameters.Should()
                .ContainSingle(parameter =>
                    parameter.In == ParameterLocation.Query && parameter.Name == "format"
                );
        }

        [Fact(DisplayName = "Should not add format query parameter for health controller")]
        public void Apply_HealthController_DoesNotAddFormatParameter()
        {
            var operation = new OpenApiOperation { Parameters = new List<OpenApiParameter>() };
            var context = CreateContext(
                typeof(CompanyInfo.Api.Application.Features.Health.SwaggerHealthProbeController).GetMethod(
                    nameof(
                        CompanyInfo.Api.Application.Features.Health.SwaggerHealthProbeController.Get
                    )
                )!
            );

            _filter.Apply(operation, context);

            operation.Parameters.Should().BeEmpty();
        }

        [Fact(DisplayName = "Should not duplicate format query parameter when already present")]
        public void Apply_FormatAlreadyExists_DoesNotDuplicateParameter()
        {
            var operation = new OpenApiOperation
            {
                Parameters =
                [
                    new OpenApiParameter { Name = "format", In = ParameterLocation.Query },
                ],
            };
            var context = CreateContext(
                typeof(CompanyInfo.Api.Application.Features.TestFeature.SwaggerFeatureProbeController).GetMethod(
                    nameof(
                        CompanyInfo
                            .Api
                            .Application
                            .Features
                            .TestFeature
                            .SwaggerFeatureProbeController
                            .Get
                    )
                )!
            );

            _filter.Apply(operation, context);

            operation
                .Parameters.Should()
                .ContainSingle(parameter =>
                    parameter.In == ParameterLocation.Query && parameter.Name == "format"
                );
        }

        private static OperationFilterContext CreateContext(System.Reflection.MethodInfo methodInfo)
        {
            return new OperationFilterContext(
                new ApiDescription(),
                Substitute.For<ISchemaGenerator>(),
                new SchemaRepository(),
                methodInfo
            );
        }
    }
}

namespace CompanyInfo.Api.Application.Features.TestFeature
{
    public class SwaggerFeatureProbeController
    {
        public void Get() { }
    }
}

namespace CompanyInfo.Api.Application.Features.Health
{
    public class SwaggerHealthProbeController
    {
        public void Get() { }
    }
}
