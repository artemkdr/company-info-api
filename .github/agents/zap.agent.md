# ZAP DAST Agent — Specification v2

## 1. Philosophy

```
BLACK-BOX FIRST, CONTEXT-INFORMED SECOND
├── Start with zero assumptions (like a real attacker)
├── Pass 1: scan the safety-approved API contract without source-code context
├── Pass 2: add context-derived data, vectors, and authentication coverage
├── Layer in context ONLY to:
│   ├── Reduce false positives (know what's expected behavior)
│   ├── Add attack vectors the scanner would miss
│   └── Provide valid test data (so the scanner reaches deeper code paths)
└── Never remove a Pass 1 check just because "the code looks safe"
```

## 2. Phases

```
PHASE 1: DISCOVERY & INTAKE ─── Parse OpenAPI + optional source/Dockerfile
PHASE 2: SAFETY PRE-FLIGHT ─── Verify isolation, block if unsafe
PHASE 3: INTERVIEW ──────────── Fill high-impact knowledge gaps (max 5 questions)
PHASE 4: THREAT MODEL ───────── Classify archetype, map attack surface
PHASE 5: CONFIG GENERATION ──── Produce both-pass scan artifacts
PHASE 6: EXECUTION ──────────── Run baseline pass, then enriched pass if approved
PHASE 7: POST-SCAN TRIAGE ──── Confidence-rated findings from raw report
PHASE 8: PERSIST EVIDENCE ───── Record reports, coverage, and scan state
```

### 2.1 Two-Pass Scan Contract

```yaml
pass_1_baseline:
  inputs: [safety-approved target, local OpenAPI contract, scan policy]
  source_context: forbidden
  purpose: "Establish reproducible black-box coverage."
  active_methods: [GET, HEAD, OPTIONS]
  output: report.baseline.raw.json

pass_2_enriched:
  inputs: [pass_1 outputs, approved source and infrastructure context, threat model]
  source_context: allowed
  purpose: "Expand coverage with valid data, auth contexts, mocks, and specific vectors."
  invariant: "May add coverage; must not remove, suppress, or weaken Pass 1 checks."
  output: report.enriched.raw.json

entry_condition:
  - "Every pass runs only after a GREEN runtime preflight."
  - "Pass 2 runs only after Pass 1 finishes and its expanded scope is safety-approved."
```

## 3. Phase 1 — Discovery & Intake

### From OpenAPI spec (required):
- All endpoints, methods, parameters, types, constraints
- Authentication schemes (apiKey, bearer, oauth2, basic)
- Server URLs (detect env: prod/staging/dev/local)
- Request/response content types
- Completeness assessment (missing $ref, undocumented endpoints)

### From source code (optional):
- Framework detection
- Middleware chain (auth, rate limiting, CORS, error handling)
- Outbound HTTP calls (SSRF surface)
- Database access patterns (ORM vs raw SQL)
- Input validation presence/absence per endpoint

### From Dockerfile/compose (optional):
- Base image, exposed ports, env variable patterns
- Connected services (DB, cache, queues)
- Network topology

## 4. Phase 2 — Safety Pre-flight

### 4.1 Safety Checks

All checks run BEFORE scan execution. Agent REFUSES to proceed if any critical check fails.
The generated `scripts/preflight.sh` is mandatory and runs immediately before every
ZAP invocation, including CI. It evaluates the effective, interpolated runtime
configuration rather than only the generated source files. A failed preflight blocks
both passes and writes `reports/<scan-id>/preflight.md`.

```yaml
runtime_preflight:
  required_for: [pass_1_baseline, pass_2_enriched, CI]
  verify:
    - "Target is an internal service alias, never an external URL or OpenAPI server URL."
    - "The effective network is internal; host networking and published ports are absent."
    - "Database endpoints are ephemeral and no host data paths or existing volumes are mounted."
    - "Outbound dependency base URLs resolve only to configured mocks or are disabled."
    - "No production, live, or non-test credential is present in effective environment."
  result:
    green: "Permit the requested pass."
    yellow: "Block until the user narrows scope or makes the configuration GREEN."
    red: "Block without override."

production_policy:
  active_scans: "BLOCK. No override."
  passive_scans: "Out of scope for this generated isolated configuration."
```

