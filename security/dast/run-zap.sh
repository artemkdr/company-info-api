#!/usr/bin/env bash
set -euo pipefail

# --- Defaults ---
OPENAPI_URL=""
TARGET_URL=""
API_KEY=""
HEADER_NAME="X-API-Key"
POLICY_FILE=""
REPORT_FORMAT="html"
NETWORK="host"
SCAN_MODE=""
REPORT_DIR="$(cd "$(dirname "$0")" && pwd)/reports"

# --- Colors ---
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

# --- Prod deny-list (add your patterns) ---
PROD_PATTERNS=(
  "prod"
  "production"
  "live\."
  "\.com"
  "\.ch"
)

usage() {
  cat <<EOF
Usage: $(basename "$0") [OPTIONS]

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
EOF
  exit 0
}

# --- Parse args ---
while [[ $# -gt 0 ]]; do
  case "$1" in
    --openapi)   OPENAPI_URL="$2"; SCAN_MODE="api"; shift 2 ;;
    --target)    TARGET_URL="$2"; SCAN_MODE="full"; shift 2 ;;
    --api-key)   API_KEY="$2"; shift 2 ;;
    --header-name) HEADER_NAME="$2"; shift 2 ;;
    --policy)    POLICY_FILE="$2"; shift 2 ;;
    --report-format) REPORT_FORMAT="$2"; shift 2 ;;
    --network)   NETWORK="$2"; shift 2 ;;
    -h|--help)   usage ;;
    *) echo "Unknown option: $1"; usage ;;
  esac
done

# --- Validate ---
if [[ -z "$SCAN_MODE" ]]; then
  echo -e "${RED}Error: Must specify --openapi or --target${NC}"
  usage
fi

if [[ "$REPORT_FORMAT" != "html" && "$REPORT_FORMAT" != "json" && "$REPORT_FORMAT" != "xml" ]]; then
  echo -e "${RED}Error: Invalid report format. Use html|json|xml${NC}"
  exit 1
fi

# --- Resolve scan target URL for safety check ---
if [[ "$SCAN_MODE" == "api" ]]; then
  CHECK_URL="$OPENAPI_URL"
else
  CHECK_URL="$TARGET_URL"
fi

# --- Safety gate ---
safety_check() {
  echo -e "${YELLOW}╔══════════════════════════════════════════════════════════════╗${NC}"
  echo -e "${YELLOW}║  ⚠️  ZAP DAST - DESTRUCTIVE SECURITY SCAN                   ║${NC}"
  echo -e "${YELLOW}╠══════════════════════════════════════════════════════════════╣${NC}"
  echo -e "${YELLOW}║  This scan WILL:                                            ║${NC}"
  echo -e "${YELLOW}║  - Send malicious payloads (SQLi, XSS, etc.)               ║${NC}"
  echo -e "${YELLOW}║  - Fuzz all discovered endpoints (POST, PUT, DELETE)        ║${NC}"
  echo -e "${YELLOW}║  - Potentially corrupt data in connected databases          ║${NC}"
  echo -e "${YELLOW}║                                                             ║${NC}"
  echo -e "${YELLOW}║  Target: ${CHECK_URL}${NC}"
  echo -e "${YELLOW}║                                                             ║${NC}"
  echo -e "${YELLOW}║  REQUIREMENTS:                                              ║${NC}"
  echo -e "${YELLOW}║  ✓ Target runs in a container or locally                    ║${NC}"
  echo -e "${YELLOW}║  ✓ NO production database connected                         ║${NC}"
  echo -e "${YELLOW}║  ✓ Data loss is acceptable                                  ║${NC}"
  echo -e "${YELLOW}╚══════════════════════════════════════════════════════════════╝${NC}"
  echo ""

  # Check for prod-like URLs
  for pattern in "${PROD_PATTERNS[@]}"; do
    if echo "$CHECK_URL" | grep -qiE "$pattern"; then
      echo -e "${RED}BLOCKED: URL matches production pattern '${pattern}'${NC}"
      echo -e "${RED}If this is intentional, remove the pattern from PROD_PATTERNS in this script.${NC}"
      exit 1
    fi
  done

  if [[ "${ZAP_I_KNOW_WHAT_IM_DOING:-}" == "true" ]]; then
    echo "CI mode: safety confirmation skipped (ZAP_I_KNOW_WHAT_IM_DOING=true)"
    return 0
  fi

  read -rp "Type 'yes' to confirm you understand the risks and proceed: " confirm
  if [[ "$confirm" != "yes" ]]; then
    echo "Aborted."
    exit 1
  fi
}

