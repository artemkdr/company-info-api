#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Generate timestamp: YYYY-MM-DD-HHMMSS
SCAN_TIMESTAMP=$(date +%Y-%m-%d-%H%M%S)

echo "Scan timestamp: $SCAN_TIMESTAMP"
echo "Reports will be saved to reports/$SCAN_TIMESTAMP-{baseline,enriched}/"
echo

run_scan() {
  local scan_type=$1
  local yaml_file=$2
  local report_subdir=$3

  echo "=== Running $scan_type Scan ==="
  local report_path="$SCRIPT_DIR/reports/$SCAN_TIMESTAMP-$report_subdir"
  mkdir -p "$report_path"

  # Create temporary YAML with timestamp substituted
  local tmp_yaml="/tmp/zap-$scan_type-$SCAN_TIMESTAMP.yaml"
  sed "s|\${SCAN_TIMESTAMP}|$SCAN_TIMESTAMP|g" "$SCRIPT_DIR/config/$yaml_file" > "$tmp_yaml"

  # Run ZAP scan
  docker run --rm \
    -v "$SCRIPT_DIR:/zap/wrk:rw" \
    -v "$tmp_yaml:/tmp/zap-config.yaml:ro" \
    --network zap-dast_zap-dast-network \
    ghcr.io/zaproxy/zaproxy:stable \
    zap.sh -cmd -autorun /tmp/zap-config.yaml

  rm -f "$tmp_yaml"
  echo "✓ $scan_type scan complete"
  echo
}

run_triage() {
  local enriched_json="$SCRIPT_DIR/reports/$SCAN_TIMESTAMP-enriched/report-enriched.json"

  if [ ! -f "$enriched_json" ]; then
    echo "⚠ Enriched JSON report not found at $enriched_json, skipping triage"
    return 1
  fi

  echo "=== Running Triage ==="
  python3 "$SCRIPT_DIR/scripts/triage-report.py" "$enriched_json"
  echo
}

# Main logic
case "${1:-both}" in
  baseline)
    run_scan "Baseline" "zap-af-baseline.yaml" "baseline"
    ;;
  enriched)
    run_scan "Enriched" "zap-af-enriched.yaml" "enriched"
    run_triage
    ;;
  both)
    run_scan "Baseline" "zap-af-baseline.yaml" "baseline"
    run_scan "Enriched" "zap-af-enriched.yaml" "enriched"
    run_triage
    ;;
  *)
    echo "Usage: $0 [baseline|enriched|both]"
    exit 1
    ;;
esac

echo "Reports saved to: $SCRIPT_DIR/reports/$SCAN_TIMESTAMP-{baseline,enriched}/"
