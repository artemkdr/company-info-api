using CompanyInfo.Api.Shared.Attributes;

namespace CompanyInfo.Api.Application.Features.Iban.Services;

/// <summary>
/// Loads BIC lookup data from a CSV file (bank code to BIC mapping).
/// </summary>
/// <remarks>
/// <strong>Registered as Singleton:</strong> CSV data is loaded once at startup and cached indefinitely
/// in an immutable <see cref="IReadOnlyDictionary{TKey, TValue}"/>. Bank BIC mappings never change during
/// runtime, making this safe to share across all request scopes. Singleton registration prevents repeated
/// file I/O and maximizes performance.
/// </remarks>
[RegisterService(ServiceLifetime.Singleton, serviceType: typeof(IBicDataProvider))]
public class CsvBicDataProvider : IBicDataProvider
{
    private const string BicLookupDirectoryRelativePath = "Application/Features/Iban/Data";
    private const string CountryBicLookupFileSuffix = "-bic-lookup.csv";
    private readonly ILogger<CsvBicDataProvider> _logger;
    private IReadOnlyDictionary<string, BicLookupEntry>? _cachedEntries;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvBicDataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public CsvBicDataProvider(ILogger<CsvBicDataProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => "CSV";

    /// <inheritdoc />
    public IReadOnlyDictionary<string, BicLookupEntry> LoadBicData()
    {
        if (_cachedEntries is not null)
        {
            return _cachedEntries;
        }

        var lookupDirectoryPath = Path.Combine(
            AppContext.BaseDirectory,
            BicLookupDirectoryRelativePath.Replace('/', Path.DirectorySeparatorChar)
        );

        if (!Directory.Exists(lookupDirectoryPath))
        {
            _logger.LogWarning(
                "CSV BIC lookup directory not found at {LookupDirectoryPath}. Continuing without CSV data.",
                lookupDirectoryPath
            );

            return _cachedEntries = new Dictionary<string, BicLookupEntry>(
                StringComparer.OrdinalIgnoreCase
            );
        }

        var entries = new Dictionary<string, BicLookupEntry>(StringComparer.OrdinalIgnoreCase);
        var countryScopedLookupFiles = Directory
            .GetFiles(lookupDirectoryPath, $"*{CountryBicLookupFileSuffix}")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var lookupPath in countryScopedLookupFiles)
        {
            var fileName = Path.GetFileName(lookupPath);
            var expectedCountryCode = GetExpectedCountryCode(fileName);
            LoadEntriesFromCsvFile(lookupPath, entries, expectedCountryCode);
        }

        _logger.LogInformation(
            "Loaded {EntryCount} BIC lookup entries from CSV provider.",
            entries.Count
        );

        return _cachedEntries = entries;
    }

    private void LoadEntriesFromCsvFile(
        string lookupPath,
        IDictionary<string, BicLookupEntry> entries,
        string? expectedCountryCode = null
    )
    {
        var lines = File.ReadAllLines(lookupPath);
        if (lines.Length == 0)
        {
            return;
        }

        foreach (var rawLine in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var columns = ParseCsvLine(rawLine);
            if (columns.Length < 7)
            {
                _logger.LogWarning(
                    "Skipping malformed CSV BIC lookup row in {LookupPath}: {LookupRow}",
                    lookupPath,
                    rawLine
                );
                continue;
            }

            var countryCode = columns[0].Trim().ToUpperInvariant();
            var lookupKey = columns[1].Trim().ToUpperInvariant();
            var bic = columns[2].Trim().ToUpperInvariant();
            var bankCode = NormalizeOptionalValue(columns[3]);
            var branchCode = NormalizeOptionalValue(columns[4]);
            var bankName = NormalizeOptionalValue(columns[5]);
            var source = NormalizeOptionalValue(columns[6]) ?? "csv-file";

            if (
                string.IsNullOrWhiteSpace(countryCode)
                || string.IsNullOrWhiteSpace(lookupKey)
                || string.IsNullOrWhiteSpace(bic)
            )
            {
                _logger.LogWarning(
                    "Skipping incomplete CSV BIC lookup row in {LookupPath}: {LookupRow}",
                    lookupPath,
                    rawLine
                );
                continue;
            }

            if (
                !string.IsNullOrWhiteSpace(expectedCountryCode)
                && !string.Equals(countryCode, expectedCountryCode, StringComparison.Ordinal)
            )
            {
                _logger.LogWarning(
                    "Skipping row from {LookupPath}. Country code '{CountryCode}' does not match expected '{ExpectedCountryCode}'.",
                    lookupPath,
                    countryCode,
                    expectedCountryCode
                );
                continue;
            }

            entries[$"{countryCode}:{lookupKey}"] = new BicLookupEntry(
                bic,
                bankCode,
                branchCode,
                bankName,
                source
            );
        }
    }

    private static string[] ParseCsvLine(string rawLine)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < rawLine.Length; index++)
        {
            var character = rawLine[index];

            if (character == '"')
            {
                if (insideQuotes && index + 1 < rawLine.Length && rawLine[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }

                continue;
            }

            if (character == ',' && !insideQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    private static string? GetExpectedCountryCode(string fileName)
    {
        if (!fileName.EndsWith(CountryBicLookupFileSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var countryCode = fileName[..^CountryBicLookupFileSuffix.Length].Trim().ToUpperInvariant();
        return countryCode.Length == 2 ? countryCode : null;
    }

    private static string? NormalizeOptionalValue(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