safety_check

# --- Prepare report directory ---
mkdir -p "$REPORT_DIR"

REPORT_FILE="report.${REPORT_FORMAT}"

# --- Build docker run command ---
DOCKER_ARGS=(
  "docker" "run" "--rm"
  "--network=${NETWORK}"
  "-v" "${REPORT_DIR}:/zap/wrk:rw"
)

# Mount policy file if provided
if [[ -n "$POLICY_FILE" ]]; then
  if [[ ! -f "$POLICY_FILE" ]]; then
    echo -e "${RED}Error: Policy file not found: ${POLICY_FILE}${NC}"
    exit 1
  fi
  DOCKER_ARGS+=("-v" "$(cd "$(dirname "$POLICY_FILE")" && pwd)/$(basename "$POLICY_FILE"):/zap/policy.conf:ro")
fi

# Mount local OpenAPI file if it's a file path (not URL)
if [[ "$SCAN_MODE" == "api" && ! "$OPENAPI_URL" =~ ^https?:// ]]; then
  if [[ ! -f "$OPENAPI_URL" ]]; then
    echo -e "${RED}Error: OpenAPI file not found: ${OPENAPI_URL}${NC}"
    exit 1
  fi
  OPENAPI_FILENAME="$(basename "$OPENAPI_URL")"
  DOCKER_ARGS+=("-v" "$(cd "$(dirname "$OPENAPI_URL")" && pwd)/${OPENAPI_FILENAME}:/zap/${OPENAPI_FILENAME}:ro")
  OPENAPI_URL="/zap/${OPENAPI_FILENAME}"
fi

DOCKER_ARGS+=("ghcr.io/zaproxy/zaproxy:stable")

# --- Build ZAP command ---
if [[ "$SCAN_MODE" == "api" ]]; then
  ZAP_CMD=("zap-api-scan.py" "-t" "$OPENAPI_URL" "-f" "openapi")
else
  ZAP_CMD=("zap-full-scan.py" "-t" "$TARGET_URL")
fi

# Auth via replacer addon
if [[ -n "$API_KEY" ]]; then
  ZAP_CMD+=(
    "-z"
    "-config replacer.full_list(0).matchtype=REQ_HEADER \
-config replacer.full_list(0).matchstr=${HEADER_NAME} \
-config replacer.full_list(0).replacement=${API_KEY} \
-config replacer.full_list(0).matchregex=false \
-config replacer.full_list(0).enabled=true"
  )
fi

# Policy file
if [[ -n "$POLICY_FILE" ]]; then
  ZAP_CMD+=("-c" "/zap/policy.conf")
fi

# Report
case "$REPORT_FORMAT" in
  html) ZAP_CMD+=("-r" "$REPORT_FILE") ;;
  json) ZAP_CMD+=("-J" "$REPORT_FILE") ;;
  xml)  ZAP_CMD+=("-x" "$REPORT_FILE") ;;
esac

# --- Execute ---
echo ""
echo "Starting ZAP scan..."
echo "Command: ${DOCKER_ARGS[*]} ${ZAP_CMD[*]}"
echo ""

"${DOCKER_ARGS[@]}" "${ZAP_CMD[@]}"

EXIT_CODE=$?

echo ""
echo "────────────────────────────────────────"
if [[ $EXIT_CODE -eq 0 ]]; then
  echo "✅ Scan complete. No alerts above threshold."
else
  echo "⚠️  Scan complete. Alerts found (exit code: ${EXIT_CODE})."
fi
echo "Report: ${REPORT_DIR}/${REPORT_FILE}"
echo "────────────────────────────────────────"

exit $EXIT_CODE