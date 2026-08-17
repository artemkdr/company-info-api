# ZAP DAST Agent

## 1. Philosophy

```text
DETERMINISTIC, AF-NATIVE, MULTI-STATE
├── Start with zero assumptions (like a real attacker)
├── Pass 1: scan the safety-approved API contract via AF Baseline YAML
├── Pass 2: add context, authentication, and replacer rules via AF Enriched YAML
├── Layer in context ONLY to:
│   ├── Reduce false positives via AF alertFilters
│   ├── Add attack vectors the scanner would miss (OAST for SSRF)
│   └── Provide valid test data (OpenAPI examples)
└── Triage is strictly deterministic; the LLM configures rules, but a script executes them.
```

## 2. Phases & Agent State Machine

```text
STATE 1: INTAKE AGENT ──────── Parse OpenAPI + limited source chunks
STATE 2: SAFETY AGENT ──────── Generate dynamic preflight inspection scripts
STATE 3: THREAT MODEL AGENT ── Classify archetype, map attack surface
STATE 4: AF COMPILER AGENT ─── Produce ZAP Automation Framework (AF) YAMLs
STATE 5: EXECUTION RUNNER ──── Run dynamic preflight, baseline AF, enriched AF
STATE 6: TRIAGE RUNNER ─────── Execute deterministic regex/rule triage (No LLM)
```

## 3. Phase 1 — Discovery & Intake

### Intake Limits & Chunking (Context Window Protection)
- **OpenAPI Spec:** Parsed entirely ONLY if within strict size limits.
- **Hard Limit Enforcement:** Before LLM intake, the local runner script measures the specification file.
  - **Threshold:** > 5,000 lines (or ~15,000 tokens).
  - **Action if exceeded:** BLOCK execution.
  - **Fatal Error Output:** "OpenAPI specification exceeds LLM context safety limits. You must split your specification into bounded domain contexts (e.g., /users, /billing) and configure them as separate ZAP CI jobs. Silent truncation causes false negatives."
- **Source Code (Strict Limits):**
  - Limit ingestion to routing files, middleware definitions, and database connection modules.
  - Exclude business logic implementations to prevent context exhaustion.
- **Infrastructure:** Dockerfile and `docker-compose.yaml` ingested for topology mapping.


## 4. Phase 2 — Safety Pre-flight (Dynamic Inspection)

Safety checks are strictly **dynamic** and execute post-boot but BEFORE execution of the scan passes. 

```yaml
runtime_preflight:
  required_for: [pass_1_baseline, pass_2_enriched, CI]
  execution_phase: post-container-boot, pre-zap-launch
  verify:
    - "Native Validation: Validate generated zap-af-baseline.yaml and zap-af-enriched.yaml using ZAP's native `-autocheck` flag (e.g., `zap.sh -cmd -autocheck /zap/wrk/config/zap-af-baseline.yaml`). Fail immediately on syntax or structural errors."
    - "Dynamic network inspect: Ensure target container has no host network mappings."
    - "Dynamic port inspect: Ensure no host-bound published ports (e.g., 0.0.0.0:8080)."
    - "Volume inspect: Verify DB containers are using tmpfs or isolated, ephemeral volumes."
    - "Env inspect: Dump target env vars; grep for production credential patterns."
  result:
    green: "Permit the requested pass."
    red: "Block without override. Fail pipeline."
```

## 5. Phase 3 — Threat Model & Archetype Classification

Classify the target into archetypes (e.g., CRUD, Orchestrator, File Processor). Priority vectors inform the ZAP Active Scan Policy block in the AF YAML.

```yaml
threat_model_schema:
  target: string
  archetypes: [list]
  priority_vectors: [list of ZAP rule IDs to boost]
  coverage_gaps: [list of limits, e.g., race conditions]
  false_positive_rules:
    - rule_id: int
      url_regex: string
      action: SUPPRESS | DOWNGRADE # Mapped directly to AF alertFilter
```

## 6. Phase 4 — AF Compiler (Configuration Generation)

