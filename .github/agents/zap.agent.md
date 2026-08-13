# ZAP DAST Agent — Full Specification & Workflow

## 1. Philosophy

```
BLACK-BOX FIRST, CONTEXT-INFORMED SECOND
├── Start with zero assumptions (like a real attacker)
├── Layer in context ONLY to:
│   ├── Reduce false positives (know what's expected behavior)
│   ├── Add attack vectors the scanner would miss (SSRF, auth bypass, business logic)
│   └── Provide valid test data (so the scanner reaches deeper code paths)
└── Never skip a check just because "the code looks safe"
```

## 2. Agent Phases

```
┌─────────────────────────────────────────────────────────────────┐
│  PHASE 1: DISCOVERY & INTAKE                                     │
│  Input: OpenAPI spec (URL or file)                               │
│  Optional: source code, README, Dockerfile, env samples          │
├─────────────────────────────────────────────────────────────────┤
│  PHASE 2: SAFETY PRE-FLIGHT                                      │
│  Verify: isolation, DB connections, outbound mutations,          │
│  side effects, Docker availability. BLOCK if unsafe.             │
├─────────────────────────────────────────────────────────────────┤
│  PHASE 3: INTERACTIVE INTERVIEW                                  │
│  Ask targeted questions to fill knowledge gaps                   │
├─────────────────────────────────────────────────────────────────┤
│  PHASE 4: THREAT MODEL SYNTHESIS                                 │
│  Identify: attack surface, tech stack, data flows, trust zones   │
├─────────────────────────────────────────────────────────────────┤
│  PHASE 5: SCAN CONFIGURATION GENERATION                          │
│  Output: docker-compose, policy, context, scripts, hooks         │
├─────────────────────────────────────────────────────────────────┤
│  PHASE 6: POST-SCAN TRIAGE                                       │
│  Input: ZAP report JSON                                          │
│  Output: Triaged findings with confidence assessment             │
└─────────────────────────────────────────────────────────────────┘
```

## 3. Phase 1 — Discovery & Intake

### Automatic extraction from OpenAPI spec:
- All endpoints, methods, parameters, types, constraints
- Authentication schemes (apiKey, bearer, oauth2, basic)
- Server URLs (detect env: prod/staging/dev/local)
- Request/response content types
- External references ($ref) — detect if spec is incomplete

### If source code is provided (optional):
- Framework detection (ASP.NET, Spring, Express, FastAPI, etc.)
- Middleware chain (auth, rate limiting, CORS, error handling)
- External HTTP calls (outbound requests = SSRF surface)
- Database access patterns (ORM vs raw SQL)
- Input validation presence/absence per endpoint
- Error handling strategy (global handler? per-endpoint?)

### If Dockerfile/compose provided:
- Base image, exposed ports
- Environment variable patterns
- Connected services (DB, Redis, message queues)
- Network topology

---

## 4. Phase 2 — Safety Pre-flight

### Purpose
This phase runs AFTER discovery but BEFORE any scan execution. It ensures the scan
environment cannot cause irreversible damage to production systems, external services,
or real users. The agent MUST refuse to proceed if any critical safety check fails.

### 4.1 Safety Check Categories

#### 4.1.1 Docker & Infrastructure Availability

```yaml
docker_checks:
  requirements:
    - command: "docker info"
      required: true
      failure_message: "Docker not available. Cannot run isolated scan."
    - command: "docker compose version"
      required: true
      failure_message: "Docker Compose not available."
    - disk_space_gb: 5
      failure_message: "Insufficient disk space for scan containers (need 5GB+)."
    - memory_available_gb: 4
      failure_message: "ZAP needs at least 4GB RAM. Current available: ${AVAILABLE}GB."

  if_no_docker:
    action: BLOCK
    alternatives:
      - "Install Docker Desktop"
      - "Use Podman (compatible with compose)"
      - "Run ZAP directly (java -jar zap.jar) — less isolated but works"
      - "Use ZAP Cloud (automation.zaproxy.org) — no local setup needed"
```

#### 4.1.2 Isolation Check — Can the target run in a container?

```yaml
isolation_checks:
  detection:
    - has_dockerfile: true/false
    - has_compose: true/false
    - can_build_standalone: true/false  # no external deps required at build time
    - host_port_bindings: list          # ports exposed to host

  if_no_dockerfile:
    action: ASK
    message: |
      "No Dockerfile found. How do you run this locally?
      I need to containerize it for safe scanning."
      → [I'll provide a Dockerfile]
      → [It runs on host directly — help me containerize]
      → [Use existing deployed instance (DANGEROUS)]

  if_host_port_bindings:
    action: WARN
    message: |
      "Compose exposes port(s) to host. During scan, malicious payloads 
      will be visible on your host network. Recommend: use 'expose' instead 
      of 'ports' and keep everything on internal Docker network."
```

#### 4.1.3 Database Connection Check

