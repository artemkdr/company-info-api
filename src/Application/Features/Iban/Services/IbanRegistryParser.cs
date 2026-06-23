using System.Text.RegularExpressions;
using CompanyInfo.Api.Shared.Attributes;
using Microsoft.Extensions.Configuration;

namespace CompanyInfo.Api.Application.Features.Iban.Services;

/// <summary>
/// Parses IBAN registry files and converts IBAN structure patterns into regex validation rules.
/// </summary>
/// <remarks>
/// <strong>Registered as Singleton:</strong> The registry file is parsed once at startup and results are
/// cached indefinitely. IBAN structure rules and country lengths never change during runtime, making
/// this service stateless and safe to share across all request scopes. This maximizes performance
/// and resource efficiency.
/// </remarks>
[RegisterService(ServiceLifetime.Singleton)]
public class IbanRegistryParser
{
    private const string DefaultIbanRegistryFileRelativePath =
        "Application/Features/Iban/Data/iban-registry-rules.txt";

    private static readonly Regex StructureTokenRegex = new(
        "(?<length>\\d+)!((?<type>[NAC]))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    // Fallback structure rules if registry file cannot be read.
    private static readonly IReadOnlyDictionary<string, string> FallbackCountryIbanRegex =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DE"] = "^DE[0-9]{2}[0-9]{18}$",
            ["CH"] = "^CH[0-9]{2}[0-9]{5}[A-Z0-9]{12}$",
            ["ES"] = "^ES[0-9]{2}[0-9]{20}$",
            ["FR"] = "^FR[0-9]{2}[0-9]{10}[A-Z0-9]{11}[0-9]{2}$",
            ["MC"] = "^MC[0-9]{2}[0-9]{10}[A-Z0-9]{11}[0-9]{2}$",
            ["NL"] = "^NL[0-9]{2}[A-Z]{4}[0-9]{10}$",
        };

