#!/bin/bash
# ZAP DAST Helper - Universal scanner runner with timestamped reports
#
# Usage:
#   zap-helper.sh RUN_DIR YAML_FILE SCAN_TYPE [TRIAGE_SCRIPT]
#
# Example:
#   zap-helper.sh /path/to/zap-dast config/zap-af-baseline.yaml baseline
#   zap-helper.sh /path/to/zap-dast config/zap-af-enriched.yaml enriched scripts/triage-report.py
#
# Environment:
#   SCAN_TIMESTAMP - optional, defaults to YYYY-MM-DD-HHMMSS
#   ZAP_NETWORK - docker network name (default: zap-dast_zap-dast-network)

set -euo pipefail

RUN_DIR="${1:-.}"
YAML_FILE="${2:-}"
SCAN_TYPE="${3:-}"
TRIAGE_SCRIPT="${4:-}"

if [ -z "$YAML_FILE" ] || [ -z "$SCAN_TYPE" ]; then
  echo "Usage: $0 <run_dir> <yaml_file> <scan_type> [triage_script]"
  echo ""
  echo "Arguments:"
  echo "  run_dir        - base directory containing config/ and reports/ subdirs"
  echo "  yaml_file      - relative path to AF YAML (e.g., config/zap-af-enriched.yaml)"
  echo "  scan_type      - identifier for this scan (e.g., baseline, enriched)"
  echo "  triage_script  - optional path to triage script to run after scan"
  echo ""
  echo "Environment variables:"
  echo "  SCAN_TIMESTAMP - timestamp for report folder (default: YYYY-MM-DD-HHMMSS)"
  echo "  ZAP_NETWORK    - docker network name (default: zap-dast_zap-dast-network)"
  exit 1
fi

# Defaults
SCAN_TIMESTAMP="${SCAN_TIMESTAMP:-$(date +%Y-%m-%d-%H%M%S)}"
ZAP_NETWORK="${ZAP_NETWORK:-zap-dast_zap-dast-network}"

# Resolve paths
RUN_DIR="$(cd "$RUN_DIR" && pwd)"
YAML_PATH="$RUN_DIR/$YAML_FILE"
REPORT_DIR="$RUN_DIR/reports/$SCAN_TIMESTAMP-$SCAN_TYPE"

if [ ! -f "$YAML_PATH" ]; then
  echo "ERROR: YAML file not found: $YAML_PATH"
  exit 1
fi

echo "Scan type: $SCAN_TYPE"
echo "Timestamp: $SCAN_TIMESTAMP"
echo "Report dir: $REPORT_DIR"
echo

# Create report directory
mkdir -p "$REPORT_DIR"

# Create temporary YAML with timestamp substituted
TMP_YAML="/tmp/zap-$SCAN_TYPE-$SCAN_TIMESTAMP.yaml"
sed "s|\${SCAN_TIMESTAMP}|$SCAN_TIMESTAMP|g" "$YAML_PATH" > "$TMP_YAML"

echo "Running ZAP scan..."
docker run --rm \
  -v "$RUN_DIR:/zap/wrk:rw" \
  -v "$TMP_YAML:/tmp/zap-config.yaml:ro" \
  --network "$ZAP_NETWORK" \
  ghcr.io/zaproxy/zaproxy:stable \
  zap.sh -cmd -autorun /tmp/zap-config.yaml

rm -f "$TMP_YAML"
echo "✓ Scan complete"
echo

# Run triage if specified
if [ -n "$TRIAGE_SCRIPT" ]; then
  JSON_REPORT="$REPORT_DIR/report-$SCAN_TYPE.json"
  if [ -f "$JSON_REPORT" ]; then
    echo "Running triage..."
    python3 "$RUN_DIR/$TRIAGE_SCRIPT" "$JSON_REPORT"
  else
    echo "⚠ JSON report not found at $JSON_REPORT"
  fi
fi

echo
echo "Reports saved to: $REPORT_DIR"
