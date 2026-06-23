# Configuration Guide

All configuration is managed through `appsettings.json` (and environment-specific overrides like `appsettings.Development.json`). Sensitive values should be stored via [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) or environment variables.

## Development Setup

### Restore tools
To restore the .NET tools and first setup, run:
```bash
dotnet tool restore
dotnet husky install
```
The tools include:
- `csharpier` for code formatting: 
  - `dotnet csharpier format .`
  - `dotnet csharpier check .`
- `husky` for git hooks:
  - `dotnet husky install` to set up git hooks (do it once in your local repo)
  - `dotnet husky run` to run hooks manually
  - 

### Using User Secrets (Recommended for Local Development)

The project is already configured with User Secrets. The sensitive configuration has been moved to User Secrets storage.

To view current secrets:
```bash
dotnet user-secrets list
```

To add/update a secret:
```bash
dotnet user-secrets set "ApiKeys:0" "your-api-key"

## Authentication

```json
{
  "ApiKeys": ["your-api-key-1", "your-api-key-2"]
}
```

| Setting | Type | Description |
|---------|------|-------------|
| `ApiKeys` | `string[]` | List of valid API keys. Clients must pass one via the `X-Api-Key` header. |

## CORS

```json
{
  "AllowedOrigins": "http://localhost:3000,https://app.example.com"
}
```

| Setting | Type | Description |
|---------|------|-------------|
| `AllowedOrigins` | `string` | Comma-separated list of allowed CORS origins. |

## Cache

All cache durations are in **minutes**.

```json
{
  "Cache": {
    "DefaultExpirationMinutes": 60,
    "ViesExpirationMinutes": 120,
    "ChIdeExpirationMinutes": 1440,
    "InseeExpirationMinutes": 1440,
    "BodaccExpirationMinutes": 720,
    "IbanExpirationMinutes": 1440
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `DefaultExpirationMinutes` | `60` | Fallback cache duration if a feature-specific value is not set. |
| `ViesExpirationMinutes` | `120` | Cache duration for VIES VAT validation results (2 hours). |
| `ChIdeExpirationMinutes` | `1440` | Cache duration for Swiss UID lookups (24 hours). |
| `InseeExpirationMinutes` | `1440` | Cache duration for INSEE establishment data (24 hours). |
| `BodaccExpirationMinutes` | `720` | Cache duration for BODACC search results (12 hours). |
| `IbanExpirationMinutes` | `1440` | Cache duration for IBAN validation results (24 hours). |

## IBAN Registry File

The IBAN verification service uses a master reference file to validate IBAN formats and extract country-specific components.

### File: `iban-registry-rules.txt`

**Default Location**: `src/Application/Features/Iban/Data/iban-registry-rules.txt`

**Configuration Key**: `Iban:RegistryFilePath`

```json
{
  "Iban": {
    "RegistryFilePath": "Application/Features/Iban/Data/iban-registry-rules.txt"
  }
}
```

Path behavior:

- Relative path: resolved from the application base directory (`AppContext.BaseDirectory`)
- Absolute path: used as-is

Environment variable override example:

```bash
Iban__RegistryFilePath=/opt/company-info/iban-registry-rules.txt
```

**Purpose**: Contains authoritative IBAN structure definitions (formats, lengths, field patterns) for all IBAN-supporting countries

**Source**: [SWIFT IBAN Registry](https://www.swift.com/standards/data-standards/iban) (published and maintained by SWIFT)

**What It Contains**:

For each country with valid IBAN support, the file includes:

- **IBAN Structure Pattern**: Formal definition of field positions and types (e.g., `2!n8!n10!n`)
- **IBAN Length**: Expected total length after normalization
- **Bank Identifier Position/Pattern**: Where the bank code is located and its format
- **Branch Identifier Position/Pattern**: Where the branch code is located (if applicable)
- **Contact Information**: Banking authority email and phone for that country
- **Effective Date**: When the current specification became effective

**How It's Used**:

The `IbanRegistryParser` loads/parses this file (used by `IbanService` for structure + length validation) on first use to:
1. Extract country codes and their IBAN structure patterns
2. Convert patterns to regex validation rules (e.g., `DE2!n8!n10!n` → `^DE[0-9]{2}[0-9]{8}[0-9]{10}$`)
3. Use regex rules to validate IBAN format and structure for each country
4. Extract country-specific component positions (bank code, branch code) for local BIC lookup

**Example**:
```
IBAN prefix country code (ISO 3166):   ...DE...FR...
IBAN structure:                        ...DE2!n8!n10!n...FR2!n5!n5!n11!c2!n...
IBAN length:                           ...22...27...
Bank identifier position within BBAN: ...1-8...1-5...
```

**Fallback Behavior**:

If the registry file is unavailable or cannot be parsed, the service falls back to built-in validation patterns for a subset of countries (CH, DE, ES, FR, MC, NL). All other countries will fail validation if the registry file is missing.

**Maintenance**:

1. SWIFT publishes updates to the IBAN registry when countries change their IBAN specifications
2. **To update**: Download the latest registry file from [SWIFT Standards](https://www.swift.com/standards/data-standards/iban)
3. **Replace** the file in the repository
4. **Test**: Run unit tests to validate format parsing
5. **Commit**: Reference the SWIFT release bulletin in the commit message

**Related Documentation**:

See [iban-bic-external-providers.md](docs/iban-bic-external-providers.md#iban-registry-infrastructure) for detailed architecture and examples.

---

## Local BIC Lookup Data Files

The IBAN verification service uses a hybrid approach:

1. **Local CSV files** (primary) for fast, offline BIC resolution in supported countries
2. **External BIC provider** (fallback) for countries without local data

### Supported Countries with Local Data

| Country | Code | File | Lookup Key Pattern |
|---------|------|------|-------------------|
| Czech Republic | CZ | `CZ-bic-lookup.csv` | 4-digit bank code |
| France | FR | `FR-bic-lookup.csv` | 5-digit bank code + optional 5-digit branch |
| Hungary | HU | `HU-bic-lookup.csv` | 3-digit bank code |
| Poland | PL | `PL-bic-lookup.csv` | 8-digit bank code |
| Slovakia | SK | `SK-bic-lookup.csv` | 4-digit bank code |

**Location**: `src/Application/Features/Iban/Data/{CC}-bic-lookup.csv`

### CSV Format

```text
CountryCode,LookupKey,Bic,BankCode,BranchCode,BankName,Source
```

**Columns**:
- `CountryCode`: Two-letter ISO country code (e.g., "FR")
- `LookupKey`: Country-specific lookup key format (see table above)
- `Bic`: 8 or 11 character BIC code
- `BankCode`: Optional; extracted bank code for reference
- `BranchCode`: Optional; branch code if applicable
- `BankName`: Optional; institution name for context
- `Source`: Attribution/documentation of data source

### Adding a New Country

See [iban-bic-external-providers.md](docs/iban-bic-external-providers.md) for step-by-step instructions.

When importing new files:
- Keep provenance notes with source URL, download date, and transformation notes
- Test with valid IBAN examples from the target country
- Run the unit tests to validate consistency

### Cache Configuration

All BIC lookups (local and external) are cached using `IbanExpirationMinutes`:

```json
{
  "Cache": {
    "IbanExpirationMinutes": 1440
  }
}
```

See [Cache](#cache-configuration) section for more details.

## External APIs

### INSEE SIRENE

```json
{
  "ExternalApis": {
    "Insee": {
      "BaseUrl": "https://api.insee.fr/api-sirene/3.11",
      "ApiKey": ""
    }
  }
}
```

| Setting | Description |
|---------|-------------|
| `ExternalApis:Insee:BaseUrl` | INSEE SIRENE API base URL. |
| `ExternalApis:Insee:ApiKey` | Your INSEE API key. Obtain one from [api.insee.fr](https://api.insee.fr/). **Store in User Secrets or environment variable.** |

To set the INSEE API key via User Secrets:

```bash
cd src
dotnet user-secrets set "ExternalApis:Insee:ApiKey" "your-insee-api-key"
```

Or via environment variable:

```bash
ExternalApis__Insee__ApiKey=your-insee-api-key
```

### CH IDE (Swiss UID Registry)

```json
{
  "ExternalApis": {
    "ChIde": {
      "BaseUrl": "https://www.uid-wse.admin.ch/V5.0/PublicServices.svc"
    }
  }
}
```

| Setting | Description |
|---------|-------------|
| `ExternalApis:ChIde:BaseUrl` | Swiss UID SOAP web service endpoint. No API key required. |

### BODACC (OpenDataSoft)

```json
{
  "ExternalApis": {
    "Bodacc": {
      "BaseUrl": "https://bodacc-datadila.opendatasoft.com/api/explore/v2.1"
    }
  }
}
```

| Setting | Description |
|---------|-------------|
| `ExternalApis:Bodacc:BaseUrl` | BODACC OpenDataSoft API base URL. No API key required. |

## Swagger

```json
{
  "SwaggerUrlPrefix": ""
}
```

| Setting | Description |
|---------|-------------|
| `SwaggerUrlPrefix` | URL prefix for the Swagger UI. Set to `""` for root or a path like `"api-docs"`. |

## Kestrel

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5035"
      }
    }
  }
}
```

| Setting | Description |
|---------|-------------|
| `Kestrel:Endpoints:Http:Url` | The URL and port Kestrel listens on. Default: `http://0.0.0.0:5035`. |

## Logging

Logging is configured via [NLog](https://nlog-project.org/). See `nlog.config` for target configuration.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Environment-Specific Overrides

The `appsettings.Development.json` file provides shorter cache TTLs and debug-level logging for local development. Any setting can be overridden per environment using the standard ASP.NET Core configuration pattern:

- `appsettings.{Environment}.json`
- Environment variables (use `__` as separator, e.g. `ExternalApis__Insee__ApiKey`)
- User Secrets (Development only)