### 6.1 Output Artifacts

```text
zap-dast/
├── docker-compose.yaml          # Includes target, mock-upstreams, and zap-auth-proxy sidecar
├── config/
│   ├── zap-af-baseline.yaml     # Pass 1: Unauthenticated baseline scan
│   ├── zap-af-enriched.yaml     # Pass 2: OpenAPI-guided, alertFilters, authenticated via proxy
│   ├── openapi-enriched.json    # Generated OpenAPI 3.0 spec with example test values
│   └── openapi-params.json      # Test data for parameter enrichment (reserved)
├── scripts/
│   ├── generate-openapi.sh      # Generate enriched OpenAPI spec
│   ├── preflight.sh             # Pre-flight validation: containers, isolation, AF syntax
│   └── triage-report.py         # Post-scan triage: applies threat-model rules
├── reports/                      # Scan output directory (gitignored)
│   └── YYYY-MM-DD-HH:MM:SS-baseline/   # Timestamped baseline scan
│       ├── report-baseline.html        # HTML report from baseline scan
│       └── report-baseline.json        # JSON report from baseline scan
│   └── YYYY-MM-DD-HH:MM:SS-enriched/   # Timestamped enriched scan
│       ├── report-enriched.html        # HTML report from enriched scan
│       ├── report-enriched.json        # JSON report from enriched scan (input to triage)
│       └── report-enriched.triaged.json  # Triaged JSON with suppressions applied
└── README.md
```

### 6.2 ZAP Automation Framework (AF) Generation Contract

The agent generates standard AF YAMLs conforming to ZAP's Automation Framework schema.

**Context Definition (Required):**
* Define at least one context in `env.contexts` with target URLs.
* Example:
  ```yaml
  env:
    contexts:
      - name: "Default Context"
        urls:
          - "http://zap-auth-proxy:80/"
  ```

**Pass 1 (`zap-af-baseline.yaml`) Pipeline:**
* `spider` job: discovers endpoints starting from the target URL.
* `activeScan` job: runs active vulnerability scanning against discovered endpoints.
* `report` job: generates HTML report with `reportDir`, `reportFile`, and `reportTitle` parameters.

**Pass 2 (`zap-af-enriched.yaml`) Pipeline:**
* `openapi` job: imports the enriched OpenAPI spec with example values, providing better parameter fuzzing guidance
* `spider` + `activeScan` jobs: same as baseline, but now discovering from the OpenAPI spec
* `alertFilter` job: applies threat-model false-positive suppressions (SUPPRESS/DOWNGRADE by rule ID and URL pattern)
* `report` job: generates HTML report (JSON available via separate export)
* Proxy always injects API key via `X-Api-Key` header for authenticated testing

**Authentication (Auth Proxy Sidecar):**
* `env.contexts.authentication` is NOT used. Instead, the `zap-auth-proxy` sidecar injects credentials.
* Configure both AF YAMLs with `targetUrl` pointing to `http://zap-auth-proxy:80`.
* The proxy injects `X-Api-Key: <test-key>` header before routing to the actual API.

**Important Parameter Notes:**

* `openapi` job: imports an OpenAPI specification to guide discovery
  - `apiFile`: local file path to OpenAPI JSON spec
  - `context`: name of the context to use (must exist in `env.contexts`)
  - `targetUrl`: optional override of the target server URL
  - Adds discovered endpoints to the context for scanning

* `alertFilter` job: suppresses or downgrades alerts by rule ID and URL pattern
  - `alertFilters`: list of filter rules
  - Each filter has `ruleId` (mandatory), `newRisk` (mandatory: 'False Positive', 'Info', 'Low', 'Medium', 'High')
  - Optional: `url` (string match) or `urlRegex` (regex match against alert URL)

* `spider`: endpoint discovery from a starting URL
  - `url`: starting URL for discovery
  - `maxDepth`: maximum crawl depth (integer)

* `activeScan`: active vulnerability scanning
  - `url`: target URL to scan
  - `policy`: ZAP policy name (e.g., "Default Policy")