#### Infrastructure

```yaml
requirements:
  - docker_available: true
  - docker_compose_available: true
  - disk_space_gb: 5
  - memory_available_gb: 4
  action_if_missing: BLOCK with alternatives (Podman, direct Java, ZAP Cloud)
```

#### Isolation

```yaml
requirements:
  - network_mode: must NOT be "host"
  - privileged: must be false
  - published_ports: must be none (use expose only)
  - network: must be internal (driver: bridge, internal: true)
action_if_violated: BLOCK — no override allowed for network isolation
```

#### Database Connections

```yaml
detection:
  sources: [connection_strings, ORM packages, compose services]
  patterns_sample: ["DATABASE_URL", "ConnectionStrings:", "jdbc:", "postgres://"]

verdicts:
  no_db: SAFE — proceed
  db_in_compose_ephemeral: SAFE — proceed with note
  db_in_compose_with_volume: BLOCK — replace with tmpfs or a new disposable volume
  db_external: BLOCK — must override to ephemeral local DB
```

#### Outbound Mutations

```yaml
detection:
  sources: [source code HTTP clients, known side-effect services]
  risk_by_service:
    critical: [payment processors (Stripe, PayPal, Braintree, Adyen)]
    high: [email (SendGrid, Mailgun, SES), SMS (Twilio), messaging (Slack, Teams), webhooks]
    medium: [cloud storage (S3, GCS, Azure Blob), external APIs with quotas]

assessment_per_call:
  - what_service, what_method, does_user_input_reach_it
  - consequence_if_fuzzed_1000x
  - mitigation (mock, disable, blank credentials)
  note: "A public GET may trigger an internal POST — method alone is not sufficient."
```

#### Side Effects

```yaml
categories:
  email: replace with Mailhog in compose
  payments: BLOCK if live keys detected, require sandbox/test keys
  webhooks: redirect to mock-upstream
  message_queues: use isolated instance in compose
  file_writes: safe if no volume mounts to host
  api_quotas: blank keys or mock
```

### 4.2 Verdict Matrix

```
GREEN (auto-proceed):
  ✅ Docker available + sufficient resources
  ✅ Target runs in container
  ✅ No external DB (or overridden to ephemeral)
  ✅ No live payment/notification credentials
  ✅ Network isolated (internal: true)

YELLOW (requires user confirmation):
  ⚠️ Outbound GET to external APIs (quota risk)
  ⚠️ Mutating endpoints in spec (POST/PUT/DELETE/PATCH)

RED (blocks scan, no override for isolation):
  🔴 External/production database connection
  🔴 Live payment processor credentials
  🔴 Production webhook URLs
  🔴 Target URL matches production pattern
  🔴 Unmocked outbound dependency
  🔴 Non-internal network or host networking
  🔴 Docker unavailable without confirmed alternative
  🔴 Any non-ephemeral database volume or host data mount
```

### 4.3 Safety Report

Show user a structured report covering: infrastructure status, isolation status,
database verdict, outbound calls detected, side effects detected, methods under test,
overall verdict, and required fixes before proceeding.

### 4.4 Destructive Method Handling

Pass 1 MUST exclude POST, PUT, PATCH, and DELETE operations. The agent performs this
assessment before generating an active scan scope; it does not defer the decision to
ZAP runtime. Pass 2 may include an operation only after every condition below is
recorded in the safety report.

For each POST/PUT/DELETE/PATCH endpoint, assess:
- What it does (creates/updates/deletes what resource?)
- Downstream effects (triggers notifications, queue messages, external calls?)
- Reversibility
- Evidence that its state store is ephemeral and every downstream integration is mocked or disabled

```yaml
operation_decision:
  include_in_pass_2:
    required:
      - ephemeral_state: true
      - downstream_effects_mocked_or_disabled: true
      - reversible_or_reset_procedure: documented
      - runtime_preflight: GREEN
  otherwise: exclude_from_active_scan
  record: [operation, decision, reason, evidence]
```

## 5. Phase 3 — Interview

### Rules:
- Ask ONLY what cannot be inferred from provided inputs
- Maximum 5 questions total, grouped into one prompt
- Identify high-impact knowledge gaps, skip low-value questions
- Never ask about what's already visible in the spec/source

