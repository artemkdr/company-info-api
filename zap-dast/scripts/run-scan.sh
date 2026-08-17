#!/bin/bash
set -euo pipefail

# Helper script to run baseline and enriched scans with timestamped report directories
# Usage: ./scripts/run-scan.sh [baseline|enriched|both]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCAN_TYPE="${1:-both}"

# Generate timestamp: YYYY-MM-DD-HH:MM:SS
SCAN_TIMESTAMP=$(date +%Y-%m-%d-%H:%M:%S)
export SCAN_TIMESTAMP

echo "Scan timestamp: $SCAN_TIMESTAMP"
echo "Reports will be saved to reports/$SCAN_TIMESTAMP-{baseline,enriched}/"
echo

run_baseline() {
  echo "=== Running Baseline Scan ==="
  mkdir -p "$SCRIPT_DIR/reports/$SCAN_TIMESTAMP-baseline"
  docker run --rm \
    -v "$SCRIPT_DIR:/zap/wrk:rw" \
    -e SCAN_TIMESTAMP="$SCAN_TIMESTAMP" \
    --network zap-dast_zap-dast-network \
    ghcr.io/zaproxy/zaproxy:stable \
    zap.sh -cmd -autorun /zap/wrk/config/zap-af-baseline.yaml
  echo "✓ Baseline scan complete"
}

run_enriched() {
  echo "=== Running Enriched Scan ==="
  mkdir -p "$SCRIPT_DIR/reports/$SCAN_TIMESTAMP-enriched"
  docker run --rm \
    -v "$SCRIPT_DIR:/zap/wrk:rw" \
    -e SCAN_TIMESTAMP="$SCAN_TIMESTAMP" \
    --network zap-dast_zap-dast-network \
    ghcr.io/zaproxy/zaproxy:stable \
    zap.sh -cmd -autorun /zap/wrk/config/zap-af-enriched.yaml
  echo "✓ Enriched scan complete"
}

run_triage() {
  ENRICHED_REPORT="$SCRIPT_DIR/reports/$SCAN_TIMESTAMP-enriched/report-enriched.json"
  if [ -f "$ENRICHED_REPORT" ]; then
    echo "=== Running Triage ==="
    python3 "$SCRIPT_DIR/scripts/triage-report.py" "$ENRICHED_REPORT"
  else
    echo "⚠ Enriched JSON report not found at $ENRICHED_REPORT, skipping triage"
  fi
}

case "$SCAN_TYPE" in
  baseline)
    run_baseline
    ;;
  enriched)
    run_enriched
    ;;
  both)
    run_baseline
    echo
    run_enriched
    echo
    run_triage
    ;;
  *)
    echo "Usage: $0 [baseline|enriched|both]"
    exit 1
    ;;
esac

echo
echo "Reports saved to: $SCRIPT_DIR/reports/$SCAN_TIMESTAMP-{baseline,enriched}/"