    // Fallback IBAN lengths for built-in countries when registry file cannot be read.
    private static readonly IReadOnlyDictionary<string, int> FallbackCountryLengths =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["DE"] = 22,
            ["CH"] = 21,
            ["ES"] = 24,
            ["FR"] = 27,
            ["MC"] = 27,
            ["NL"] = 18,
        };

    // Fallback bank/branch positions for built-in countries when registry file cannot be read.
    // Offsets are 0-indexed IBAN positions; lengths are character counts.
    private static readonly IReadOnlyDictionary<
        string,
        BbanComponentPositions
    > FallbackBankBranchPositions = new Dictionary<string, BbanComponentPositions>(
        StringComparer.Ordinal
    )
    {
        ["CH"] = new(4, 5, null, null),
        ["CZ"] = new(4, 4, null, null),
        ["DE"] = new(4, 8, null, null),
        ["ES"] = new(4, 4, 8, 4),
        ["FR"] = new(4, 5, null, null),
        ["HU"] = new(4, 3, 7, 4),
        ["IT"] = new(5, 5, 10, 5),
        ["MC"] = new(4, 5, null, null),
        ["NL"] = new(4, 4, null, null),
        ["PL"] = new(4, 8, null, null),
        ["SK"] = new(4, 4, null, null),
    };

    private readonly ILogger<IbanRegistryParser> _logger;
    private readonly string _registryFilePath;
    private readonly Lazy<
        IReadOnlyDictionary<string, BbanComponentPositions>
    > _bankBranchPositionsLazy;

    /// <summary>
    /// Initializes a new instance of the <see cref="IbanRegistryParser"/> class.
    /// </summary>
    public IbanRegistryParser(ILogger<IbanRegistryParser> logger, IConfiguration configuration)
        : this(logger, configuration["Iban:RegistryFilePath"]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="IbanRegistryParser"/> class with an explicit registry file path.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="registryFilePath">Optional absolute or AppContext-relative path to the registry file.</param>
    public IbanRegistryParser(ILogger<IbanRegistryParser> logger, string? registryFilePath)
    {
        _logger = logger;
        _registryFilePath = ResolveRegistryFilePath(registryFilePath);
        _bankBranchPositionsLazy = new Lazy<IReadOnlyDictionary<string, BbanComponentPositions>>(
            LoadBankBranchPositions
        );
    }

    private static string ResolveRegistryFilePath(string? configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? DefaultIbanRegistryFileRelativePath
            : configuredPath;

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            path.Replace('/', Path.DirectorySeparatorChar)
        );
    }

    /// <summary>
    /// Loads IBAN structure patterns from registry file and converts them to regex validation rules.
    /// </summary>
    /// <returns>Dictionary mapping country codes to IBAN validation regex patterns.</returns>
    public IReadOnlyDictionary<string, string> LoadCountryIbanRegex()
    {
        try
        {
            if (!File.Exists(_registryFilePath))
            {
                _logger.LogWarning(
                    "IBAN registry file not found at {RegistryPath}. Falling back to built-in subset rules.",
                    _registryFilePath
                );
                return FallbackCountryIbanRegex;
            }

            var lines = File.ReadAllLines(_registryFilePath);
            var countryCodesLine = lines.FirstOrDefault(line =>
                line.StartsWith("IBAN prefix country code (ISO 3166)", StringComparison.Ordinal)
            );
            var ibanStructureLine = lines.FirstOrDefault(line =>
                line.StartsWith("IBAN structure", StringComparison.Ordinal)
            );

            if (
                string.IsNullOrWhiteSpace(countryCodesLine)
                || string.IsNullOrWhiteSpace(ibanStructureLine)
            )
            {
                _logger.LogWarning(
                    "IBAN registry file missing required rows. Falling back to built-in subset rules."
                );
                return FallbackCountryIbanRegex;
            }

            var countryCodes = countryCodesLine
                .Split('\t')
                .Skip(1)
                .Select(value => value.Trim())
                .ToList();
            var structures = ibanStructureLine
                .Split('\t')
                .Skip(1)
                .Select(value => value.Trim())
                .ToList();

            if (countryCodes.Count == 0 || structures.Count == 0)
            {
                _logger.LogWarning(
                    "IBAN registry file has empty code/pattern rows. Falling back to built-in subset rules."
                );
                return FallbackCountryIbanRegex;
            }

            var count = Math.Min(countryCodes.Count, structures.Count);
            var regexByCountry = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var index = 0; index < count; index++)
            {
                var countryCode = countryCodes[index].ToUpperInvariant();
                var structure = structures[index].ToUpperInvariant();

                if (countryCode.Length != 2 || string.IsNullOrWhiteSpace(structure))
                {
                    continue;
                }

                var regex = TryBuildRegexFromIbanStructure(countryCode, structure);
                if (regex is null)
                {
                    continue;
                }

                regexByCountry[countryCode] = regex;
            }

            if (regexByCountry.Count == 0)
            {
                _logger.LogWarning(
                    "No country patterns could be parsed from IBAN registry file. Falling back to built-in subset rules."
                );
                return FallbackCountryIbanRegex;
            }

            foreach (var fallback in FallbackCountryIbanRegex)
            {
                regexByCountry.TryAdd(fallback.Key, fallback.Value);
            }

            _logger.LogInformation(
                "Loaded {PatternCount} IBAN structure patterns from registry file.",
                regexByCountry.Count
            );
            return regexByCountry;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to parse IBAN registry file. Falling back to built-in subset rules."
            );
            return FallbackCountryIbanRegex;
        }
    }

    /// <summary>
    /// Converts an IBAN structure pattern (e.g., "DE2!n8!n10!n") to a regex validation pattern.
    /// </summary>
    /// <param name="countryCode">Two-letter country code (e.g., "DE").</param>
    /// <param name="structure">IBAN structure pattern from registry (e.g., "2!n8!n10!n").</param>
    /// <returns>Regex pattern string, or null if conversion fails.</returns>
    private static string? TryBuildRegexFromIbanStructure(string countryCode, string structure)
    {
        if (!structure.StartsWith(countryCode, StringComparison.Ordinal))
        {
            return null;
        }

        var tokenPart = structure[countryCode.Length..];
        var matches = StructureTokenRegex.Matches(tokenPart);
        if (matches.Count == 0)
        {
            return null;
        }

        var consumedLength = 0;
        var pattern = "^" + countryCode;

        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                return null;
            }

            consumedLength += match.Length;
            var length = int.Parse(match.Groups["length"].Value);
            var type = match.Groups["type"].Value;

            pattern += type switch
            {
                "N" => $"[0-9]{{{length}}}",
                "A" => $"[A-Z]{{{length}}}",
                "C" => $"[A-Z0-9]{{{length}}}",
                _ => string.Empty,
            };

            if (pattern.EndsWith("{}", StringComparison.Ordinal))
            {
                return null;
            }
        }

        if (consumedLength != tokenPart.Length)
        {
            return null;
        }

        return pattern + "$";
    }

    /// <summary>
    /// Loads IBAN country lengths from registry file (BBAN length + 4 for country/check digits).
    /// </summary>
    /// <returns>Dictionary mapping country codes to expected IBAN lengths.</returns>
    public IReadOnlyDictionary<string, int> LoadCountryLengths()
    {
        try
        {
            if (!File.Exists(_registryFilePath))
            {
                _logger.LogWarning(
                    "IBAN registry file not found at {RegistryPath}. Using fallback country lengths for built-in countries.",
                    _registryFilePath
                );
                return FallbackCountryLengths;
            }

            var lines = File.ReadAllLines(_registryFilePath);
            var countryCodesLine = lines.FirstOrDefault(line =>
                line.StartsWith("IBAN prefix country code (ISO 3166)", StringComparison.Ordinal)
            );
            var bbanLengthLine = lines.FirstOrDefault(line =>
                line.StartsWith("BBAN length", StringComparison.Ordinal)
            );

            if (
                string.IsNullOrWhiteSpace(countryCodesLine)
                || string.IsNullOrWhiteSpace(bbanLengthLine)
            )
            {
                _logger.LogWarning(
                    "IBAN registry file missing required rows. Using fallback country lengths."
                );
                return FallbackCountryLengths;
            }

            var countryCodes = countryCodesLine
                .Split('\t')
                .Skip(1)
                .Select(value => value.Trim())
                .ToList();
            var bbanLengths = bbanLengthLine
                .Split('\t')
                .Skip(1)
                .Select(value => value.Trim())
                .ToList();

            if (countryCodes.Count == 0 || bbanLengths.Count == 0)
            {
                _logger.LogWarning(
                    "IBAN registry file has empty code/length rows. Using fallback country lengths."
                );
                return FallbackCountryLengths;
            }

            var count = Math.Min(countryCodes.Count, bbanLengths.Count);
            var lengthsByCountry = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var index = 0; index < count; index++)
            {
                var countryCode = countryCodes[index].Trim().ToUpperInvariant();
                var bbanLengthStr = bbanLengths[index].Trim();

                if (countryCode.Length != 2 || !int.TryParse(bbanLengthStr, out var bbanLength))
                {
                    continue;
                }

                // IBAN length = country code (2) + check digits (2) + BBAN length
                var ibanLength = 4 + bbanLength;
                lengthsByCountry[countryCode] = ibanLength;
            }

            if (lengthsByCountry.Count == 0)
            {
                _logger.LogWarning(
                    "No country lengths could be parsed from IBAN registry file. Using fallback country lengths."
                );
                return FallbackCountryLengths;
            }

            foreach (var fallback in FallbackCountryLengths)
            {
                lengthsByCountry.TryAdd(fallback.Key, fallback.Value);
            }

            _logger.LogInformation(
                "Loaded {CountryCount} IBAN country lengths from registry file.",
                lengthsByCountry.Count
            );
            return lengthsByCountry;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to parse IBAN registry file. Using fallback country lengths."
            );
            return FallbackCountryLengths;
        }
    }

    /// <summary>
    /// Loads bank and branch identifier positions from the registry file.
    /// Positions are expressed as 0-indexed IBAN offsets (BBAN 1-indexed start converted to absolute IBAN offset).
    /// </summary>
    /// <returns>Dictionary mapping country codes to bank/branch component positions.</returns>
    public IReadOnlyDictionary<string, BbanComponentPositions> LoadBankBranchPositions()
    {
        try
        {
            if (!File.Exists(_registryFilePath))
            {
                _logger.LogWarning(
                    "IBAN registry file not found at {RegistryPath}. Using fallback bank/branch positions.",
                    _registryFilePath
                );
                return FallbackBankBranchPositions;
            }

            var lines = File.ReadAllLines(_registryFilePath);
            var countryCodesLine = lines.FirstOrDefault(line =>
                line.StartsWith("IBAN prefix country code (ISO 3166)", StringComparison.Ordinal)
            );
            var bankPosLine = lines.FirstOrDefault(line =>
                line.StartsWith(
                    "Bank identifier position within the BBAN",
                    StringComparison.Ordinal
                )
            );
            var branchPosLine = lines.FirstOrDefault(line =>
                line.StartsWith(
                    "Branch identifier position within the BBAN",
                    StringComparison.Ordinal
                )
            );

            if (
                string.IsNullOrWhiteSpace(countryCodesLine)
                || string.IsNullOrWhiteSpace(bankPosLine)
            )
            {
                _logger.LogWarning(
                    "IBAN registry file missing required rows for bank/branch positions. Using fallback."
                );
                return FallbackBankBranchPositions;
            }

            var countryCodes = countryCodesLine.Split('\t').Skip(1).Select(v => v.Trim()).ToList();
            var bankPositions = bankPosLine.Split('\t').Skip(1).Select(v => v.Trim()).ToList();
            var branchPositions = branchPosLine is not null
                ? branchPosLine.Split('\t').Skip(1).Select(v => v.Trim()).ToList()
                : [];

            var count = Math.Min(countryCodes.Count, bankPositions.Count);
            var positionsByCountry = new Dictionary<string, BbanComponentPositions>(
                StringComparer.Ordinal
            );

            for (var index = 0; index < count; index++)
            {
                var countryCode = countryCodes[index].ToUpperInvariant();
                if (countryCode.Length != 2)
                {
                    continue;
                }

                if (
                    !TryParseBbanRange(bankPositions[index], out var bankOffset, out var bankLength)
                )
                {
                    continue;
                }

                int? branchOffset = null;
                int? branchLength = null;
                if (
                    index < branchPositions.Count
                    && TryParseBbanRange(branchPositions[index], out var bOff, out var bLen)
                )
                {
                    branchOffset = bOff;
                    branchLength = bLen;
                }

                positionsByCountry[countryCode] = new BbanComponentPositions(
                    bankOffset,
                    bankLength,
                    branchOffset,
                    branchLength
                );
            }

            if (positionsByCountry.Count == 0)
            {
                _logger.LogWarning(
                    "No bank/branch positions could be parsed from registry file. Using fallback."
                );
                return FallbackBankBranchPositions;
            }

            foreach (var fallback in FallbackBankBranchPositions)
            {
                positionsByCountry.TryAdd(fallback.Key, fallback.Value);
            }

            _logger.LogInformation(
                "Loaded bank/branch positions for {CountryCount} countries from registry file.",
                positionsByCountry.Count
            );
            return positionsByCountry;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to parse bank/branch positions from registry file. Using fallback."
            );
            return FallbackBankBranchPositions;
        }
    }

    /// <summary>
    /// Populates national components (bank code, branch code, account number) based on registry-derived positions.
    /// </summary>
    /// <param name="response">IBAN verification response to populate.</param>
    public void PopulateNationalComponents(IbanVerifyResponse response)
    {
        // Bank and branch codes: registry-driven, covers all 89 countries for bank code.
        if (_bankBranchPositionsLazy.Value.TryGetValue(response.CountryCode, out var pos))
        {
            response.BankCode = response.NormalizedIban.Substring(pos.BankOffset, pos.BankLength);

            if (pos.BranchOffset.HasValue && pos.BranchLength.HasValue)
            {
                response.BranchCode = response.NormalizedIban.Substring(
                    pos.BranchOffset.Value,
                    pos.BranchLength.Value
                );
            }
        }

        // Account number, national check digits, and branch fallback for countries where
        // the registry does not define a branch identifier position (FR, MC).
        // Only BankCode and BranchCode affect BIC lookup; the rest are informational.
        switch (response.CountryCode)
        {
            case "CH":
                response.AccountNumber = response.NormalizedIban.Substring(9, 12);
                break;
            case "DE":
                response.AccountNumber = response.NormalizedIban.Substring(12, 10);
                break;
            case "ES":
                response.NationalCheckDigits = response.NormalizedIban.Substring(12, 2);
                response.AccountNumber = response.NormalizedIban.Substring(14, 10);
                break;
            case "FR":
            case "MC":
                // Registry omits branch position for FR/MC; fall back to known BBAN offset.
                response.BranchCode ??= response.NormalizedIban.Substring(9, 5);
                response.AccountNumber = response.NormalizedIban.Substring(14, 11);
                response.NationalCheckDigits = response.NormalizedIban.Substring(25, 2);
                break;
            case "NL":
                response.AccountNumber = response.NormalizedIban.Substring(8, 10);
                break;
            case "CZ":
            case "SK":
                response.AccountNumber = response.NormalizedIban.Substring(14, 10);
                break;
            case "PL":
                response.AccountNumber = response.NormalizedIban.Substring(12, 16);
                break;
            case "HU":
                response.NationalCheckDigits = response.NormalizedIban.Substring(11, 1);
                response.AccountNumber = response.NormalizedIban.Substring(12, 16);
                break;
            case "IT":
                response.NationalCheckDigits = response.NormalizedIban.Substring(4, 1);
                response.AccountNumber = response.NormalizedIban.Substring(15, 12);
                break;
        }
    }

    /// <summary>
    /// Generates country-specific lookup keys for BIC resolution from validated IBANs.
    /// </summary>
    /// <param name="response">IBAN verification response with extracted components.</param>
    /// <returns>Enumerable of lookup keys to attempt in order.</returns>
    public IEnumerable<string> GetLookupKeys(IbanVerifyResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.BankCode))
        {
            yield break;
        }

        // FR and MC: try with branch code first if available
        if (
            (response.CountryCode == "FR" || response.CountryCode == "MC")
            && !string.IsNullOrWhiteSpace(response.BranchCode)
        )
        {
            yield return $"{response.CountryCode}:{response.BankCode}:{response.BranchCode}";
        }

        // Default: country:bankcode for all countries
        yield return $"{response.CountryCode}:{response.BankCode}";
    }

    /// <summary>
    /// Parses a BBAN range string (e.g. "1-8") into a 0-indexed IBAN offset and length.
    /// BBAN positions are 1-indexed; IBAN offset accounts for the 4-character country+check prefix.
    /// </summary>
    private static bool TryParseBbanRange(string raw, out int ibanOffset, out int length)
    {
        ibanOffset = 0;
        length = 0;

        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = raw.Split('-');
        if (
            parts.Length != 2
            || !int.TryParse(parts[0].Trim(), out var start)
            || !int.TryParse(parts[1].Trim(), out var end)
            || start < 1
            || end < start
        )
        {
            return false;
        }

        // BBAN is 1-indexed; IBAN prefix (country code + check digits) is 4 chars.
        ibanOffset = 4 + (start - 1);
        length = end - start + 1;
        return true;
    }
}

/// <summary>
/// Represents the 0-indexed IBAN positions of bank and branch identifiers for a country.
/// </summary>
/// <param name="BankOffset">0-indexed IBAN offset where the bank code starts.</param>
/// <param name="BankLength">Number of characters in the bank code.</param>
/// <param name="BranchOffset">0-indexed IBAN offset where the branch code starts, or null if not defined.</param>
/// <param name="BranchLength">Number of characters in the branch code, or null if not defined.</param>
public sealed record BbanComponentPositions(
    int BankOffset,
    int BankLength,
    int? BranchOffset,
    int? BranchLength
);