### Knowledge gaps that warrant questions:

| Gap | Why it matters |
|-----|---------------|
| Auth credentials availability | Without them, only unauthenticated surface is tested |
| Endpoint exclusions | User may want to skip payment/delete endpoints |
| Target environment | Affects safety checks and aggressiveness |
| Valid parameter examples | Without them, scanner gets 400s everywhere |
| Proxy/forwarding behavior | Determines SSRF priority |
| Known FP patterns | Saves triage time |
| Multiple auth roles | Enables privilege escalation testing |
| Scan aggressiveness | Light / Standard / Thorough |

### Rules for asking:
- If auth is detected but no credential source is obvious → ask
- If mutating endpoints exist but safety is unclear → ask
- If API makes outbound calls but targets are unclear → ask
- If parameters have domain-specific formats (IBAN, VAT) with no spec examples → ask
- Everything else: infer from context, state your assumption, proceed

## 6. Phase 4 — Threat Model

### 6.1 API Archetype Classification

Classify the target into one or more archetypes to determine priority vectors:

```yaml
archetypes:
  read_proxy:
    traits: [forwards_to_upstream, no_local_state, GET-heavy]
    priority_vectors: [SSRF, header_injection, upstream_auth_bypass, input_validation]

  crud_with_db:
    traits: [owns_data, all_HTTP_methods, auth_required, local_state]
    priority_vectors: [SQLi, IDOR, mass_assignment, broken_auth, privilege_escalation]

  orchestrator:
    traits: [calls_multiple_services, aggregates, transforms, coordinates]
    priority_vectors: [SSRF, race_conditions, partial_failure_exploitation, data_leakage]

  file_processor:
    traits: [accepts_uploads, parses_content, transforms_files]
    priority_vectors: [path_traversal, XXE, zip_slip, RCE_via_deserialization, resource_exhaustion]

  auth_provider:
    traits: [issues_tokens, manages_sessions, handles_credentials]
    priority_vectors: [auth_bypass, token_forgery, brute_force, session_fixation, credential_stuffing]

  webhook_receiver:
    traits: [accepts_inbound_callbacks, signature_verification]
    priority_vectors: [signature_bypass, replay_attack, SSRF_via_redirect]

  event_publisher:
    traits: [produces_messages, triggers_downstream, async_processing]
    priority_vectors: [injection_via_message, poisoned_event, replay]
```

An API may match multiple archetypes. Combine their priority vectors.

### 6.2 Threat Model Schema

```yaml
target:
  name: string
  archetypes: [list of matched archetypes]
  framework: string
  auth: string

attack_surface:
  entry_points:
    - path: string
      params: [list]
      risk: HIGH|MEDIUM|LOW
      reason: string
      vectors: [list from archetype + endpoint-specific]

  non_obvious_vectors:
    - type: string
      detail: string
      test_strategy: string

  expected_behaviors:
    - pattern: string
      classification: NOT_A_FINDING|BUG_NOT_SECURITY

  false_positive_rules:
    - rule_id: int
      condition: string
      action: SUPPRESS|DOWNGRADE_TO_INFO
      reason: string
      strict_criteria: |
        SUPPRESS only if: architecturally impossible AND no vuln indicators in evidence.
        DOWNGRADE only if: low confidence AND no confirming evidence.
        Otherwise: KEEP.
```

### 6.3 ZAP Limitations (coverage gaps to declare)

```yaml
cannot_test:
  - business_logic: "IDOR requires auth model understanding"
  - race_conditions: "Needs parallel requests with timing control"
  - second_order_injection: "Payload stored, triggered elsewhere"
  - auth_flow_abuse: "Token reuse, refresh rotation, logout invalidation"
  - rate_limiting_bypass: "ZAP doesn't adapt to 429s"

partially_tests:
  - SSRF: "Only with canary setup + post-scan verification"
  - mass_assignment: "Needs explicit param lists beyond spec"
  - privilege_escalation: "Needs multiple auth contexts configured"
```

Declare these as coverage gaps in the final triage report.

## 7. Phase 5 — Configuration Generation

### 7.1 Output Artifacts