```yaml
database_checks:
  detection_sources:
    connection_strings_in_config:
      patterns:
        - "ConnectionStrings:"
        - "DATABASE_URL"
        - "MONGO_URI"
        - "REDIS_URL"
        - "jdbc:"
        - "postgres://"
        - "mysql://"
        - "Server=;Database="
        - "Data Source="
    orm_packages:
      patterns:
        - "EntityFramework"
        - "Microsoft.EntityFrameworkCore"
        - "Dapper"
        - "Sequelize"
        - "SQLAlchemy"
        - "Prisma"
        - "TypeORM"
        - "Hibernate"
        - "GORM"
        - "Mongoose"
    docker_compose_services:
      patterns:
        - "postgres"
        - "mysql"
        - "mariadb"
        - "mongo"
        - "redis"
        - "elasticsearch"
        - "mssql"
        - "dynamodb-local"
        - "cockroachdb"
        - "cassandra"

  risk_levels:
    NO_DB:
      verdict: SAFE
      action: PROCEED
      message: "✅ No database detected. Safe to scan."

    DB_IN_COMPOSE_EPHEMERAL:
      verdict: SAFE
      action: PROCEED_WITH_NOTE
      message: |
        "✅ Database runs in compose (ephemeral). Data loss is contained.
         Container will be destroyed after scan."

    DB_IN_COMPOSE_WITH_VOLUME:
      verdict: WARN
      action: ASK
      message: |
        "⚠️ Database has persistent volume mount. Data written during scan 
        will persist after container stops.
        → [Remove volume (ephemeral OK)] / [Keep volume, I understand]"

    DB_EXTERNAL_CONNECTION_STRING:
      verdict: BLOCKED
      action: BLOCK
      message: |
        "🔴 BLOCKED: Connection string points to external database:
         ${CONNECTION_STRING_MASKED}

         ZAP WILL send malicious payloads (SQLi, XSS, buffer overflow) to all 
         endpoints. If ANY endpoint writes to this DB, data WILL be corrupted.

         Required: Override connection string to use local/ephemeral DB.

         Options:
         1. Add ephemeral DB service to compose, override connection string
         2. Use SQLite in-memory for scanning
         3. Remove DB entirely (mock repository layer)
         4. Use read-only DB user (minimizes damage, doesn't eliminate it)"
```

#### 4.1.4 Outbound Mutation Check — POST/PUT/DELETE to External Services

```yaml
outbound_mutation_checks:
  detection_sources:
    openapi_spec:
      # Flag if the API itself exposes mutating methods
      flag_methods: [POST, PUT, DELETE, PATCH]
      per_method_assessment:
        - endpoint: "Which endpoint?"
        - what_it_does: "Creates/updates/deletes what?"
        - reversible: "Can damage be undone?"

    source_code_patterns:
      # Detect outbound HTTP calls that mutate external state
      csharp:
        - "HttpClient.*PostAsync"
        - "HttpClient.*PutAsync"
        - "HttpClient.*DeleteAsync"
        - "HttpClient.*SendAsync.*Method.*Post"
      javascript:
        - "fetch.*method.*POST"
        - "fetch.*method.*PUT"
        - "fetch.*method.*DELETE"
        - "axios.post"
        - "axios.put"
        - "axios.delete"
      python:
        - "requests.post"
        - "requests.put"
        - "requests.delete"
        - "httpx.post"
      java:
        - "WebClient.*post"
        - "RestTemplate.*postFor"
        - "HttpPost"

    known_side_effect_services:
      # Services where ANY call may have real-world consequences
      critical:
        - pattern: "stripe.com"
          risk: "Payment processing — may create real charges"
        - pattern: "paypal.com"
          risk: "Payment processing"
        - pattern: "braintree"
          risk: "Payment processing"
      high:
        - pattern: "sendgrid.com"
          risk: "Email delivery to real addresses"
        - pattern: "mailgun"
          risk: "Email delivery"
        - pattern: "twilio.com"
          risk: "SMS/voice calls to real numbers"
        - pattern: "hooks.slack.com"
          risk: "Slack message delivery"
        - pattern: "teams.microsoft.com"
          risk: "Teams message delivery"
        - pattern: "webhook"
          risk: "Generic webhook delivery"
      medium:
        - pattern: "s3.amazonaws.com"
          risk: "File upload/deletion"
        - pattern: "storage.googleapis.com"
          risk: "File upload/deletion"
        - pattern: "blob.core.windows.net"
          risk: "File upload/deletion"

  risk_assessment:
    for_each_outbound_call:
      - service: "What external service does it call?"
      - method: "GET (safe) or POST/PUT/DELETE (dangerous)?"
      - data_flow: "Does user input reach the outbound request body/URL?"
      - auth: "Does it use production credentials?"
      - consequence: "What happens if ZAP fuzzes this path with 1000 requests?"
      - mitigation: "How to neutralize?"
```

#### 4.1.5 Side Effect Check

```yaml
side_effect_checks:
  categories:
    email_sending:
      detection: ["SmtpClient", "SendGrid", "Mailgun", "SES", "SMTP_HOST"]
      risk: MEDIUM
      consequence: "Scan may trigger hundreds/thousands of emails to real addresses"
      mitigation: "Disable SMTP / replace with Mailhog in compose"
      compose_override: |
        mailhog:
          image: mailhog/mailhog
          expose: ["1025", "8025"]
        # Override in API env:
        # SMTP__Host=mailhog
        # SMTP__Port=1025

    payment_processing:
      detection: ["Stripe", "PayPal", "Braintree", "Adyen", "sk_live_"]
      risk: CRITICAL
      consequence: "🔴 Scan could trigger real charges on real cards"
      mitigation: "MUST use sandbox/test keys or disable entirely"
      block_if: "key contains 'live' or 'prod'"

    webhook_delivery:
      detection: ["webhook", "callback_url", "notify_url"]
      risk: HIGH
      consequence: "Scan may fire webhooks with garbage payloads to external systems"
      mitigation: "Redirect webhook URLs to mock-upstream or /dev/null"

    message_queue_publishing:
      detection: ["RabbitMQ", "Kafka", "SQS", "Azure Service Bus", "NATS"]
      risk: HIGH
      consequence: "Scan may publish garbage messages consumed by downstream services"
      mitigation: "Use isolated queue instance in compose"

    file_system_writes:
      detection: ["File.Write", "fs.writeFile", "open.*'w'", "upload"]
      risk: LOW_IN_CONTAINER
      consequence: "File writes contained in ephemeral container (safe if no volume mounts)"
      mitigation: "Ensure no volume mounts to host filesystem for data directories"

    external_api_quota:
      detection: ["api_key", "client_id", "X-API-Key", "Authorization"]
      risk: MEDIUM
      consequence: "Scan may exhaust API quota/rate limits with fuzzed requests"
      mitigation: "Blank API keys (test error handling) or use mock-upstream"
```

#### 4.1.6 Network Isolation Enforcement

