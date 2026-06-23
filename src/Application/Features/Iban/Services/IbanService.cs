using System.Text.RegularExpressions;
using CompanyInfo.Api.Application.Features.Iban.Services;
using CompanyInfo.Api.Shared.Attributes;
using Microsoft.Extensions.Caching.Memory;

namespace CompanyInfo.Api.Application.Features.Iban;

/// <summary>
/// Orchestrates IBAN validation using a multi-step process:
/// 1. Validates format, length, structure (via registry), and MOD-97 checksum
/// 2. Extracts country-specific components (bank code, branch code, account number)
/// 3. Resolves BIC from local CSV files or external provider fallback
///
/// Delegates specialized logic to:
/// - <see cref="IbanRegistryParser"/>: Parses IBAN structure patterns, country lengths, generates lookup keys, and extracts components (DI — needs ILogger)
///
/// <remarks>
/// <strong>Lifetime Management:</strong>
/// Registered as <see cref="ServiceLifetime.Scoped"/> (per-request) to allow request-scoped dependencies
/// like <see cref="IExternalBicLookupService"/> and <see cref="IMemoryCache"/> usage.
///
/// Safe to depend on <see cref="ServiceLifetime.Singleton"/> helpers:
/// - <see cref="IbanRegistryParser"/> — Singleton (parses registry file once at startup, immutable results)
/// - <see cref="IBicDataProvider"/> — Singleton (loads CSV data once, immutable dictionary)
/// </remarks>
/// </summary>
[RegisterService(ServiceLifetime.Scoped, serviceType: typeof(IIbanService))]
public class IbanService : IIbanService
{
    private readonly ILogger<IbanService> _logger;
    private readonly IBicDataProvider _csvBicDataProvider;
    private readonly IExternalBicLookupService _externalBicLookupService;
    private readonly IbanRegistryParser _registryParser;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly Lazy<IReadOnlyDictionary<string, BicLookupEntry>> _bicLookupEntries;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _countryIbanRegex;
    private readonly Lazy<IReadOnlyDictionary<string, int>> _countryLengths;

    private const string CacheKeyPrefix = "iban:";

    /// <summary>
    /// Initializes a new instance of the <see cref="IbanService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="csvBicDataProvider">Provider for CSV BIC data.</param>
    /// <param name="externalBicLookupService">External BIC fallback lookup service.</param>
    /// <param name="registryParser">Parser for IBAN registry file.</param>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="configuration">The application configuration.</param>
    public IbanService(
        ILogger<IbanService> logger,
        IBicDataProvider csvBicDataProvider,
        IExternalBicLookupService externalBicLookupService,
        IbanRegistryParser registryParser,
        IMemoryCache cache,
        IConfiguration configuration
    )
    {
        _logger = logger;
        _csvBicDataProvider = csvBicDataProvider;
        _externalBicLookupService = externalBicLookupService;
        _registryParser = registryParser;
        _cache = cache;
        _configuration = configuration;
        _bicLookupEntries = new Lazy<IReadOnlyDictionary<string, BicLookupEntry>>(
            LoadBicLookupEntries
        );
        _countryIbanRegex = new Lazy<IReadOnlyDictionary<string, string>>(() =>
            _registryParser.LoadCountryIbanRegex()
        );
        _countryLengths = new Lazy<IReadOnlyDictionary<string, int>>(() =>
            _registryParser.LoadCountryLengths()
        );
    }

    /// <inheritdoc />
    public async Task<IbanVerifyResponse> VerifyAsync(string iban)
    {
        var normalizedIban = NormalizeIban(iban);
        var cacheKey = $"{CacheKeyPrefix}verify:{normalizedIban}";

        if (_cache.TryGetValue(cacheKey, out IbanVerifyResponse? cached) && cached != null)
        {
            _logger.LogDebug("IBAN cache hit for {NormalizedIban}", normalizedIban);
            return cached;
        }

        var response = new IbanVerifyResponse
        {
            NormalizedIban = normalizedIban,
            ActualLength = normalizedIban.Length,
        };

        if (string.IsNullOrWhiteSpace(normalizedIban))
        {
            response.ErrorMessage = "IBAN is required.";
            response.BicLookupStatus = "invalid_iban";
            return response;
        }

        if (normalizedIban.Length < 4)
        {
            response.ErrorMessage = "IBAN must contain at least 4 characters after normalization.";
            response.BicLookupStatus = "invalid_iban";
            return response;
        }

        if (!HasValidIbanPrefix(normalizedIban))
        {
            response.ErrorMessage =
                "IBAN prefix must start with two letters followed by two digits.";
            response.BicLookupStatus = "invalid_iban";
            return response;
        }

        response.CountryCode = normalizedIban[..2];
        response.IsKnownCountry = _countryLengths.Value.TryGetValue(
            response.CountryCode,
            out var expectedLength
        );
        response.ExpectedLength = response.IsKnownCountry ? expectedLength : 0;

        if (
            !normalizedIban.All(static character =>
                (character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9')
            )
        )
        {
            response.ErrorMessage = "IBAN may only contain letters, digits, spaces, or dashes.";
            response.BicLookupStatus = "invalid_iban";
            return response;
        }

        response.IsLengthValid =
            response.IsKnownCountry && normalizedIban.Length == response.ExpectedLength;
        response.IsStructureValid =
            response.IsLengthValid && HasValidCountryStructure(normalizedIban);
        response.IsChecksumValid = response.IsStructureValid && HasValidChecksum(normalizedIban);
        response.IsValid =
            response.IsLengthValid && response.IsStructureValid && response.IsChecksumValid;

        if (!response.IsKnownCountry)
        {
            response.ErrorMessage = $"Unsupported IBAN country code '{response.CountryCode}'.";
            response.BicLookupStatus = "unsupported_country";
            return response;
        }

        if (!response.IsLengthValid)
        {
            response.ErrorMessage =
                $"IBAN length mismatch for country {response.CountryCode}. Expected {response.ExpectedLength} characters.";
            response.BicLookupStatus = "invalid_iban";
            return response;
        }

        if (!response.IsStructureValid)
        {
            response.ErrorMessage =
                $"IBAN BBAN structure mismatch for country {response.CountryCode}.";
            response.BicLookupStatus = "invalid_iban";
            return response;
        }

        if (!response.IsChecksumValid)
        {
            response.ErrorMessage = "IBAN checksum validation failed.";
            response.BicLookupStatus = "invalid_iban";
            return response;
        }

        await ResolveBicAsync(response);

        var expirationMinutes = _configuration.GetValue<int>("Cache:IbanExpirationMinutes", 1440);
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(expirationMinutes));

        return response;
    }