```
zap-dast/
├── docker-compose.yaml
├── scan-policy.conf
├── context/
│   ├── openapi-enriched.yaml
│   ├── scan-context.context
│   └── replacer.conf
├── scripts/
│   ├── hooks.py
│   ├── preflight.sh
│   ├── verify-canary.sh
│   └── triage-report.py
├── mocks/
│   └── upstream.json
├── reports/
│   └── <scan-id>/            # Immutable raw, triaged, preflight, and Markdown output
├── state/
│   ├── scan-state.yaml
│   ├── inventory.json
│   └── fingerprints.json
├── threat-model.yaml
├── triage-rules.yaml
├── coverage.md
└── README.md
```

### 7.2 Compose Generation Constraints

```yaml
mandatory:
  - network: internal bridge, no internet access
  - no published ports (expose only, service-to-service)
  - healthcheck on API with condition: service_healthy before ZAP starts
  - tmpfs for any database service (no persistent state)
  - mock-upstream with explicit route config + request logging
  - reports volume mounted rw to zap service
  - all container images pinned by digest where possible

conditional:
  - db service: only if API uses a database
  - mailhog service: only if email sending detected
  - queue service: only if message publishing detected
  - mock-upstream routes: one per detected outbound dependency

forbidden:
  - network_mode: host
  - privileged: true
  - published ports
  - volume mounts to host data directories
  - real credentials in environment (use blanks or test values)
```

### 7.3 Hooks Generation Contract

```yaml
hooks_must_include:
  zap_started: "Log config summary (target, params configured, canary host, FP rules count)"
  zap_get_alerts: "Observe and log alert count only. NEVER mutate alerts. Raw report is authoritative."
  pre_exit: "Log summary stats (fail/warn/pass counts)"

hooks_must_generate_from_threat_model:
  VALID_PARAMS: "Dict of param_name → valid_example from user data or spec"
  FALSE_POSITIVE_SUPPRESSIONS: "List from threat model FP rules (for logging only in hooks)"
  NO_DB_ENDPOINTS: "List of endpoint patterns with no DB (if applicable, not always)"

hooks_must_not:
  - mutate alert objects (riskcode, riskdesc, otherinfo)
  - suppress or remove alerts from the list
  - query external services or mock-upstream admin APIs
  - hardcode assumptions about API archetype
  - make network calls

separation_of_concerns:
  hooks.py: "Observation, logging, seed injection only"
  triage-report.py: "All transformation, suppression, downgrade, reclassification"
  verify-canary.sh: "Read mock-upstream request log, report canary hits"
```

### 7.4 Mock Upstream Contract

```yaml
mock_upstream_requirements:
  - explicit_routes: "One route per detected upstream dependency"
  - request_logging: "Log every incoming request (method, path, headers, body) to stdout and /data/requests.log"
  - default_response: "Return 200 with empty JSON for configured routes"
  - unknown_routes: "Return 404 and log as unexpected (potential SSRF indicator)"
  - no_magic_redirect: "Placing mock on the network does NOT auto-intercept external URLs"
  - canary_routes: "Specific routes that should NEVER be hit unless SSRF succeeds"
```

### 7.5 Scan Policy Generation

Based on archetype and threat model:
- Enable all passive rules always
- Active rules: enable based on priority vectors from archetype
- Strength/threshold per rule: tuned by aggressiveness level (Light/Standard/Thorough)
- Technology context: set in ZAP context file to focus relevant rules

## 8. Phase 6 — Post-Scan Triage

### 8.1 Process

```
Input: report.raw.json (NEVER modified)
Input: mock-upstream request log (for canary verification)
Input: threat-model.yaml (for context)
Input: triage-rules.yaml (for suppression/downgrade criteria)

Output: report.triaged.json (separate file)
Output: triage-report.md (human-readable)

Steps:
1. Parse raw report
2. For each alert:
   ├── Cross-reference with threat model
   ├── Apply triage rules
   ├── Assess confidence: CONFIRMED | LIKELY | UNLIKELY
   ├── Reclassify if miscategorized (e.g., SQLi → validation bug)
   └── Assign priority: exploitability × impact × confidence
3. Read canary log:
   ├── If canary route hit + attacker input caused it → CONFIRMED SSRF
   ├── If canary route hit but internal-only trigger → NOT SSRF
   └── If no canary hit → SSRF not demonstrated (coverage gap)
4. Generate outputs
```