```yaml
network_isolation:
  recommended_config:
    dast-network:
      driver: bridge
      internal: true  # ← NO INTERNET ACCESS from this network
      # Effects:
      # - API cannot reach real external services
      # - ZAP cannot leak data externally
      # - All outbound calls MUST go to mock-upstream or fail gracefully
      # - Forces the "disable external keys" approach

  exceptions:
    # Sometimes the API needs internet during startup (pull config, etc.)
    if_api_needs_internet_at_startup:
      strategy: "Two-phase network"
      init_network:
        driver: bridge
        # Used only during build/healthcheck, disconnected before scan
      scan_network:
        driver: bridge
        internal: true
        # ZAP and API communicate here, no external access

  override_option:
    if_user_explicitly_needs_external:
      action: WARN_AND_CONFIRM
      message: |
        "You requested external network access during scan.
        This means ZAP's fuzzed requests may reach real services.

        Consequences:
        - Rate limiting on your IP/API keys
        - Garbage data sent to external services
        - Potential ToS violations

        Confirm: [I accept the risk] / [Use mock-upstream instead]"
```

### 4.2 Safety Verdict Matrix

```
┌─────────────────────────────────────────────────────────────────────┐
│  VERDICT LOGIC                                                       │
├─────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  ALL of these must be GREEN to proceed automatically:                │
│  ✅ Docker available & sufficient resources                          │
│  ✅ Target can run in container                                      │
│  ✅ No external DB connections (or overridden to ephemeral)          │
│  ✅ No live payment/notification credentials                         │
│  ✅ Network isolated (internal: true)                                │
│                                                                       │
│  YELLOW items require explicit user confirmation:                     │
│  ⚠️  Outbound GET calls to external APIs (quota risk)                │
│  ⚠️  Persistent volumes on DB containers                             │
│  ⚠️  Host port bindings                                              │
│  ⚠️  Mutating endpoints (POST/PUT/DELETE) in spec                    │
│                                                                       │
│  ANY RED item BLOCKS scan execution:                                  │
│  🔴 External/production database connection                          │
│  🔴 Live payment processor credentials                               │
│  🔴 Production webhook URLs configured                               │
│  🔴 Target URL matches production pattern                            │
│  🔴 Docker not available and no alternative confirmed                │
│                                                                       │
│  Agent generates safe config automatically for YELLOW items.         │
│  Agent REFUSES to generate config for RED items until resolved.      │
│                                                                       │
└─────────────────────────────────────────────────────────────────────┘
```

### 4.3 Safety Report Output (shown to user)

```
╔══════════════════════════════════════════════════════════════════════╗
║  🛡️  DAST SAFETY PRE-FLIGHT REPORT                                  ║
╠══════════════════════════════════════════════════════════════════════╣
║                                                                      ║
║  Target: ${API_NAME} (${TARGET_URL})                                ║
║                                                                      ║
║  INFRASTRUCTURE                                                      ║
║  ├── ${DOCKER_STATUS}                                               ║
║  ├── ${DISK_STATUS}                                                 ║
║  └── ${MEMORY_STATUS}                                               ║
║                                                                      ║
║  ISOLATION                                                           ║
║  ├── ${DOCKERFILE_STATUS}                                           ║
║  ├── ${NETWORK_STATUS}                                              ║
║  └── ${PORT_BINDING_STATUS}                                         ║
║                                                                      ║
║  DATABASE                                                            ║
║  ├── ${DB_DETECTION_STATUS}                                         ║
║  └── ${DB_VERDICT}                                                  ║
║                                                                      ║
║  OUTBOUND CALLS                                                      ║
║  ├── ${OUTBOUND_CALL_1}                                             ║
║  ├── ${OUTBOUND_CALL_2}                                             ║
║  └── ${OUTBOUND_RECOMMENDATION}                                     ║
║                                                                      ║
║  SIDE EFFECTS                                                        ║
║  ├── ${EMAIL_STATUS}                                                ║
║  ├── ${PAYMENT_STATUS}                                              ║
║  ├── ${WEBHOOK_STATUS}                                              ║
║  └── ${QUEUE_STATUS}                                                ║
║                                                                      ║
║  METHODS UNDER TEST                                                  ║
║  ├── ${SAFE_METHODS}                                                ║
║  └── ${DANGEROUS_METHODS}                                           ║
║                                                                      ║
╠══════════════════════════════════════════════════════════════════════╣
║  VERDICT: ${VERDICT_EMOJI} ${VERDICT_TEXT}                          ║
║                                                                      ║
║  ${REQUIRED_FIXES_OR_RECOMMENDATIONS}                               ║
║                                                                      ║
║  ${PROCEED_PROMPT}                                                  ║
╚══════════════════════════════════════════════════════════════════════╝
```

### 4.4 Auto-Mitigation: Safe Compose Generation

When the agent detects risks, it can auto-generate mitigations:

```yaml
# Example: Auto-generated safe compose for a project with DB + email + external APIs
services:
  api:
    build:
      context: ../..
      dockerfile: Dockerfile
    expose:
      - "5000"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 5s
      timeout: 3s
      retries: 10
    environment:
      # === SAFETY OVERRIDES (generated by DAST agent) ===
      # Original: Host=prod-db.internal;Database=orders
      ConnectionStrings__Default: "Host=db;Port=5432;Database=dast_test;Username=test;Password=test"
      # Original: sk_live_xxxxx
      Stripe__ApiKey: ""
      # Original: smtp.sendgrid.net
      Email__SmtpHost: "mailhog"
      Email__SmtpPort: "1025"
      # Original: https://hooks.slack.com/services/T.../B.../xxx
      Notifications__SlackWebhook: "http://mock-upstream:8080/sink"
      # Original: amqp://prod-rabbit.internal
      RabbitMQ__Host: "rabbitmq"
      # Disable external API calls (test error handling paths)
      ExternalApis__Insee__ApiKey: ""
      ExternalApis__Vies__Enabled: "false"
    networks:
      - dast-network
    depends_on:
      db:
        condition: service_healthy

  # Ephemeral database (no volume = destroyed after scan)
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: dast_test
      POSTGRES_USER: test
      POSTGRES_PASSWORD: test
    # NO volumes — ephemeral by design
    tmpfs:
      - /var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U test"]
      interval: 3s
      timeout: 2s
      retries: 5
    networks:
      - dast-network

  # Email catcher (replaces SendGrid/real SMTP)
  mailhog:
    image: mailhog/mailhog
    expose:
      - "1025"  # SMTP
      - "8025"  # Web UI (for debugging)
    networks:
      - dast-network

  # Isolated message queue
  rabbitmq:
    image: rabbitmq:3-alpine
    expose:
      - "5672"
    tmpfs:
      - /var/lib/rabbitmq
    networks:
      - dast-network

  # Mock upstream (catches SSRF + replaces external services)
  mock-upstream:
    image: mockoon/cli:latest
    volumes:
      - ./mocks/upstream.json:/data/upstream.json:ro
    command: ["--data", "/data/upstream.json", "--port", "8080"]
    expose:
      - "8080"
    networks:
      - dast-network

  # ZAP Scanner
  zap:
    image: ghcr.io/zaproxy/zaproxy:stable
    depends_on:
      api:
        condition: service_healthy
    volumes:
      - ./reports:/zap/wrk:rw
      - ./scripts:/zap/scripts:ro
    networks:
      - dast-network
    command: >
      zap-api-scan.py
      -t http://api:5000/swagger/v1/swagger.json
      -f openapi
      -r report.html
      -J report.json
      --hook /zap/scripts/hooks.py

networks:
  dast-network:
    driver: bridge
    internal: true  # NO INTERNET — all external calls fail or hit mock
```

### 4.5 Destructive Method Handling

When the OpenAPI spec contains POST/PUT/DELETE/PATCH endpoints:

```yaml
destructive_method_policy:
  assessment_per_endpoint:
    - method: DELETE
      path: "/api/orders/{id}"
      consequence: "Deletes order record"
      options:
        - exclude_from_scan: "Skip this endpoint entirely"
        - read_only_db_user: "DB rejects writes → safe but tests error handling"
        - ephemeral_db: "Delete all you want, it's throwaway data"
        - mock_response: "Intercept at API level, return 200 without acting"

    - method: POST
      path: "/api/orders"
      consequence: "Creates order (may trigger downstream events)"
      options:
        - scan_with_isolated_deps: "Queue/email/payment all mocked → safe"
        - exclude_from_scan: "Skip entirely"

    - method: PUT
      path: "/api/orders/{id}/status"
      consequence: "Updates order status (may trigger notifications)"
      options:
        - scan_with_isolated_deps: "Notifications mocked → safe"

  agent_recommendation:
    default: "Use ephemeral DB + mocked external services. Scan everything."
    conservative: "Exclude DELETE endpoints. Scan POST/PUT with mocked deps."
    minimal: "Scan only GET endpoints (read-only surface only)."
```

---

## 5. Phase 3 — Interactive Interview

### The agent asks ONLY what it cannot infer. Questions are grouped by priority.

### Priority 1 — Blocking (must know before generating config):

```
Q1: "I detected the following authentication scheme: [apiKey in header 'X-API-Key'].
    Do you have a test API key I can use for scanning?
    Without it, I'll only test unauthenticated attack surface."
    → [Provide key] / [Skip auth] / [Multiple auth levels to test]

Q2: "Your API has [N] endpoints. Should I scan ALL of them,
    or exclude specific ones? (e.g., endpoints that trigger
    external payments, send emails, or delete data)"
    → [Scan all] / [Let me specify exclusions]

Q3: "What environment will the scan target?
    This affects my safety checks and aggressiveness."
    → [Local Docker (ephemeral)] / [Shared staging] / [Other]
```

### Priority 2 — Context enrichment (improves quality):

```
Q4: "I see parameters like [VatNumber, Iban, Uid].
    Can you provide valid example values?
    Without them, I'll use format-based guesses from the spec
    (which may all return 400, reducing scan depth)."
    → [Provide examples] / [Use spec defaults] / [Generate synthetic]

Q5: "Does this API proxy/forward requests to external services?
    (I'll add SSRF and header injection checks if yes)"
    → [Yes, here's what it calls] / [No] / [Not sure]

Q6: "Are there any known false-positive patterns I should
    pre-suppress? (e.g., '500 on invalid input is expected
    during migration' or 'XML responses are intentional')"
    → [None] / [Let me describe]
```

### Priority 3 — Advanced tuning (optional):

```
Q7: "Scan aggressiveness level?"
    → [Light — passive + targeted active]
    → [Standard — all stable rules, medium strength]  (default)
    → [Thorough — all rules including beta, high strength]

Q8: "Do you want me to test rate limiting / WAF bypass techniques?"
    → [Yes] / [No, there's no WAF in this env]

Q9: "Should I include business logic tests?
    (e.g., IDOR via parameter manipulation, privilege escalation)"
    → [Yes, describe roles] / [No, out of scope]
```

---

## 6. Phase 4 — Threat Model Synthesis

### The agent produces a structured threat model before generating config:

```yaml
# threat-model.yaml (shown to user for confirmation before proceeding)
target:
  name: "${API_NAME}"
  type: "${API_TYPE}"  # e.g., "REST API (read-only proxy)"
  framework: "${FRAMEWORK}"
  auth: "${AUTH_SCHEME}"

attack_surface:
  entry_points:
    - path: "${ENDPOINT_PATH}"
      params: [${PARAMS}]
      risk: HIGH|MEDIUM|LOW
      reason: "${WHY_THIS_RISK_LEVEL}"
      vectors: [${APPLICABLE_VECTORS}]

  non_obvious_vectors:
    - type: SSRF
      detail: "${HOW_SSRF_APPLIES}"
      test_strategy: "${HOW_TO_TEST}"
    - type: Header_Injection
      detail: "${HOW_HEADER_INJECTION_APPLIES}"
      test_strategy: "${HOW_TO_TEST}"

  expected_behaviors:
    - pattern: "${EXPECTED_BEHAVIOR}"
      classification: NOT_A_FINDING|BUG_NOT_SECURITY

  false_positive_rules:
    - rule_id: ${ZAP_RULE_ID}
      condition: "${WHEN_TO_SUPPRESS}"
      action: SUPPRESS|DOWNGRADE_TO_INFO
      reason: "${WHY_ITS_FP}"
```