    private static string NormalizeIban(string? iban)
    {
        return new string(
            (iban ?? string.Empty)
                .Where(character => !char.IsWhiteSpace(character) && character != '-')
                .Select(char.ToUpperInvariant)
                .ToArray()
        );
    }

    private static bool HasValidChecksum(string iban)
    {
        var remainder = 0;

        foreach (var character in iban[4..].Concat(iban[..4]))
        {
            if (char.IsDigit(character))
            {
                remainder = ((remainder * 10) + (character - '0')) % 97;
                continue;
            }

            var value = character - 'A' + 10;
            remainder = ((remainder * 10) + (value / 10)) % 97;
            remainder = ((remainder * 10) + (value % 10)) % 97;
        }

        return remainder == 1;
    }

    private static bool HasValidIbanPrefix(string iban)
    {
        return iban.Length >= 4
            && char.IsLetter(iban[0])
            && char.IsLetter(iban[1])
            && char.IsDigit(iban[2])
            && char.IsDigit(iban[3]);
    }

    private bool HasValidCountryStructure(string iban)
    {
        if (iban.Length < 4)
        {
            return false;
        }

        var countryCode = iban[..2];
        if (!_countryIbanRegex.Value.TryGetValue(countryCode, out var ibanRegex))
        {
            // Keep checksum + length behavior when no pattern is available.
            return true;
        }

        return Regex.IsMatch(iban, ibanRegex, RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Resolves BIC from local CSV files first, then tries external provider fallback.
    /// Extracts national components (bank code, branch code) required for key generation.
    /// </summary>
    private async Task ResolveBicAsync(IbanVerifyResponse response)
    {
        _registryParser.PopulateNationalComponents(response);

        var lookupKeys = _registryParser.GetLookupKeys(response).ToList();
        if (lookupKeys.Count == 0)
        {
            if (await TryResolveBicFromExternalProviderAsync(response))
            {
                return;
            }

            response.BicLookupStatus = "unsupported_structure";
            return;
        }

        var lookupEntries = _bicLookupEntries.Value;
        foreach (var lookupKey in lookupKeys)
        {
            if (!lookupEntries.TryGetValue(lookupKey, out var entry))
            {
                continue;
            }

            response.Bic = entry.Bic;
            response.BankName = entry.BankName;
            response.BicLookupSource = entry.Source;
            response.BicLookupStatus = "found";
            return;
        }

        if (await TryResolveBicFromExternalProviderAsync(response))
        {
            return;
        }

        response.BicLookupStatus = "not_found";
    }

    /// <summary>
    /// Attempts to resolve BIC using external provider service (fallback).
    /// </summary>
    private async Task<bool> TryResolveBicFromExternalProviderAsync(IbanVerifyResponse response)
    {
        var externalLookup = await _externalBicLookupService.TryResolveAsync(
            response.NormalizedIban,
            response.CountryCode
        );

        if (!externalLookup.IsFound || string.IsNullOrWhiteSpace(externalLookup.Bic))
        {
            return false;
        }

        response.Bic = externalLookup.Bic;
        if (
            string.IsNullOrWhiteSpace(response.BankCode)
            && !string.IsNullOrWhiteSpace(externalLookup.BankCode)
        )
        {
            response.BankCode = externalLookup.BankCode;
        }

        response.BankName = externalLookup.BankName ?? response.BankName;
        response.BicLookupSource = externalLookup.Source;
        response.BicLookupStatus = "found_fallback";
        return true;
    }

    private IReadOnlyDictionary<string, BicLookupEntry> LoadBicLookupEntries()
    {
        var csvEntries = _csvBicDataProvider.LoadBicData();
        _logger.LogInformation(
            "Loaded {EntryCount} BIC lookup entries from CSV provider.",
            csvEntries.Count
        );
        return csvEntries;
    }
}