### 8.2 Confidence Assessment

```yaml
CONFIRMED:
  criteria: "Evidence contains actual vulnerability proof"
  examples:
    - SQL error message in response body
    - Reflected payload executing in response
    - Canary hit with attacker-controlled input as cause
    - Sensitive data leaked in response
    - Different response for valid vs invalid auth tokens

LIKELY:
  criteria: "Behavior anomaly consistent with vulnerability"
  examples:
    - Unexpected 500 with error details (but no SQL keywords)
    - Timing difference suggesting injection
    - Information disclosure in error messages
    - Missing security headers on sensitive endpoints

UNLIKELY:
  criteria: "Generic scanner heuristic with no confirming evidence"
  examples:
    - Single quote → 500 without SQL error in body
    - Low confidence from scanner with no behavioral proof
    - Known benign patterns (swagger serving HTML)
```

### 8.3 Triage Rules

```yaml
strict_suppression_criteria:
  all_must_be_true:
    - vulnerability_architecturally_impossible: true
    - no_vuln_indicators_in_evidence: true
    - benign_explanation_documented: true
  if_any_false: KEEP or DOWNGRADE, never SUPPRESS

downgrade_rules:
  - condition: "500 on bad input without SQL/injection indicators in body"
    from: "SQLi/XSS/Injection (High)"
    to: "Missing input validation (Info)"
    note: "Real bug, wrong category. Should return 400."

  - condition: "Low scanner confidence + no behavioral proof"
    action: "Downgrade risk by one level, flag for manual review"

reclassification_rules:
  - original: "SQL Injection"
    actual: "Missing input validation"
    when: "No DB in architecture AND no SQL indicators in evidence"

  - original: "XSS"
    actual: "Improper error handling"
    when: "JSON API reflecting input in error message (not rendered in browser)"

escalation_rules:
  - condition: "SQL indicators found on endpoint declared as no-DB"
    action: "ESCALATE — investigate hidden DB dependency"

  - condition: "Canary hit with attacker-controlled input"
    action: "CONFIRMED SSRF — Critical"
```

### 8.4 Triage Report Format

```markdown
## DAST Scan Triage Report
### Target: {API_NAME} | Date: {DATE} | Duration: {DURATION}
### Total: {TOTAL} ({CONFIRMED} confirmed, {LIKELY} likely, {SUPPRESSED} suppressed)

---

### 🔴 Confirmed (Immediate Action)
| # | Finding | Endpoint | Evidence Summary | Remediation |

### 🟡 Likely (Investigate)
| # | Finding | Endpoint | Assessment | Suggested Action |

### 🔵 Reclassified (Real bugs, different category)
| Original | Reclassified As | Endpoint | Action |

### 🟢 Suppressed (Verified False Positives)
| Rule | Endpoint | Suppression Reason |

### ⚪ Coverage Gaps (Not Testable by ZAP)
| Vector | Why ZAP Can't Test | Manual Test Recommendation |

### 📊 Statistics
- URLs tested | Response distribution | Rules executed | Requests sent
```

### 8.5 CI Failure Criteria

```yaml
fail_pipeline_if:
  - any_confirmed_high: true
  - any_confirmed_medium: true
  - confirmed_low_count_exceeds: 5
never_fail_on:
  - UNLIKELY findings
  - SUPPRESSED findings
  - reclassified findings (unless reclassified TO a security issue)
warn_only:
  - LIKELY findings (log warning, don't block)
  - coverage gaps (informational)
```

## 9. Persist Evidence and Scan State

Every completed or blocked execution MUST persist evidence. Raw reports are immutable;
state is a summary and never replaces the raw report.

```yaml
state_files:
  scan-state.yaml:
    required:
      - schema_version
      - scan_id
      - phase # preflight | baseline | enriched | triage | completed | blocked
      - safety_verdict
      - passes_completed
      - freshness # current | stale | invalid
      - report_paths
  inventory.json:
    purpose: "Normalized operations, parameters, and documented authentication from the local OpenAPI contract."
  fingerprints.json:
    required: [openapi, scan_policy, safety_config, relevant_source]
  coverage.md:
    purpose: "Human-readable operation matrix with passes, exclusions, last scan, and coverage gaps."

per_scan_artifacts:
  path: reports/<scan-id>/
  required_for_every_attempt:
    - preflight.md
  required_when_completed:
    - report.baseline.raw.json
    - report.enriched.raw.json # when Pass 2 ran
    - report.triaged.json
    - triage-report.md
    - canary-evidence.md # when SSRF testing was configured
```

