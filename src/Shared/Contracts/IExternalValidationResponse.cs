namespace CompanyInfo.Api.Shared.Contracts;

/// <summary>
/// Marks a response model that proxies an external validation service.
/// </summary>
/// <remarks>
/// Implementing types always return HTTP 200 OK regardless of the validation outcome.
/// Callers must inspect <see cref="ErrorMessage"/> to determine whether the request
/// was accepted by the external service:
/// <list type="bullet">
///   <item><description><see langword="null"/> — the external service accepted the request; treat as valid.</description></item>
///   <item><description>non-<see langword="null"/> — the external service rejected the request or returned an error; the value contains the reason.</description></item>
/// </list>
/// This is distinct from HTTP 4xx/5xx responses, which are only returned for malformed
/// input (request validation failures) or unhandled internal exceptions.
/// </remarks>
public interface IExternalValidationResponse
{
    /// <summary>
    /// Gets the error message returned by the external service, or <see langword="null"/> if validation succeeded.
    /// </summary>
    string? ErrorMessage { get; }
}