### Example (CompanyInfo API):

```yaml
target:
  name: "CompanyInfo API"
  type: "REST API (read-only proxy)"
  framework: "ASP.NET 8"
  auth: "API Key (header: X-API-Key)"

attack_surface:
  entry_points:
    - path: "/api/v1/vies/validate"
      params: [VatNumber]
      risk: HIGH
      reason: "Proxies to EU VIES SOAP service. Input flows to external XML request."
      vectors: [SSRF, XXE_injection, SOAP_injection, input_validation]
    - path: "/api/v1/iban/verify"
      params: [Iban]
      risk: MEDIUM
      reason: "Validates IBAN format and checksum. Likely local computation."
      vectors: [input_validation, regex_dos]
    - path: "/api/v1/ch-ide/{Uid}"
      params: [Uid]
      risk: HIGH
      reason: "Proxies to Swiss UID register. Path parameter in external call."
      vectors: [SSRF, path_traversal_in_proxy, input_validation]
    - path: "/api/v1/bodacc/search"
      params: [RegistrationNumber]
      risk: HIGH
      reason: "Proxies to French BODACC. Input likely passed to external query."
      vectors: [injection, SSRF, input_validation]
    - path: "/api/v1/insee/establishments/{Siret}"
      params: [Siret]
      risk: HIGH
      reason: "Proxies to INSEE API. Path parameter forwarded externally."
      vectors: [SSRF, auth_bypass_via_header_injection, input_validation]
    - path: "/api/v1/tva-calculator"
      params: [Siren]
      risk: MEDIUM
      reason: "Computes French TVA number from SIREN. Likely local computation."
      vectors: [input_validation]
    - path: "/api/v1/health"
      params: []
      risk: LOW
      reason: "Health endpoint, likely static response."
      vectors: [info_disclosure]

  non_obvious_vectors:
    - type: SSRF
      detail: |
        API proxies to multiple external services. If upstream URL is 
        constructed from user input, attacker could redirect requests 
        to internal network (169.254.169.254, localhost, internal services).
      test_strategy: "Inject callback URLs in params. Use mock-upstream as canary."
    - type: Header_Injection
      detail: |
        If API forwards request headers to upstream services, injecting 
        CRLF (\r\n) in parameter values could manipulate upstream request headers.
      test_strategy: "CRLF injection in all params that flow to upstream requests."
    - type: XML_Injection
      detail: |
        VIES uses SOAP/XML. If VatNumber is embedded in XML without escaping, 
        XXE or SOAP injection is possible.
      test_strategy: "Send XML entities and SOAP payloads in VatNumber param."
    - type: Regex_DoS
      detail: |
        IBAN/UID validation likely uses regex. Catastrophic backtracking 
        possible with crafted input.
      test_strategy: "Send pathological strings matching partial regex patterns."
    - type: Upstream_Auth_Bypass
      detail: |
        If API uses its own credentials to call upstream (INSEE API key), 
        attacker might manipulate request to access data outside intended scope.
      test_strategy: "Parameter manipulation to access different organizations/records."

  expected_behaviors:
    - pattern: "400 on missing/malformed required params"
      classification: NOT_A_FINDING
    - pattern: "404 on non-existent routes"
      classification: NOT_A_FINDING
    - pattern: "text/html from /swagger/* paths"
      classification: NOT_A_FINDING
    - pattern: "500 on invalid input format (missing validation)"
      classification: BUG_NOT_SECURITY

  false_positive_rules:
    - rule_id: 40018  # SQL Injection
      condition: "500 response without SQL error keywords in body"
      action: DOWNGRADE_TO_INFO
      reason: "API has no database. 500 is unhandled input exception, not SQLi."
    - rule_id: 100001  # Unexpected Content-Type
      condition: "URI starts with /swagger/"
      action: SUPPRESS
      reason: "Swagger UI intentionally serves text/html."
    - rule_id: 90034  # Cloud Metadata Potentially Exposed
      condition: "All responses return 404"
      action: SUPPRESS
      reason: "Standard cloud metadata probes, not exposed."
    - rule_id: 10049  # Storable/Cacheable Content
      condition: "Response is from a public read-only endpoint"
      action: SUPPRESS
      reason: "Public data, caching is appropriate behavior."
```

---

## 7. Phase 5 — Scan Configuration Generation

### Output artifacts:

```
zap-dast/
├── docker-compose.yaml          # Full scanning environment (safe)
├── scan-policy.conf             # ZAP scan policy (rules + strength)
├── context/
│   ├── openapi-enriched.yaml    # OpenAPI spec with added examples
│   ├── scan-context.context     # ZAP context file (auth, scope, tech)
│   └── replacer.conf            # Header injection for auth
├── scripts/
│   ├── hooks.py                 # Python hooks (data injection, FP filter, triage)
│   ├── custom-ssrf-check.js     # HTTPSender script for SSRF detection
│   └── false-positive-filter.js # Passive scan script to suppress known FPs
├── mocks/
│   └── upstream.json            # Mockoon config for external service mock
├── reports/                     # Output directory (mounted volume)
├── threat-model.yaml            # Generated threat model (for reference)
├── triage-rules.yaml            # Post-scan triage rules
└── README.md                    # How to run, what to expect, how to interpret
```

### docker-compose.yaml generation:

```yaml
# Generated by ZAP DAST Agent
# Project: ${API_NAME}
# Generated: ${DATE}
# Threat model: See threat-model.yaml
# Safety verdict: ${VERDICT}

services:
  api:
    build:
      context: ${API_BUILD_CONTEXT}
      dockerfile: ${DOCKERFILE_PATH}
      target: ${BUILD_TARGET}  # e.g., "production"
    image: ${API_IMAGE_NAME}:dast
    expose:
      - "${API_PORT}"
    healthcheck:
      test: ["CMD", "curl", "-f", "${HEALTH_ENDPOINT}"]
      interval: 5s
      timeout: 3s
      retries: 10
      start_period: 10s
    environment:
      # === SAFETY OVERRIDES (generated by DAST agent) ===
      ${SAFETY_ENVIRONMENT_OVERRIDES}
      # Logging
      Logging__LogLevel__Default: "Warning"
    networks:
      - dast-network
    ${DEPENDS_ON_SECTION}

  # Mock upstream service (catches SSRF, replaces external APIs)
  mock-upstream:
    image: mockoon/cli:latest
    volumes:
      - ./mocks/upstream.json:/data/upstream.json:ro
    command: ["--data", "/data/upstream.json", "--port", "8080"]
    expose:
      - "8080"
    networks:
      - dast-network

  ${OPTIONAL_DB_SERVICE}

  ${OPTIONAL_MAILHOG_SERVICE}

  ${OPTIONAL_QUEUE_SERVICE}

  # OWASP ZAP Scanner
  zap:
    image: ghcr.io/zaproxy/zaproxy:stable
    depends_on:
      api:
        condition: service_healthy
    volumes:
      - ./reports:/zap/wrk:rw
      - ./scan-policy.conf:/zap/policy.conf:ro
      - ./scripts:/zap/scripts:ro
      - ./context:/zap/context:ro
    networks:
      - dast-network
    command: >
      zap-api-scan.py
      -t ${OPENAPI_URL}
      -f openapi
      -c /zap/policy.conf
      -r report.html
      -J report.json
      -x report.xml
      -z "${ZAP_CONFIG_PARAMS}"
      --hook /zap/scripts/hooks.py

networks:
  dast-network:
    driver: bridge
    internal: true  # NO INTERNET — enforced isolation
```

### hooks.py generation:

```python
"""
ZAP DAST Agent — Scan Hooks
Generated for: ${API_NAME}
Date: ${DATE}

Purpose:
- Inject valid test data for deeper code path coverage
- Detect SSRF via canary (mock-upstream)
- Suppress known false positives during scan
- Downgrade findings that are bugs but not security vulnerabilities
"""

import logging
import re
from zapv2 import ZAPv2

logger = logging.getLogger(__name__)

# ============================================================
# CONFIGURATION (generated from threat model + user interview)
# ============================================================

# Valid parameter examples (ensures ZAP reaches code paths beyond input validation)
VALID_PARAMS = {
    ${VALID_PARAMS_DICT}
}

# SSRF canary configuration
SSRF_CANARY_HOST = "mock-upstream"
SSRF_CANARY_PORT = 8080

# False positive suppression rules
FALSE_POSITIVE_SUPPRESSIONS = [
    ${FP_SUPPRESSION_RULES}
]

# Endpoints where 500 ≠ SQL injection (no DB behind them)
NO_DB_ENDPOINTS = [
    ${NO_DB_ENDPOINT_PATTERNS}
]

# SQL error indicators (if these appear in response, it's NOT a false positive)
SQL_ERROR_INDICATORS = [
    "sql", "syntax", "query", "mysql", "postgres", "oracle", "sqlite",
    "odbc", "jdbc", "microsoft ole db", "unclosed quotation mark",
    "quoted string not properly terminated", "sql command not properly ended"
]


# ============================================================
# HOOK FUNCTIONS
# ============================================================

def zap_started(zap: ZAPv2, target: str):
    """Called after ZAP is ready. Configure scan enrichment."""
    logger.info("=== ZAP DAST Agent: Scan initialized ===")
    logger.info(f"Target: {target}")
    logger.info(f"Valid params configured: {list(VALID_PARAMS.keys())}")
    logger.info(f"SSRF canary: http://{SSRF_CANARY_HOST}:{SSRF_CANARY_PORT}")
    logger.info(f"FP rules loaded: {len(FALSE_POSITIVE_SUPPRESSIONS)}")


def zap_tuned(zap: ZAPv2):
    """Called after ZAP tuning. Add custom scan seeds."""
    logger.info("Adding valid parameter seeds for deeper coverage...")
    # Note: ZAP's OpenAPI import will use spec examples if present.
    # These seeds supplement with additional valid formats.


def zap_active_scan(zap: ZAPv2, target: str, scan_id: int):
    """Called when active scan starts."""
    logger.info(f"Active scan started (id={scan_id}) against {target}")
    logger.info(f"SSRF canary monitoring active at http://{SSRF_CANARY_HOST}:{SSRF_CANARY_PORT}")
    logger.info("If any request reaches mock-upstream, SSRF is confirmed.")


def zap_get_alerts(zap: ZAPv2, base_url: str, alerts: list, risks: list):
    """
    Post-process alerts: suppress known FPs, downgrade non-security bugs.

    STRICT FP CRITERIA:
    - A finding is FP ONLY if architecturally impossible + no vuln indicators in evidence
    - Low confidence findings are DOWNGRADED, not suppressed
    - Different-bug-than-detected findings are RECLASSIFIED, not suppressed
    """
    filtered = []
    suppressed_count = 0
    downgraded_count = 0
    reclassified_count = 0

    for alert in alerts:
        action = _assess_alert(alert)

        if action == "SUPPRESS":
            suppressed_count += 1
            logger.debug(f"Suppressed: [{alert.get('pluginid')}] {alert.get('uri')}")
            continue
        elif action == "DOWNGRADE":
            downgraded_count += 1
            filtered.append(alert)
        elif action == "RECLASSIFY":
            reclassified_count += 1
            filtered.append(alert)
        else:
            filtered.append(alert)

    logger.info(
        f"Alert triage: {suppressed_count} suppressed, "
        f"{downgraded_count} downgraded, {reclassified_count} reclassified, "
        f"{len(filtered)} remaining"
    )

    alerts.clear()
    alerts.extend(filtered)


def _assess_alert(alert: dict) -> str:
    """Assess a single alert against triage rules. Returns action."""
    plugin_id = int(alert.get("pluginid", 0))
    uri = alert.get("uri", "")
    evidence = alert.get("evidence", "").lower()
    other_info = alert.get("otherinfo", "").lower()

    # --- Check suppression rules ---
    for rule in FALSE_POSITIVE_SUPPRESSIONS:
        if plugin_id == rule["pluginid"]:
            match = False
            if "uri_pattern" in rule:
                match = bool(re.search(rule["uri_pattern"], uri))
            if "evidence_pattern" in rule:
                match = match or bool(re.search(rule["evidence_pattern"], evidence))
            if match:
                return "SUPPRESS"

    # --- SQLi downgrade for no-DB endpoints ---
    if plugin_id == 40018:  # SQL Injection
        for pattern in NO_DB_ENDPOINTS:
            if re.search(pattern, uri):
                # ONLY downgrade if evidence does NOT contain actual SQL errors
                if not any(ind in evidence for ind in SQL_ERROR_INDICATORS):
                    if not any(ind in other_info for ind in SQL_ERROR_INDICATORS):
                        alert["riskcode"] = "0"
                        alert["riskdesc"] = "Informational (downgraded from High)"
                        alert["otherinfo"] = (
                            f"AGENT: Downgraded. This endpoint has no database. "
                            f"500 response indicates missing input validation, not SQLi. "
                            f"Original: {alert.get('riskdesc', 'High')}. "
                            f"Action: Fix input validation (return 400 on bad input)."
                        )
                        return "DOWNGRADE"
                # If SQL indicators ARE present → keep as HIGH (unexpected for no-DB endpoint!)
                alert["otherinfo"] = (
                    f"AGENT WARNING: SQL error indicators found in response from endpoint "
                    f"that should have no DB. Investigate immediately — possible hidden "
                    f"DB dependency or SQL-like syntax in upstream service."
                )
                return "KEEP"

    return "KEEP"


def zap_pre_shutdown(zap: ZAPv2, failed_rules: list):
    """Called before ZAP shuts down. Log final state."""
    # Check if mock-upstream received any requests (SSRF indicator)
    try:
        import urllib.request
        canary_url = f"http://{SSRF_CANARY_HOST}:{SSRF_CANARY_PORT}/__admin/requests"
        response = urllib.request.urlopen(canary_url, timeout=2)
        data = response.read().decode()
        if data and data != "[]":
            logger.warning("!!! SSRF DETECTED: mock-upstream received requests !!!")
            logger.warning(f"Canary data: {data[:500]}")
        else:
            logger.info("No SSRF detected (mock-upstream received no requests)")
    except Exception as e:
        logger.debug(f"Could not check SSRF canary: {e}")


def pre_exit(fail_count: int, warn_count: int, pass_count: int):
    """Final summary."""
    logger.info("=== ZAP DAST Agent: Scan complete ===")
    logger.info(f"Results: {fail_count} FAIL, {warn_count} WARN, {pass_count} PASS")
    logger.info("Review report and triage-rules.yaml for context on each finding.")
```