### 9.1 Incremental Rescan Policy

```yaml
always:
  - "Run runtime preflight before every execution, including an incremental scan."
  - "Mark state stale when any required fingerprint differs; never represent stale evidence as current."
  - "Write a new scan-id and immutable report directory; do not overwrite prior evidence."

invalidation:
  openapi_changed:
    action: "Rescan changed operations and affected shared authentication or error-handling paths."
  scan_policy_changed:
    action: "Rescan operations affected by the changed rules."
  relevant_source_changed:
    action: "Run the enriched pass for affected operations; broaden to all operations when middleware, authentication, validation, or outbound-client code changed."
  safety_config_changed:
    action: "Invalidate all prior reuse. Run a new preflight and both passes."
  no_required_fingerprint_changed:
    action: "Reuse current evidence; do not rescan."

fallback:
  condition: "The agent cannot determine the affected operation set confidently."
  action: "Mark state stale and run both passes."
```

## 10. Integration Modes

```
MODE 1: FIRST RUN (Interactive)
  Developer provides spec + optional context → agent interviews →
  generates threat model → user confirms → generates config → commits to repo

MODE 2: CI/CD (Automated)
  Pipeline runs preflight.sh → reads scan state and fingerprints → runs the required
  full or incremental passes → runs verify-canary.sh → runs triage-report.py →
  persists evidence and coverage.md → fails if confirmed findings exceed threshold

MODE 3: SPEC CHANGE (Re-assessment)
  Triggered by OpenAPI spec diff → agent analyzes new/changed endpoints →
  updates threat model + config → may ask questions for new surface only
```

## 11. System Prompt

```
You are a DAST Security Scanning Agent specialized in OWASP ZAP configuration and triage.

ROLE: Help developers set up targeted, context-aware ZAP scans that minimize false
positives while maximizing real vulnerability detection.

PRINCIPLES:

1. SAFETY FIRST: Never generate config that could damage production systems. BLOCK if
   environment is unsafe. Network isolation is non-negotiable.

2. BLACK-BOX FIRST: Never assume code is safe. Context informs severity assessment,
   not scan scope reduction.

3. CONTEXT REDUCES NOISE: Use context ONLY to suppress proven FPs (architecturally
   impossible + no indicators), add missed vectors, and provide valid test data.

4. CLASSIFY BEFORE CONFIGURING: Identify which archetype(s) the API matches. This
   determines priority vectors and scan tuning. Never default to a single archetype.

5. HONEST ABOUT LIMITS: Declare what ZAP cannot test. Recommend manual testing for
   business logic, race conditions, auth flows.

6. SEPARATION OF CONCERNS: Hooks observe only. Triage transforms. Raw report is
   never modified. CI decisions come from triage output only.

SAFETY RULES (non-negotiable):
- NEVER target production or external URLs for active scans; no override exists
- ALWAYS use internal Docker network, no host networking, no published ports
- ALWAYS deny external egress; every outbound dependency explicitly mocked or disabled
- ALWAYS override DB connections to ephemeral
- ALWAYS disable or mock payment/email/webhook services
- NEVER request secrets in chat; reference env vars or secret files
- ALWAYS run the generated runtime preflight before ZAP; if uncertain about safety → BLOCK

FALSE POSITIVE RULES:
- SUPPRESS only if: architecturally impossible AND no vuln indicators AND documented reason
- DOWNGRADE if: low confidence AND no confirming evidence
- RECLASSIFY if: real bug but wrong category (e.g., SQLi → validation bug)
- ESCALATE if: unexpected indicators appear (SQL errors on no-DB endpoint)

WORKFLOW: Follow phases 1-8 as specified. Ask max 5 interview questions. Show threat
model for confirmation before generating config. Run Pass 1 without source context, then
run only safety-approved Pass 2 additions. Persist immutable evidence, scan state, and
coverage after every attempt. All output files must be production-ready with comments
explaining decisions. Always include README.md with run instructions.
```
