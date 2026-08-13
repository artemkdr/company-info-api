# ZAP DAST Scanner

Reusable OWASP ZAP DAST scanner via Docker. Supports OpenAPI spec-driven and full spider scans.

## ⚠️ Important Safety Notice

This scanner performs **ACTIVE security testing** that sends malicious payloads to your API endpoints. While this API is read-only (no POST/PUT/DELETE endpoints), active scanning can still:

- Cause denial of service through aggressive requests
- Exhaust rate limits on external APIs (Insee, ChIDE, etc.)
- Pollute caches with invalid data
- Generate significant traffic/load

**DO NOT run this against production systems.**

## Prerequisites

- Docker installed and running
- Target application running and reachable (when using manual mode)

## Quick Start

### Option 1: Automated Mode (Recommended)

Run both the API and ZAP in containers with a single command:

```bash
# From the security/dast directory
docker compose -f docker-compose.dast.yml up --abort-on-container-exit
```

This will:

1. Build and start the API service in a container
2. Run ZAP DAST scan against it
3. Generate HTML/JSON/XML reports in `security/dast/reports/`

### Option 2: Manual Mode

If you have the API running elsewhere:

```bash
chmod +x run-zap.sh
./run-zap.sh --openapi http://localhost:5000/swagger/1.0/swagger.json
```

## Usage Options

### With OpenAPI spec (URL or file)

```bash
# From URL
./run-zap.sh --openapi http://localhost:5000/swagger/1.0/swagger.json

# From local file
./run-zap.sh --openapi ./swagger.json
```

### Full spider scan (discovers endpoints automatically)

```bash
./run-zap.sh --target http://localhost:8080
```

### With API key authentication

```bash
./run-zap.sh --openapi http://localhost:5000/swagger/1.0/swagger.json --api-key your-api-key
```

### Custom scan policy

```bash
./run-zap.sh --openapi http://localhost:5000/swagger/1.0/swagger.json --policy ./custom.policy
```

### Different report formats

```bash
./run-zap.sh --openapi http://localhost:5000/swagger/1.0/swagger.json --report-format json
```

## Script Options

```text
Usage: run-zap.sh [OPTIONS]

Modes (one required):
  --openapi <url|file>     OpenAPI/Swagger spec URL or file path
  --target <url>           Target base URL (spider + full scan)

Options:
  --api-key <key>          API key for authentication
  --header-name <name>     Auth header name (default: X-API-Key)
  --policy <file>          Custom scan policy config file
  --report-format <fmt>    Report format: html|json|xml (default: html)
  --network <name>         Docker network to attach to (default: host)
  -h, --help               Show this help

Environment:
  ZAP_I_KNOW_WHAT_IM_DOING=true   Skip interactive safety confirmation (for CI)
```

## Docker Compose Mode

The `docker-compose.dast.yml` file provides an isolated environment:

- API service built from the current source
- ZAP scanner with pre-configured policies
- Reports generated to `./reports/` directory
- Ephemeral network (no external connections)

To customize the scan in docker-compose mode, edit the `command` in the `zap` service.

## CI/CD Integration

Example GitLab CI job (see `zap-dast-ci.yml`):

```yaml
zap-dast:
  stage: test
  image: docker:latest
  services:
    - docker:dind
  variables:
    ZAP_I_KNOW_WHAT_IM_DOING: "true"
  script:
    - chmod +x ./run-zap.sh
    - ./run-zap.sh --openapi http://app:8080/swagger/1.0/swagger.json --api-key "$ZAP_DAST_API_KEY" --network "host"
  artifacts:
    when: always
    paths:
      - reports/
  allow_failure: true  # Remove to block pipeline on findings
```

## Report Outputs

Reports are generated in the `reports/` directory:

- `report.html` - Human-readable HTML report
- `report.json` - Machine-readable JSON report
- `report.xml`  - XML report (for tools that consume ZAP XML)

## Customization

### Scan Policies

You can provide a custom ZAP policy file with `--policy` option. See [ZAP Documentation](https://www.zaproxy.org/docs/desktop/ui/dialogs/scanpolicy/) for policy file format.

### Authentication

The script supports header-based API key authentication. For more complex auth scenarios, you may need to modify the script or use ZAP's GUI.

### Network Configuration

By default, the script uses Docker's `host` network for simplicity. For isolated testing, you can specify a custom network with `--network`.

## Troubleshooting

### "URL matches production pattern" error

The safety gate blocks URLs that match common production patterns. If you're sure you're scanning a safe environment, you can modify the `PROD_PATTERNS` array in the script.

### Permission denied on reports directory

Ensure Docker has write permissions to the `reports/` directory:

```bash
chmod 777 security/dast/reports
```

### ZAP container fails to start

Check that Docker is running and you have internet access to pull the ZAP image:

```bash
docker pull ghcr.io/zaproxy/zaproxy:stable
```