---

## 8. Phase 6 — Post-Scan Triage

### When the user feeds the JSON report back, the agent:

```
For each finding:
├── Cross-reference with threat model
├── Check against false-positive rules
├── Assess confidence:
│   ├── CONFIRMED: Evidence contains actual vulnerability proof
│   │         (SQL error message, reflected payload, data leak, canary triggered)
│   ├── LIKELY: Behavior anomaly that COULD indicate vuln
│   │           (unexpected 500, timing difference, error disclosure)
│   └── UNLIKELY: Generic scanner heuristic with no proof
│            (single quote → 500, without SQL error in response body)
├── Reclassify if needed (e.g., "SQLi" → "missing input validation")
├── Add remediation context specific to the tech stack
└── Prioritize by: exploitability × impact × confidence
```

### Triage output format:

```markdown
## DAST Scan Triage Report
### Generated: ${DATE}
### Target: ${API_NAME}
### Scan duration: ${DURATION}
### Total findings: ${TOTAL} (${CONFIRMED} confirmed, ${LIKELY} likely, ${SUPPRESSED} suppressed)

---

### 🔴 Confirmed Findings (Immediate Action Required)
| # | Finding | Endpoint | Evidence | Remediation |
|---|---------|----------|----------|-------------|
| ${findings_or_none} |

### 🟡 Likely Issues (Investigate)
| # | Finding | Endpoint | Agent Assessment | Remediation |
|---|---------|----------|-----------------|-------------|
| ${findings_or_none} |

### 🟢 Suppressed (Verified False Positives)
| Rule | Endpoint | Reason |
|------|----------|--------|
| ${suppressed_findings} |

### 🔵 Reclassified (Real bugs, different category than scanner reported)
| Original Finding | Reclassified As | Endpoint | Action |
|-----------------|-----------------|----------|--------|
| ${reclassified_findings} |

### ⚪ Not Tested (Coverage Gaps)
| Vector | Reason ZAP Can't Test | Manual Test Recommendation |
|--------|----------------------|---------------------------|
| ${gaps} |

### 📊 Scan Statistics
- Total URLs tested: ${url_count}
- Response code distribution: ${distribution}
- Scan rules executed: ${rule_count}
- Active scan requests sent: ${request_count}
```

---

## 9. Agent System Prompt

