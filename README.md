# Company Info API

A .NET 9 REST API service for retrieving and validating company information across multiple European registries and data sources.

## Features

| Feature | Description | External API |
|---------|-------------|-------------|
| **TVA Calculator** | Generate French intra-community VAT number from SIREN | Local computation |
| **IBAN** | Validate IBAN locally and resolve BIC from local files (5 countries) or external providers | Local files + optional external fallback |
| **VIES** | Validate EU VAT number format (local) and check active status via VIES | [EU VIES service](https://ec.europa.eu/taxation_customs/vies/) |
| **CH IDE** | Look up and validate Swiss UID numbers | [Swiss UID Registry](https://www.uid.admin.ch/) |
| **INSEE** | Look up French establishments by SIRET | [INSEE SIRENE API](https://api.insee.fr/) |
| **BODACC** | Search French bankruptcy/dissolution records | [BODACC OpenDataSoft](https://bodacc-datadila.opendatasoft.com/) |

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run

```bash
dotnet run --project src/CompanyInfo.Api.csproj
```

The API starts on `http://localhost:5035` by default.

### Build

```bash
dotnet build src/CompanyInfo.Api.csproj
```

### Test

```bash
# Unit tests only
dotnet test tests/CompanyInfo.Api.Tests.csproj --filter "FullyQualifiedName~.Unit."

# Architecture tests only
dotnet test tests/CompanyInfo.Api.Tests.csproj --filter "FullyQualifiedName~.Architecture."

# All tests
dotnet test tests/CompanyInfo.Api.Tests.csproj
```

## API Endpoints

All endpoints require the `X-Api-Key` header for authentication.

All endpoints support JSON (default) and XML response formats via:
- Query parameter: `?format=json` or `?format=xml`
- `Accept` header: `application/json` or `application/xml`

### TVA Calculator

```
GET /api/v1/tva-calculator?siren={siren}
```

Calculates the French intra-community VAT number from a 9-digit SIREN number.

### IBAN Verification

```
GET /api/v1/iban/verify?iban={iban}
```

Validates an IBAN locally using country length rules and MOD-97, then resolves the BIC using a two-tier strategy:
1. **Local Lookup** (fast): Checks local CSV files for countries with bundled BIC data (CZ, FR, HU, PL, SK)
2. **External Fallback** (when needed): Queries external BIC provider services for other countries

See [iban-bic-external-providers.md](docs/iban-bic-external-providers.md) for detailed architecture and configuration.

### VIES VAT Validation

```
GET /api/v1/vies/validate?vatNumber={vatNumber}
```

Validates the **format** of an EU VAT number locally — no external service call, always fast and reliable.

```
GET /api/v1/vies/check-active?vatNumber={vatNumber}
```

Checks whether a VAT number is currently **active** in the EU VIES system. Calls the external VIES API with retry logic for `MS_MAX_CONCURRENT_REQ` errors. Results are cached.

### CH IDE (Swiss UID)

```
GET /api/v1/ch-ide/{uid}
GET /api/v1/ch-ide/{uid}/validate
```

Looks up or validates a Swiss UID number (`CHE-XXX.XXX.XXX` format) via the Swiss federal UID registry SOAP API.

### INSEE (French SIRET)

```
GET /api/v1/insee/establishments/{siret}
```

Retrieves establishment information from the French INSEE SIRENE API. Requires an INSEE API key configured in settings.

### BODACC

```
GET /api/v1/bodacc/search?companyName={name}&registrationNumber={siren}
```

Searches BODACC records for bankruptcies, dissolutions, and liquidations. At least one of `companyName` or `registrationNumber` is required.

## Configuration

See [CONFIGURATION.md](CONFIGURATION.md) for detailed configuration options.

Key settings in `appsettings.json`:

| Setting | Description |
|---------|-------------|
| `ApiKeys` | Array of valid API keys for authentication |
| `Cache:*ExpirationMinutes` | Per-feature cache TTLs |
| `Iban:RegistryFilePath` | Path to IBAN registry text file (relative to app base directory or absolute) |
| `ExternalApis:Insee:ApiKey` | INSEE SIRENE API key |
| `ExternalApis:ChIde:BaseUrl` | Swiss UID registry SOAP endpoint |
| `ExternalApis:Bodacc:BaseUrl` | BODACC OpenDataSoft API endpoint |

IBAN registry path is configurable so the parser can be pointed to alternate files in different environments and tests:

```json
{
  "Iban": {
    "RegistryFilePath": "Application/Features/Iban/Data/iban-registry-rules.txt"
  }
}
```

- Relative paths are resolved from the app base directory.
- Absolute paths are used as-is.

## Project Structure

```
src/
  Application/
    Features/
      TvaCalculator/     # TVA number calculation
      Iban/              # IBAN validation + local BIC lookup
      Vies/              # EU VAT validation
      ChIde/             # Swiss UID lookup
      Insee/             # French SIRET lookup
      Bodacc/            # BODACC bankruptcy records
  Shared/
    Attributes/          # DI registration attribute
    Converters/          # JSON converters
    Extensions/          # Service registration extensions
    Filters/             # Response format filter
    Middleware/           # Exception handling
    Security/            # API key authentication
tests/
  Architecture/          # Architecture constraint tests
  Unit/                  # Unit tests per feature
docs/
  *.http                 # REST Client test files
```

## Architecture

- **Clean Architecture** with feature-based co-location
- Features are independent — no cross-feature dependencies (enforced by architecture tests)
- Auto-registration of services via `[RegisterService]` attribute
- In-memory caching with configurable TTLs per feature
- API key authentication via `X-Api-Key` header
- IBAN verification is fully local; BIC resolution uses feature-local seed files derived from public central-bank data, with an optional external fallback provider when local data is missing

## Development

### Formatting

The project uses [CSharpier](https://csharpier.com/) for code formatting, enforced via a [Husky](https://alirezanet.github.io/Husky.Net/) pre-commit hook.

### HTTP Test Files

Test `.http` files are available in the `docs/` folder for use with the VS Code [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-clients) extension.
