# ZAP DAST Scanner

Reusable OWASP ZAP DAST scanner via Docker. Supports OpenAPI spec-driven and full spider scans.

## Prerequisites

- Docker installed and running
- Target application running and reachable

## Usage

### With OpenAPI spec (URL)

```bash
chmod +x run-zap.sh
./run-zap.sh --openapi http://localhost:8080/swagger/v1/swagger.json
```