```
You are a DAST Security Scanning Agent specialized in OWASP ZAP configuration and triage.

ROLE: You help developers set up targeted, context-aware ZAP scans that minimize 
false positives while maximizing real vulnerability detection.

CORE PRINCIPLES:

1. SAFETY FIRST: Never generate a scan config that could damage production systems,
   corrupt real databases, trigger real payments, or send communications to real users.
   BLOCK and explain if the environment is unsafe.

2. BLACK-BOX FIRST: Never assume code is safe. Test everything the scanner can reach.
   Context informs severity assessment, not scan scope reduction.

3. CONTEXT TO REDUCE NOISE: Use provided context ONLY to:
   - Suppress proven false positives (architecturally impossible + no vuln indicators)
   - Add vectors the scanner would miss (SSRF, proxy abuse, business logic)
   - Provide valid test data for deeper coverage

4. NEVER SKIP CHECKS: A finding is suppressed ONLY if ALL of these are true:
   - The vulnerability is architecturally impossible (e.g., SQLi with no DB)
   - The evidence does NOT contain actual vulnerability indicators
   - The behavior has a known, documented benign explanation

5. INTERACTIVE: Ask when uncertain. Wrong assumptions = missed vulns or noise.
   Prefer asking one good question over making one bad assumption.

6. DEFENSE IN DEPTH: Even if code "looks safe", test it. Layers fail.

WORKFLOW:
Phase 1: Parse inputs (OpenAPI spec, optional source/Dockerfile)
Phase 2: Safety pre-flight (BLOCK if unsafe, offer mitigations)
Phase 3: Interactive interview (prioritized, minimal questions)
Phase 4: Threat model synthesis (show to user for confirmation)
Phase 5: Generate scan configuration (all artifacts, production-ready)
Phase 6: Post-scan triage (confidence-rated findings)

SAFETY RULES (non-negotiable):
- NEVER generate config targeting a production URL without explicit override
- ALWAYS use internal Docker network (no internet access during scan)
- ALWAYS check for DB connections and override to ephemeral
- ALWAYS check for payment/email/webhook services and disable or mock
- ALWAYS warn about destructive HTTP methods (POST/PUT/DELETE)
- If in doubt about safety → ASK, don't assume

FALSE POSITIVE ASSESSMENT:
- "500 on bad input" without SQL errors → NOT SQLi, IS validation bug. Downgrade.
- "500 on bad input" WITH SQL errors → REAL SQLi even if "no DB expected". Escalate.
- Low confidence from scanner → downgrade, never suppress
- Scanner says X but it's actually Y → reclassify, never suppress

SSRF SPECIAL ATTENTION:
Any API that makes outbound HTTP requests based on user input (proxies, webhooks, 
URL fetchers, redirect handlers, file importers) MUST be flagged for SSRF testing 
even if ZAP won't catch it natively. Generate mock-upstream canary.

OUTPUT REQUIREMENTS:
- All generated files must be production-ready (not pseudocode)
- Include comments explaining WHY each decision was made
- Always generate README.md with: run instructions, expected output, interpretation guide
- Never include secrets/keys in generated files (use placeholders + .env)
```

---

## 10. Example Interaction Flow

```
USER: "Here's my OpenAPI spec: [uploads swagger.json]"

AGENT: "I've analyzed your spec. Here's what I found:
        - 8 GET endpoints across 6 controllers
        - Auth: API Key in X-API-Key header
        - No request bodies (all query/path params)
        - Response types: application/json, application/xml

        Before I proceed, let me run safety checks..."

AGENT: "🛡️ SAFETY PRE-FLIGHT:
        ✅ No database detected
        ✅ GET-only methods (no destructive operations)
        ⚠️  External service calls detected (VIES, INSEE, CH-IDE)
           → Recommendation: disable API keys or use mock-upstream

        Verdict: SAFE TO SCAN (with recommendations applied)

        Now I have a few questions:

        1. [Auth] Do you have a test API key?
        2. [Architecture] These endpoints look like proxies to external 
           services. Confirm? (affects SSRF priority)
        3. [Test data] Valid examples for: VatNumber, Iban, Uid, Siren, Siret?"

USER: "1. Here's a key: xxx  2. Yes, all proxy  3. [provides values]"

AGENT: "Here's my threat model: [shows threat-model.yaml]

        Key priorities:
        - HIGH: SSRF on all proxy endpoints
        - HIGH: Input validation (main defense surface)
        - MEDIUM: Header injection through proxy
        - LOW: Standard OWASP (XSS irrelevant for JSON API)

        I'll suppress: Swagger HTML content-type, cloud metadata 404s
        I'll downgrade: SQLi where no DB exists (→ validation bug)
        I'll ADD: SSRF canary via mock-upstream

        Confirm? [yes] / [modify]"

USER: "Yes, proceed"

AGENT: [Generates all artifacts in zap-dast/ directory]
       "Done. Run with: docker compose -f zap-dast/docker-compose.yaml up

        After scan completes, share report.json and I'll triage the findings."
```

---

## 11. Integration Modes

```
┌─────────────────────────────────────────────────────────────────┐
│  MODE 1: FIRST RUN (Interactive)                                 │
│  Developer runs agent locally                                    │
│  → Answers questions                                            │
│  → Reviews threat model                                         │
│  → Gets generated config                                        │
│  → Commits config to repo                                       │
├─────────────────────────────────────────────────────────────────┤
│  MODE 2: CI/CD (Automated)                                       │
│  Pipeline uses committed config                                  │
│  → Runs scan with pre-approved settings                         │
│  → Agent only does post-scan triage on new reports              │
│  → Fails pipeline if CONFIRMED findings > 0                    │
├─────────────────────────────────────────────────────────────────┤
│  MODE 3: SPEC CHANGE (Re-assessment)                             │
│  Triggered when OpenAPI spec changes (git diff)                  │
│  → Agent analyzes diff                                          │
│  → Flags new endpoints for review                               │
│  → Updates config if needed                                     │
│  → May ask new questions for new endpoints                      │
└─────────────────────────────────────────────────────────────────┘
```

### CI/CD Pipeline Integration:

```yaml
# .github/workflows/dast.yml (example)
name: DAST Security Scan
on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  dast:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Run DAST scan
        run: |
          docker compose -f security/zap-dast/docker-compose.yaml up \
            --abort-on-container-exit \
            --exit-code-from zap

      - name: Upload reports
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: dast-report
          path: security/zap-dast/reports/

      - name: Triage (agent post-scan)
        if: failure()
        run: |
          # Feed report to agent for automated triage
          # Agent applies triage-rules.yaml and outputs summary
          echo "Review security/zap-dast/reports/report.json"
```
