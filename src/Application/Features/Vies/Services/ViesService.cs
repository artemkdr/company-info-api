using CompanyInfo.Api.Shared.Attributes;
using Microsoft.Extensions.Caching.Memory;

namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// Service that validates EU VAT numbers using the Padi.Vies library.
/// Handles MS_MAX_CONCURRENT_REQ errors with retry logic.
/// Results are cached using IMemoryCache with configurable TTL.
/// </summary>
[RegisterService(ServiceLifetime.Scoped, serviceType: typeof(IViesService))]
public class ViesService : IViesService
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ViesService> _logger;

    private const int MaxRetries = 3;
    private const int BaseDelayMs = 1000;
    private const string CacheKeyPrefix = "vies:";

    /// <summary>
    /// Initializes a new instance of the <see cref="ViesService"/> class.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public ViesService(
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<ViesService> logger
    )
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public ViesFormatValidationResponse ValidateFormat(string vatNumber)
    {
        if (string.IsNullOrWhiteSpace(vatNumber))
        {
            return new ViesFormatValidationResponse
            {
                VatNumber = vatNumber ?? string.Empty,
                IsValid = false,
            };
        }

        var isValid = Padi.Vies.ViesManager.IsValid(vatNumber).IsValid;
        return new ViesFormatValidationResponse { VatNumber = vatNumber, IsValid = isValid };
    }

    /// <inheritdoc />
    public async Task<ViesValidationResponse> CheckActiveAsync(string vatNumber)
    {
        var cacheKey = $"{CacheKeyPrefix}{vatNumber.ToUpperInvariant()}";

        if (_cache.TryGetValue(cacheKey, out ViesValidationResponse? cached) && cached != null)
        {
            _logger.LogDebug("VIES cache hit for {VatNumber}", vatNumber);
            return cached;
        }

        var response = await CheckActiveWithRetryAsync(vatNumber);

        // Only cache successful, non-transient results
        if (
            string.IsNullOrEmpty(response.ErrorMessage)
            || !response.ErrorMessage.Contains(
                "temporarily unavailable",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            var expirationMinutes = _configuration.GetValue<int>(
                "Cache:ViesExpirationMinutes",
                120
            );
            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(expirationMinutes));
        }

        return response;
    }

    private async Task<ViesValidationResponse> CheckActiveWithRetryAsync(string vatNumber)
    {
        var viesManager = new Padi.Vies.ViesManager();

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var activeResult = await viesManager.IsActiveAsync(vatNumber);
                return new ViesValidationResponse
                {
                    VatNumber = vatNumber,
                    IsActive = activeResult.IsValid,
                };
            }
            catch (Exception ex)
                when (ex.Message.Contains(
                        "MS_MAX_CONCURRENT_REQ",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && attempt < MaxRetries
                )
            {
                var delay = BaseDelayMs * (int)Math.Pow(2, attempt);
                _logger.LogWarning(
                    "VIES MS_MAX_CONCURRENT_REQ error, retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                    delay,
                    attempt + 1,
                    MaxRetries
                );
                await Task.Delay(delay);
            }
            catch (Exception ex) when (attempt == MaxRetries)
            {
                _logger.LogError(
                    ex,
                    "VIES active check failed after {MaxRetries} retries",
                    MaxRetries
                );
                return new ViesValidationResponse
                {
                    VatNumber = vatNumber,
                    IsActive = false,
                    ErrorMessage =
                        "VIES service is temporarily unavailable. Please try again later.",
                };
            }
            catch (Exception ex)
                when (!ex.Message.Contains(
                        "MS_MAX_CONCURRENT_REQ",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            {
                _logger.LogError(ex, "VIES active check failed for {VatNumber}", vatNumber);
                return new ViesValidationResponse
                {
                    VatNumber = vatNumber,
                    IsActive = false,
                    ErrorMessage = $"Active check failed: {ex.Message}",
                };
            }
        }

        return new ViesValidationResponse
        {
            VatNumber = vatNumber,
            IsActive = false,
            ErrorMessage = "VIES service is temporarily unavailable.",
        };
    }
}
