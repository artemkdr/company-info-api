using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CompanyInfo.Api.Shared.Security;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    )
        : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredApiKeys = await Task.Run(() =>
            Context
                .RequestServices.GetRequiredService<IConfiguration>()
                .GetSection("ApiKeys")
                .Get<string[]>()
        );
        // Allow anonymous access if no API keys are configured
        if (configuredApiKeys == null || configuredApiKeys.Length == 0)
        {
            // Create a dummy identity to allow access without an API key
            var claims = new[] { new System.Security.Claims.Claim("ApiKey", "None") };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, Options.Scheme);
            var identities = new[] { identity };
            var principal = new System.Security.Claims.ClaimsPrincipal(identities);
            var ticket = new AuthenticationTicket(principal, Options.Scheme);
            return AuthenticateResult.Success(ticket);
        }

        if (!Request.Headers.TryGetValue(Options.ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.Fail("Unauthorized");
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();

        if (apiKeyHeaderValues.Count == 0 || string.IsNullOrWhiteSpace(providedApiKey))
        {
            return AuthenticateResult.Fail("Unauthorized");
        }

        if (configuredApiKeys != null && configuredApiKeys.Contains(providedApiKey))
        {
            var claims = new[] { new System.Security.Claims.Claim("ApiKey", providedApiKey) };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, Options.Scheme);
            var identities = new[] { identity };
            var principal = new System.Security.Claims.ClaimsPrincipal(identities);
            var ticket = new AuthenticationTicket(principal, Options.Scheme);

            return AuthenticateResult.Success(ticket);
        }

        return AuthenticateResult.Fail("Unauthorized");
    }
}
