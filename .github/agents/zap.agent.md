# ZAP DAST Agent — Specification v3 (AF-Native)

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

Safety checks are strictly **dynamic** and execute post-boot but BEFORE ZAP initiates. 

```yaml
runtime_preflight:
  required_for: [pass_1_baseline, pass_2_enriched, CI]
  execution_phase: post-container-boot, pre-zap-launch
  verify:
    - "Schema Validation: Validate generated zap-af-baseline.yaml and zap-af-enriched.yaml against the official ZAP AF JSON schema using a local validator (e.g., ajv-cli). Fail immediately on syntax or schema errors."
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
├── docker-compose.yaml          # Includes target, DB (tmpfs), and zap-auth-proxy Sidecar
├── config/
│   ├── zap-af-baseline.yaml     # Pass 1: Unauthenticated, strictly safe methods
│   ├── zap-af-enriched.yaml     # Pass 2: Targets auth-proxy, full methods, replacers, OAST
│   └── openapi-enriched.yaml    # Native OpenAPI spec with injected valid example data
├── scripts/
│   ├── preflight.sh             # Dynamic daemon inspection + YAML schema validation
│   └── triage-report.py         # Post-scan JSON parser
├── reports/
│   └── <scan-id>/               # Immutable raw HTML/JSON and triaged JSON
└── README.md
```

### 6.2 ZAP Automation Framework (AF) Generation Contract

The agent generates standard AF YAMLs. Python hooks are strictly forbidden.

**Pass 1 (`zap-af-baseline.yaml`) Features:**

* `openapi` job pointing to raw spec.
* `activeScan` scoped explicitly to safe methods.

**Pass 2 (`zap-af-enriched.yaml`) Features:**

* `openapi` job pointing to `openapi-enriched.yaml`.
* **Authentication (Auth Proxy Sidecar)**: `env.contexts.authentication` MUST NOT be used. Configure the `targetUrl` to point to the `zap-auth-proxy` service. The proxy deterministically upgrades requests with credentials before routing to the actual API.
* `replacer` jobs for test headers/tenants.
* `alertFilter` jobs derived directly from the Threat Model.
* **SSRF Detection:** Native ZAP OAST add-on enabled in `activeScan` policy. Custom `mock-upstream` parsing is forbidden.
* `report` job exporting raw JSON.

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

`triage-report.py` enforces business logic without LLM hallucinations. Reclassification is disabled.

```yaml
triage_operations:
  - apply_downgrades: "Based on static JSON rules generated during Threat Model phase."
  - flag_oast: "If OAST URL is present in alert evidence, mark CONFIRMED SSRF (Critical)."

ci_pipeline_criteria:
  fail_if:
    - any_confirmed_high: true
    - any_confirmed_medium: true
  warn_only:
    - any_low_or_info: true # Never fail pipeline on accumulation of low/info findings.
```

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
TASK: Generate `docker-compose.yaml` utilizing tmpfs/ephemeral configurations. ALWAYS include a `zap-auth-proxy` sidecar service (Nginx/Envoy/Node) to inject credentials and route to the target API. Generate `scripts/preflight.sh` containing YAML schema validation (`ajv-cli`) and dynamic Docker daemon inspection commands.
```

### Agent 3: ZAP AF Compiler

```text
ROLE: Translate the Threat Model into strictly valid ZAP Automation Framework (AF) YAML plans.
TASK: Generate `zap-af-baseline.yaml` and `zap-af-enriched.yaml`. Map Threat Model suppressions to `alertFilter` jobs. Map valid parameters into `openapi-enriched.yaml`. Enable OAST for SSRF. 
CONSTRAINT 1: Enriched pass targetUrl MUST point to the `zap-auth-proxy`, never configure ZAP native authentication jobs.
CONSTRAINT 2: Output MUST strictly adhere to the official ZAP AF YAML schema to pass automated preflight validation.
```