* `report`: generate scan reports
  - `template`: report format ('traditional-html', 'traditional-json', 'modern', etc.)
  - `reportDir`: output directory path
  - `reportFile`: output filename (without extension if using templates)
  - `reportTitle`: human-readable report title

## 7. Phase 5 & 6 — Execution & Post-Scan Triage

### 7.1 CI Execution Flow

```bash
# 1. Boot target
docker compose up -d target db

# 2. Dynamic Preflight
./scripts/preflight.sh || exit 1

# 3. Baseline Scan
docker run -v $(pwd):/zap/wrk/:rw -t ghcr.io/zaproxy/zaproxy:stable zap.sh -cmd -autorun /zap/wrk/config/zap-af-baseline.yaml

# 4. Enriched Scan
docker run -v $(pwd):/zap/wrk/:rw -t ghcr.io/zaproxy/zaproxy:stable zap.sh -cmd -autorun /zap/wrk/config/zap-af-enriched.yaml

# 5. Deterministic Triage
python3 ./scripts/triage-report.py /zap/wrk/reports/report.enriched.raw.json
```

### 7.2 Deterministic Triage Logic (No LLM)

`triage-report.py` processes the enriched JSON report and applies threat-model rules without LLM involvement. Reclassification is deterministic based on static rules.

**Triage Operations:**
1. **Load threat-model.json** — read false-positive rules with rule IDs, URL patterns, and actions
2. **Apply suppressions** — for each alert, check if its rule ID + URL matches a suppression rule
   - If action is `SUPPRESS`: mark alert as `False Positive` (riskCode -1)
   - If action is `DOWNGRADE`: lower risk level one step (High → Medium, Medium → Low, etc.)
3. **Flag OAST findings** — check for OAST callbacks in alert evidence; if present, mark as `CONFIRMED SSRF` (Critical)
4. **Count remaining issues** — after all rules applied, count High and Medium alerts
5. **Determine exit code** — fail (exit 1) if any High or Medium remain; pass (exit 0) otherwise

**Pass/Fail Criteria:**
- **FAIL** (exit 1): Any alert with risk level High or Medium after triage
- **WARN** (report, exit 0): Low and Info alerts are logged but never fail the pipeline
- **PASS** (exit 0): No High or Medium severity findings after applying all rules

The triaged report (`report-enriched.triaged.json`) is saved for review, showing each alert's original and post-triage risk level.

## 8. Multi-Agent System Prompts

Instead of one monolithic prompt, the system utilizes specialized prompts based on the state machine.

### Agent 1: Intake & Threat Modeler

```text
ROLE: Analyze OpenAPI and limited source chunks to classify the API archetype.
TASK: Output a Threat Model JSON containing priority attack vectors, inferred auth schemes, and specific false-positive regex suppression rules. DO NOT generate execution configurations.
```

### Agent 2: Safety & Isolation Engineer

```text
ROLE: Ensure the test environment cannot harm external systems or host infrastructure.
TASK: Generate `docker-compose.yaml` utilizing tmpfs/ephemeral configurations. ALWAYS include a `zap-auth-proxy` sidecar service (Nginx/Envoy/Node) to inject credentials and route to the target API. Generate `scripts/preflight.sh` containing AF plan validation using ZAP's native `-cmd -autocheck` flag and dynamic Docker daemon inspection commands.
```

### Agent 3: ZAP AF Compiler

```text
ROLE: Translate the Threat Model into strictly valid ZAP Automation Framework (AF) YAML plans.
TASK: Generate `zap-af-baseline.yaml` and `zap-af-enriched.yaml`. Map Threat Model suppressions to `alertFilter` jobs. Map valid parameters into `openapi-enriched.yaml`. Enable OAST for SSRF. 
CONSTRAINT 1: Enriched pass targetUrl MUST point to the `zap-auth-proxy`, never configure ZAP native authentication jobs.
CONSTRAINT 2: Output MUST strictly adhere to the ZAP AF YAML structure to pass automated preflight `-autocheck` validation.
```
