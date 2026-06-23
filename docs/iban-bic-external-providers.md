# IBAN BIC Lookup: External Providers Fallback

## Overview

The IBAN verification service uses a **multi-tier BIC resolution strategy**:

1. **Primary**: Local CSV lookup files (fast, offline)
2. **Fallback**: External BIC provider services (when local data is unavailable)

## IBAN Registry Infrastructure

### The `iban-registry-rules.txt` File

**Purpose**: Master reference file containing IBAN structure definitions and validation rules for all countries that support IBAN

**Location**: `src/Application/Features/Iban/Data/iban-registry-rules.txt`

**Source**: [SWIFT IBAN Registry](https://www.swift.com/standards/data-standards/iban) — an authoritative, internationally maintained registry of IBAN formats and specifications.

**What It Contains**:

This tab-separated file includes metadata for ~100+ countries with valid IBAN formats:

| Data | Example | Purpose |
|------|---------|---------|
| **Country Code** (ISO 3166) | DE, FR, HU | Two-letter identifier |
| **IBAN Structure Pattern** | `2!n8!n10!n` | Defines BBAN format rules |
| **IBAN Length** | 22 (DE), 27 (FR) | Total expected length |
| **Bank Identifier Position/Pattern** | Pos 4-11, `8!n` | Where and how to extract bank code |
| **Branch Code Position/Pattern** | Optional; varies by country | Where and how to extract branch code |
| **Effective Date** | Apr-07, Jan-14 | Version/update tracking |
| **Contact Details** | Organization, email, phone | Banking authority for that country |

**Example Structure Pattern** (syntax):
- `2!n` = 2 numeric digits (required)
- `5!a` = 5 alphabetic characters (required)
- `11!c` = 11 alphanumeric characters (required)
- `!` = required field
- `?` = optional field

**How It's Used in Code**:

The `IbanRegistryParser` parses this file at runtime (used by `IbanService` for structure + length validation):
```csharp
// Extract IBAN structure patterns from registry
var countryCodesLine = lines.FirstOrDefault(line =>
    line.StartsWith("IBAN prefix country code (ISO 3166)", StringComparison.Ordinal));
var ibanStructureLine = lines.FirstOrDefault(line =>
    line.StartsWith("IBAN structure", StringComparison.Ordinal));

// Convert patterns to regex for validation
// Example: "DE2!n8!n10!n" → "^DE[0-9]{2}[0-9]{8}[0-9]{10}$"
var regex = TryBuildRegexFromIbanStructure(countryCode, structure);
```

**Fallback Behavior**:

If the registry file is missing or cannot be parsed, the service falls back to built-in patterns:

```csharp
private static readonly IReadOnlyDictionary<string, string> FallbackCountryIbanRegex =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DE"] = "^DE[0-9]{2}[0-9]{18}$",
        ["ES"] = "^ES[0-9]{2}[0-9]{20}$",
        ["FR"] = "^FR[0-9]{2}[0-9]{10}[A-Z0-9]{11}[0-9]{2}$",
        // ... more countries
    };
```

Ensures validation continues even if the registry file is unavailable.

**Update Strategy**:

1. SWIFT publishes registry updates when IBAN rules change
2. Download the latest version from [SWIFT Standards](https://www.swift.com/standards/data-standards/iban)
3. Replace the file in the repository
4. Run validation tests to ensure backward compatibility
5. Commit with reference to SWIFT update bulletin

**Related Configuration**:

See [CONFIGURATION.md](../CONFIGURATION.md) section "Local BIC Lookup Data Files" for details on how country-specific components (bank code, branch code) are extracted from validated IBANs.

---

## Current Data Coverage

### Countries with Local BIC Data

| Country | Code | Data Source | BIC Lookup Pattern | Status |
|---------|------|-------------|-------------------|--------|
| Czech Republic | CZ | Czech National Bank | Bank code (4 digits) | ✅ Complete |
| France | FR | selectra.info + codebic.com | Bank code (5 digits) + optional branch code (5 digits) | ✅ 100 most popular banks |
| Hungary | HU | Hungarian branch office register | Bank code (3 digits) | ✅ Complete |
| Poland | PL | Polish National Bank | Bank code (8 digits) | ✅ Complete |
| Slovakia | SK | Slovak National Bank | Bank code (4 digits) | ✅ Complete |

**Files location**: `src/Application/Features/Iban/Data/{CC}-bic-lookup.csv`

### Countries Using External Fallback

All IBAN-supporting countries without local CSV files may attempt external BIC resolution when:
- The IBAN is valid (passes format and checksum validation)
- The country structure can be extracted from the IBAN
- Local lookup returns no result

Note: external fallback depends on the configured provider(s). The current OpenIBAN integration only attempts fallback for: AT, BE, CH, DE, LI, LU, NL.

## External Provider Architecture

### Interface: `IExternalBicLookupService`

Located in `src/Application/Features/Iban/Services/IExternalBicLookupService.cs`

```csharp
public interface IExternalBicLookupService
{
    /// Attempts to resolve BIC and bank metadata for a validated IBAN
    Task<ExternalBicLookupResult> TryResolveAsync(string normalizedIban, string countryCode);
    
    /// Provider name for diagnostics and telemetry
    string ProviderName { get; }
}
```

### Result Record: `ExternalBicLookupResult`

```csharp
public sealed record ExternalBicLookupResult(
    bool IsFound,           // Whether a BIC was resolved
    string? Bic,            // The BIC value (if found)
    string? BankCode,       // Bank code extracted from IBAN (optional)
    string? BankName,       // Institution name (optional, for context)
    string Source           // Provider identifier (e.g., "external-service-v1")
);
```

### Current Implementation

**Service**: `IbanServiceRegistrationExtensions.cs`

The fallback service is registered during application startup. The actual implementation depends on configuration and available external APIs.

**Fallback Behavior**:
- Only triggered if local CSV lookup fails
- Result is marked with `BicLookupStatus = "found_fallback"` to distinguish from local matches
- BIC resolution is **cached** using the configured cache TTL to minimize external calls

## Resolution Flow

```
┌─────────────────────────────────────────┐
│ IBAN Verification Request               │
└──────────────────┬──────────────────────┘
                   │
     ┌─────────────▼──────────────┐
     │ 1. Validate IBAN Structure │
     │    (format, checksum)      │
     └─────────────┬──────────────┘
                   │ ✅ Valid
     ┌─────────────▼──────────────────────────┐
     │ 2. Extract Country Code & Components   │
     │    (bank code, branch code, etc.)      │
     └─────────────┬──────────────────────────┘
                   │
     ┌─────────────▼────────────────────────────────┐
     │ 3. Query Local BIC Lookup Files              │
     │    (if CSV for country exists)               │
     └─────────────┬────────────────────────────────┘
                   │
         ┌─────────┴─────────┐
         │                   │
    ✅ Found           ❌ Not Found
         │                   │
         │     ┌─────────────▼──────────────────┐
         │     │ 4. Try External Provider       │
         │     │    (fallback service)          │
         │     └─────────────┬──────────────────┘
         │                   │
         │             ┌─────┴──────┐
         │         ✅ Found    ❌ Not Found
         │             │           │
         └─────┬───────┘           │
               │                   │
         ┌─────▼──────────────────────────┐
         │ Return Result                  │
         │ (with BicLookupStatus)         │
         └────────────────────────────────┘
```

## Response Status Values

The `IbanVerifyResponse` includes a `BicLookupStatus` field indicating resolution source:

| Status | Meaning | Example |
|--------|---------|---------|
| `"found"` | BIC resolved from local CSV file | FR IBAN → local file match |
| `"found_fallback"` | BIC resolved from external provider | AT IBAN → external provider |
| `"not_found"` | No BIC found after all attempts | IBAN valid but no provider match |
| `"unsupported_structure"` | Country structure not extractable | Country not in supported list |
| `"invalid_iban"` | IBAN failed validation | Format or checksum error |
| `"unsupported_country"` | Country code not recognized | Country not in IBAN registry |

## Adding Local BIC Data for a New Country

### Prerequisites

- Valid IBAN examples for the country
- A mapping of bank codes (or bank code + branch code) to BIC values
- Permission to redistribute the data source

### Steps

1. **Prepare Data File**

   Create a CSV file: `src/Application/Features/Iban/Data/{CC}-bic-lookup.csv`
   
   Format (7 columns):
   ```csv
   CountryCode,LookupKey,Bic,BankCode,BranchCode,BankName,Source
   CC,BANKCODE,BICVALUE,BANKCODE,,Bank Name,"Source description"
   ```
   
   Notes:
   - `CountryCode`: Two-letter ISO code (e.g., "DE")
   - `LookupKey`: The lookup pattern used by `GetLookupKeys()` in `IbanRegistryParser` (`IbanRegistryParser.cs`)
   - `Bic`: 8 or 11 character BIC (normalized to uppercase)
   - `BankCode`: Optional; extracted bank code for reference
   - `BranchCode`: Optional; branch code if supported by country structure
   - `Source`: Attribution/documentation of data source

2. **Add Country Structure (if needed)**

   If not already in the IBAN registry file, add a fallback pattern to `IbanRegistryParser.cs`: 
   
   ```csharp
   private static readonly IReadOnlyDictionary<string, string> FallbackCountryIbanRegex =
       new Dictionary<string, string>(StringComparer.Ordinal)
       {
           ["CC"] = "^CC[0-9]{2}...$",  // Adjust regex for country structure
       };
   ```

3. **Implement Country-Specific Extraction Logic**

   Add to `PopulateNationalComponents()` in `IbanRegistryParser` (`IbanRegistryParser.cs`):
   ```csharp
   case "CC":
       response.BankCode = response.NormalizedIban.Substring(4, 5);
       // Extract other components as needed
       break;
   ```

4. **Implement Lookup Key Generation**

   Add to `GetLookupKeys()` in `IbanRegistryParser` (`IbanRegistryParser.cs`):
    
   ```csharp
   case "CC" when !string.IsNullOrWhiteSpace(response.BankCode):
       yield return $"CC:{response.BankCode}";
       yield break;
   ```

5. **Test**

   ```bash
   # Unit test to verify lookups
   dotnet test tests/CompanyInfo.Api.Tests.csproj --filter "FullyQualifiedName~Iban"
   ```

6. **Document the Source**

   Update this file with:
   - Data provider attribution
   - Update frequency expectations
   - Any known limitations

### Example: Hungary (HU)

**Data File**: `src/Application/Features/Iban/Data/HU-bic-lookup.csv`

**Generator Script**: `src/Application/Features/Iban/Data/sources/HU/build-hu-bic-lookup.ps1`

Reduces 1,500+ branch office entries to 179 bank-level mappings.

**Run**:
```powershell
.\src\Application\Features\Iban\Data\sources\HU\build-hu-bic-lookup.ps1
```

## Maintenance & Data Updates

### Local CSV Files

1. Obtain updated data from provider
2. Regenerate or update the CSV file
3. Run tests to validate
4. Commit changes

For countries with generator scripts (e.g., HU), update the source file and re-run the generator.

### External Providers

The external provider service should:
- Log all external API calls for monitoring
- Cache results per IBAN to minimize requests
- Handle provider errors gracefully (return `IsFound = false`)
- Support health checks if available

## Configuration

### Cache Settings

All BIC lookups (local and external) are cached using the configured TTL:

**`appsettings.json`**:
```json
{
  "Cache": {
    "DefaultExpirationMinutes": 60,
    "IbanExpirationMinutes": 120
  }
}
```

## Monitoring & Diagnostics

### Logging

- **Info**: Successful local or external BIC resolution
- **Warning**: Missing CSV files or registry data
- **Error**: External provider failures or parsing errors

### Response fields

The IBAN endpoint includes metadata for auditing in the response body:
- `bicLookupSource`: provider identifier (e.g., "csv-file" or "openiban")
- `bicLookupStatus`: one of the values listed above
## Future Enhancements

1. **Expanded Local Coverage**: Add more countries (DE, ES, IT, NL, etc.)
2. **Caching Strategy**: Consider distributed cache for multi-instance deployments
3. **Batching**: Support bulk IBAN verification with external providers
4. **Fallback Chain**: Support multiple external providers with priority ordering
5. **Data Quality**: Add validation/unit tests for each country's lookup consistency
