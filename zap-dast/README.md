# ZAP DAST Security Scan Setup

Deterministic, AF-native ZAP DAST pipeline for `CompanyInfo.Api` per `.github/agents/zap.agent.md`.

## Overview

The scan follows a 6-phase state machine:

1. **Intake** — Parse OpenAPI + limited source inspection
2. **Safety Preflight** — Dynamic Docker/network isolation checks, native AF syntax validation
3. **Threat Model** — API archetype classification and priority vectors (`threat-model.json`)
4. **AF Compiler** — Generate ZAP Automation Framework YAMLs
5. **Execution** — Run baseline (unauthenticated) + enriched (authenticated, OAST-enabled) scans
6. **Triage** — Deterministic rule-based suppression (no LLM), exit on confirmed High/Medium

## Directory structure

```text
zap-dast/
├── docker-compose.yaml           # Three-service setup: target, mock-upstreams, auth-proxy
├── mock-upstreams/               # Nginx stubs for INSEE/VIES/ChIde/BODACC/OpenIban
├── auth-proxy/                   # Nginx reverse proxy injecting X-Api-Key header
├── threat-model.json             # Phase 3 archetype + priority vectors + false positives
├── config/
│   ├── zap-af-baseline.yaml      # Pass 1: unauthenticated, safe methods only
│   ├── zap-af-enriched.yaml      # Pass 2: authenticated, OAST enabled, alertFilters
│   ├── openapi-params.json       # Test data for parameter enrichment
│   └── openapi-enriched.json     # Generated at runtime from target's /swagger/v1/swagger.json
├── scripts/
│   ├── generate-openapi.sh       # Fetch + enrich OpenAPI spec
│   ├── preflight.sh              # Health checks + native AF validation
│   └── triage-report.py          # Deterministic post-scan triage
├── reports/                      # Scan output (gitignored)
└── README.md                     # This file
```

## Running locally

### Prerequisites

- Docker (with `docker compose` v2+)
- `bash`
- ZAP (via container image, not required locally)

### Quick start

```bash
cd zap-dast

# 1. Boot the test environment (target, mocks, auth-proxy)
docker compose up -d --build

# 2. Wait ~30s for containers to be healthy
docker compose logs --follow target

# 3. Run baseline scan
docker run --rm \
  -v $(pwd):/zap/wrk:rw \
  --network zap-dast_zap-dast-network \
  ghcr.io/zaproxy/zaproxy:stable \
  zap.sh -cmd -autorun /zap/wrk/config/zap-af-baseline.yaml

# 4. Run enriched scan (authenticated via proxy)
docker run --rm \
  -v $(pwd):/zap/wrk:rw \
  --network zap-dast_zap-dast-network \
  ghcr.io/zaproxy/zaproxy:stable \
  zap.sh -cmd -autorun /zap/wrk/config/zap-af-enriched.yaml

# 5. View reports
# Reports are generated in HTML format in reports/ directory:
#   - reports/report-baseline.html
#   - reports/report-enriched.html

# 6. Clean up
docker compose down -v
```

### Interpreting results

- **Baseline scan** — spider + active scan against the auth-proxy, discovering vulnerabilities on the unauthenticated API surface.
- **Enriched scan** — leverages the OpenAPI spec (with example values) to guide discovery and fuzzing, applies threat-model-based alertFilters to suppress/downgrade known false positives, runs with authentication (proxy injects test API key).
- **Reports** — generated as HTML files in `reports/` directory (`report-baseline.html`, `report-enriched.html`).
- **Triage** — `scripts/triage-report.py` applies threat-model false-positive rules to determine pass/fail (fails on confirmed High or Medium, not on Low/Info).

Each report contains:
- Alert counts by risk and confidence
- Detailed alerts with evidence and remediation guidance
- Site/endpoint breakdown

## Mock upstreams

The test environment includes a mock Nginx container that stubs the 5 external registries:

- **INSEE** (8081) — returns JSON with a canned list of businesses
- **VIES** (8082) — returns XML with VAT validation
- **ChIde** (8083) — returns JSON with Swiss company data
- **BODACC** (8084) — returns JSON search results
- **OpenIban** (8085) — returns JSON IBAN validation results + `/health` endpoint

