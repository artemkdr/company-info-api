using System.Reflection;
using CompanyInfo.Api.Application.Extensions;
using CompanyInfo.Api.Shared.Extensions;
using CompanyInfo.Api.Shared.Filters;
using CompanyInfo.Api.Shared.Middleware;
using CompanyInfo.Api.Shared.Validation;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.AddNLog();

// Caching
builder.Services.AddMemoryCache();

// HttpClient factory for external API calls
builder.Services.AddHttpClient();

// Controllers with XML support
builder
    .Services.AddControllers(options =>
    {
        options.Filters.Add<ResponseFormatFilter>();
        options.RespectBrowserAcceptHeader = true;
        options.ReturnHttpNotAcceptable = false;
    })
    .AddXmlSerializerFormatters();

// API versioning
builder.Services.SetupApiVersioning();

// Authentication
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// CORS
builder.Services.SetupCors(builder.Configuration);

// Swagger
builder.Services.SetupSwagger();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<FormatQueryParameterOperationFilter>();
});

// Request validation
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
builder.Services.AddScoped<IRequestValidationService, RequestValidationService>();

// IBAN services (strict CSV BIC provider)
builder.Services.AddIbanServices();

// Auto-register services decorated with [RegisterService]
builder.Services.AddServicesFromAssembly(Assembly.GetExecutingAssembly());

// Add MCP server
builder
    .Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Configure the MCP server to be stateless, meaning it does not maintain any session state between requests. This is suitable for scenarios where each request is independent and does not rely on previous interactions, improving scalability and performance.
        options.Stateless = true;
    })
    .WithToolsFromAssembly()
    .AddAuthorizationFilters();

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger (available in all environments)
app.SetupSwagger(builder.Configuration);

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// setup routing for MCP server, which maps the MCP endpoints to the request pipeline
app.MapMcp("/mcp");

app.Run();

/// <summary>
/// Partial Program class to allow test assembly access via typeof(Program).
/// </summary>
public partial class Program { }
