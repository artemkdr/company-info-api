using Microsoft.AspNetCore.Authentication;

namespace CompanyInfo.Api.Shared.Security;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string Scheme => DefaultScheme;
    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
}
