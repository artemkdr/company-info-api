#!/usr/bin/env bash
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

SCAN_TIMESTAMP=$(date +%Y-%m-%d-%H%M%S)

echo "Scan timestamp: $SCAN_TIMESTAMP"
echo "Reports will be saved to reports/$SCAN_TIMESTAMP-baseline/ and reports/$SCAN_TIMESTAMP-enriched/"
echo

cleanup() {
  rm -f /tmp/zap-*-"$SCAN_TIMESTAMP".yaml
}
trap cleanup EXIT

run_scan() {
  local scan_type=$1
  local yaml_file=$2
  local report_subdir=$3

  local config_file="$SCRIPT_DIR/config/$yaml_file"
  if [[ ! -f "$config_file" ]]; then
    echo "✗ Config file not found: $config_file"
    return 1
  fi

  echo "=== Running $scan_type Scan ==="
  local report_path="$SCRIPT_DIR/reports/$SCAN_TIMESTAMP-$report_subdir"
  mkdir -p "$report_path"

  local tmp_yaml
  tmp_yaml=$(mktemp "/tmp/zap-$scan_type-$SCAN_TIMESTAMP.XXXXXX.yaml")
  sed "s|\${SCAN_TIMESTAMP}|$SCAN_TIMESTAMP|g" "$config_file" > "$tmp_yaml"

  local exit_code=0
  docker run --rm \
    -v "$SCRIPT_DIR:/zap/wrk:rw" \
    -v "$tmp_yaml:/tmp/zap-config.yaml:ro" \
    --network zap-dast_zap-dast-network \
    ghcr.io/zaproxy/zaproxy:stable \
    zap.sh -cmd -autorun /tmp/zap-config.yaml || exit_code=$?

  rm -f "$tmp_yaml"

  # ZAP exit codes: 0=pass, 1=error, 2=warnings found, 3=fail
  if [[ $exit_code -eq 1 ]]; then
    echo "✗ $scan_type scan failed with ZAP error (exit code 1)"
    return 1
  fi

  if [[ $exit_code -eq 2 ]]; then
    echo "⚠ $scan_type scan complete (warnings found)"
  elif [[ $exit_code -eq 3 ]]; then
    echo "⚠ $scan_type scan complete (failures found)"
  else
    echo "✓ $scan_type scan complete"
  fi
  echo
  return 0
}

run_triage() {
  local enriched_json="$SCRIPT_DIR/reports/$SCAN_TIMESTAMP-enriched/report-enriched.json"
  local threat_model_file="$SCRIPT_DIR/threat-model.json"

  if [[ ! -f "$enriched_json" ]]; then
    echo "⚠ Enriched JSON report not found at $enriched_json, skipping triage"
    return 0
  fi

  echo "=== Running Triage ==="
  python3 "$SCRIPT_DIR/scripts/triage-report.py" "$enriched_json" "$threat_model_file"
  echo "✓ Triage complete"
  echo
}

main() {
  local mode="${1:-both}"
  local had_error=0

  case "$mode" in
    baseline)
      run_scan "Baseline" "zap-af-baseline.yaml" "baseline" || had_error=1
      ;;
    enriched)
      run_scan "Enriched" "zap-af-enriched.yaml" "enriched" || had_error=1
      [[ $had_error -eq 0 ]] && run_triage
      ;;
    both)
      run_scan "Baseline" "zap-af-baseline.yaml" "baseline" || had_error=1
      run_scan "Enriched" "zap-af-enriched.yaml" "enriched" || had_error=1
      run_triage
      ;;
    *)
      echo "Usage: $0 [baseline|enriched|both]"
      exit 1
      ;;
  esac

  echo "Reports saved to: $SCRIPT_DIR/reports/$SCAN_TIMESTAMP-baseline/ and $SCRIPT_DIR/reports/$SCAN_TIMESTAMP-enriched/"
  return $had_error
}

main "$@"