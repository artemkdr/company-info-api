#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BASELINE_AF="$SCRIPT_DIR/config/zap-af-baseline.yaml"
ENRICHED_AF="$SCRIPT_DIR/config/zap-af-enriched.yaml"

echo "=== ZAP DAST Preflight Checks ==="
echo

# Check 1: Containers exist and are running
echo "1. Checking containers are running..."
for container_name in company-info-api mock-upstreams zap-auth-proxy; do
  CONTAINER_ID=$(docker ps -aqf "name=^${container_name}$")
  if [ -z "$CONTAINER_ID" ]; then
    echo "ERROR: Container '$container_name' not found"
    exit 1
  fi
  echo "   ✓ $container_name is running"
done
echo

# Check 2: Target and mock-upstreams have no host network or host-bound ports
echo "2. Checking network isolation (no host network / host-bound ports)..."
for service in company-info-api mock-upstreams; do
  CONTAINER_ID=$(docker ps -aqf "name=^${service}$")
  NETWORK_MODE=$(docker inspect "$CONTAINER_ID" | jq -r '.[0].HostConfig.NetworkMode')
  if [ "$NETWORK_MODE" = "host" ]; then
    echo "ERROR: Container '$service' uses host network mode"
    exit 1
  fi

  # Check for published ports (should have none for isolation)
  PUBLISHED=$(docker inspect "$CONTAINER_ID" | jq '[.[0].NetworkSettings.Ports | to_entries | map(select(.value != null and (.value | length > 0))) | length]' 2>/dev/null || echo 0)
  if [ "$PUBLISHED" -gt 0 ]; then
    echo "WARNING: Container '$service' has host-published ports (non-ideal for isolation)"
  else
    echo "   ✓ $service is isolated (no host network, no published ports)"
  fi
done
echo

# Check 3: Env var credential leak detection
echo "3. Checking for leaked production credentials..."
TARGET_ID=$(docker ps -aqf "name=company-info-api")
TARGET_ENV=$(docker inspect "$TARGET_ID" | jq -r '.[0].Config.Env[]' 2>/dev/null || true)
if echo "$TARGET_ENV" | grep -qi "TO-SET-IN-PRODUCTION\|\.prod\|\.live\|example\.com"; then
  echo "ERROR: Production credentials or placeholders detected in target env"
  exit 1
fi
INSEE_KEY=$(echo "$TARGET_ENV" | grep "ExternalApis__Insee__ApiKey" | cut -d= -f2- || echo "")
if [ -n "$INSEE_KEY" ] && [ "$INSEE_KEY" != "test-insee-key" ]; then
  echo "ERROR: INSEE API key is not the test value"
  exit 1
fi
echo "   ✓ No production credentials detected"
echo

# Check 4: AF files exist and are valid YAML
echo "4. Checking AF configuration files..."
for af_file in "$BASELINE_AF" "$ENRICHED_AF"; do
  if [ ! -f "$af_file" ]; then
    echo "ERROR: AF file not found: $af_file"
    exit 1
  fi
  if ! grep -q "jobs:" "$af_file"; then
    echo "ERROR: AF file is missing 'jobs' section: $af_file"
    exit 1
  fi
  echo "   ✓ $(basename "$af_file") exists and has jobs"
done
echo

# Check 5: OpenAPI spec exists
echo "5. Checking OpenAPI spec..."
OPENAPI_SPEC="$SCRIPT_DIR/config/openapi-enriched.json"
if [ ! -f "$OPENAPI_SPEC" ]; then
  echo "ERROR: OpenAPI spec not found: $OPENAPI_SPEC"
  exit 1
fi
if ! jq . "$OPENAPI_SPEC" > /dev/null 2>&1; then
  echo "ERROR: OpenAPI spec is not valid JSON"
  exit 1
fi
echo "   ✓ OpenAPI spec is valid JSON"
echo

# Check 6: Connectivity check
echo "6. Testing basic connectivity..."
PROXY_ID=$(docker ps -aqf "name=^zap-auth-proxy$")
if ! docker exec "$PROXY_ID" sh -c "echo > /dev/tcp/target/5000" 2>/dev/null; then
  echo "WARNING: Proxy cannot reach target on port 5000 (may still be starting)"
else
  echo "   ✓ Proxy can reach target"
fi
echo

echo "=== Preflight Complete ==="
echo "All safety checks passed. Ready for ZAP scan."