Target's env vars point to these mocks (e.g., `ExternalApis__Insee__BaseUrl=http://mock-upstreams:8081`). No real government APIs are called during scanning.

## Auth proxy

The `zap-auth-proxy` service is a plain Nginx reverse proxy that:

1. Intercepts all requests on port 8080
2. Injects the header `X-Api-Key: test-api-key-12345`
3. Routes to the target API at `target:5000`

Per `.github/agents/zap.agent.md` § 6.2, native ZAP authentication jobs are never used. Instead:

- Baseline scan targets the target directly (no auth, tests public endpoints)
- Enriched scan targets the proxy (requests auto-authenticated)

The target is configured with `ApiKeys__0=test-api-key-12345` so the injected key is valid.

## Threat model

`threat-model.json` classifies this API as a **Data Aggregator / Orchestrator** archetype:

- **Priority vectors**: Injection into external API URL construction, input validation bypass, auth misconfiguration
- **Coverage gaps**: SQL injection (no DB), CSRF (no sessions), file upload
- **False positives**: Session cookies (not used), X-Frame-Options on health endpoint (public by design), XSS on JSON APIs
- **External APIs**: 5 upstream registries (INSEE, VIES, ChIde, BODACC, OpenIban)
- **Attack surface**: GET-only REST, 6 + 1 health endpoints, single X-Api-Key header auth

All suppressions and downgrades are applied deterministically in `triage-report.py`, not by LLM.

## CI integration

See `.github/workflows/zap-dast.yml` for the full CI workflow.

Trigger: `workflow_dispatch` (manual)

Steps:

1. Checkout
2. `docker compose up -d --build`
3. `generate-openapi.sh`
4. `preflight.sh` (fails if isolation or validation checks fail)
5. Baseline scan
6. Enriched scan
7. `triage-report.py` (exits 0/1 based on High/Medium count)
8. Upload `reports/` as artifact
9. `docker compose down -v`

## Extending the scan

**Adjusting scan depth and policy:**
- Edit `zap-af-baseline.yaml` and `zap-af-enriched.yaml` to modify spider `maxDepth`, active scan `policy`, or target URLs.

**Changing mock upstream responses:**
- Edit `mock-upstreams/nginx.conf` and re-run `docker compose up --build` to change mock API responses.

**Updating the target configuration:**
- Modify `docker-compose.yaml` env vars to change external API mock URLs or update `auth-proxy/nginx.conf` to alter header injection behavior.

## Troubleshooting

#### Scan fails to run

Check that all containers are running:
```bash
docker compose ps
```

Check container logs for errors:
```bash
docker compose logs company-info-api
docker compose logs zap-auth-proxy
docker compose logs mock-upstreams
```

#### AF validation fails

Run with verbose output:

```bash
docker run --rm \
  -v $(pwd):/zap/wrk:ro \
  ghcr.io/zaproxy/zaproxy:stable \
  zap.sh -cmd -autocheck /zap/wrk/config/zap-af-baseline.yaml -d
```

### Auth proxy not reaching target

Check target is healthy:

```bash
docker compose logs target
```

Then test from proxy:

```bash
docker exec zap-auth-proxy wget -O- http://target:5000/api/v1/health
```

### OpenAPI enrichment failed

Ensure target is booted and accessible:

```bash
curl -v http://localhost:8080/swagger/v1/swagger.json
```

(Note: port 8080 is the proxy's public port; internally it's port 80.)

### Scan hangs or times out

ZAP scans can take several minutes. Monitor logs:

```bash
docker run --rm \
  -v $(pwd):/zap/wrk:rw \
  --network zap-dast_zap-dast-network \
  ghcr.io/zaproxy/zaproxy:stable \
  zap.sh -cmd -autorun /zap/wrk/config/zap-af-baseline.yaml -d
```

The `-d` flag enables debug output.

## Further reading

- [ZAP Automation Framework docs](https://www.zaproxy.org/docs/automate/automation-framework/)
- [`.github/agents/zap.agent.md`](../.github/agents/zap.agent.md) — the original specification
