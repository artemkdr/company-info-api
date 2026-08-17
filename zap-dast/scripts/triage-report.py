#!/usr/bin/env python3
"""
Deterministic post-scan triage without LLM.
Applies threat-model-based suppressions and flags SSRF/OAST findings.
Exits non-zero if any confirmed High or Medium remain.
"""

import json
import sys
import re
from pathlib import Path

def load_threat_model(threat_model_path):
    """Load threat model JSON."""
    with open(threat_model_path, 'r') as f:
        return json.load(f)

def apply_suppressions(alerts, threat_model):
    """Apply false-positive rules from threat model."""
    fp_rules = threat_model.get('false_positive_rules', [])

    for alert in alerts:
        rule_id = alert.get('pluginId')
        url = alert.get('url', '')

        for fp_rule in fp_rules:
            if fp_rule.get('rule_id') == rule_id:
                url_pattern = fp_rule.get('url_regex', '.*')
                if re.match(url_pattern, url):
                    action = fp_rule.get('action')
                    if action == 'SUPPRESS':
                        alert['riskCode'] = -1  # Mark as suppressed
                    elif action == 'DOWNGRADE':
                        if alert.get('riskCode') == 2:  # High → Medium
                            alert['riskCode'] = 1
                        elif alert.get('riskCode') == 1:  # Medium → Low
                            alert['riskCode'] = 0

    return alerts

def flag_oast_findings(alerts):
    """Flag alerts with OAST evidence as CONFIRMED SSRF."""
    for alert in alerts:
        evidence = alert.get('evidence', [])
        for item in evidence:
            if 'oastData' in item or 'callbackUrl' in item.get('request', {}):
                alert['confirmed'] = True
                alert['riskCode'] = 3  # Critical
                alert['reason'] = 'CONFIRMED SSRF via OAST callback'
                break

    return alerts

def triage(report_path, threat_model_path):
    """Load report, apply rules, and determine pass/fail."""
    with open(report_path, 'r') as f:
        report = json.load(f)

    threat_model = load_threat_model(threat_model_path)

    alerts = report.get('site', [{}])[0].get('alerts', [])

    alerts = apply_suppressions(alerts, threat_model)
    alerts = flag_oast_findings(alerts)

    high_alerts = [a for a in alerts if a.get('riskCode') == 2]
    medium_alerts = [a for a in alerts if a.get('riskCode') == 1]

    print("=== ZAP DAST Triage Results ===")
    print(f"Total alerts: {len(alerts)}")
    print(f"  Critical: {len([a for a in alerts if a.get('riskCode') == 3])}")
    print(f"  High: {len(high_alerts)}")
    print(f"  Medium: {len(medium_alerts)}")
    print(f"  Low: {len([a for a in alerts if a.get('riskCode') == 0])}")
    print(f"  Info: {len([a for a in alerts if a.get('riskCode') == -1])}")
    print()

    if high_alerts:
        print("HIGH SEVERITY FINDINGS:")
        for alert in high_alerts:
            print(f"  - {alert.get('name')} ({alert.get('url')})")
        print()

    if medium_alerts:
        print("MEDIUM SEVERITY FINDINGS:")
        for alert in medium_alerts:
            print(f"  - {alert.get('name')} ({alert.get('url')})")
        print()

    # Write triaged report
    report['site'][0]['alerts'] = alerts
    triaged_path = report_path.parent / f"{report_path.stem}.triaged.json"
    with open(triaged_path, 'w') as f:
        json.dump(report, f, indent=2)
    print(f"Triaged report saved to {triaged_path}")
    print()

    # Determine exit code
    fail_on_high = bool(high_alerts)
    fail_on_medium = bool(medium_alerts)

    if fail_on_high or fail_on_medium:
        print("FAIL: Scan found confirmed High or Medium severity issues.")
        return 1
    else:
        print("PASS: No High or Medium severity issues found.")
        return 0

if __name__ == '__main__':
    if len(sys.argv) < 3:
        print("Usage: triage-report.py <report.json> <threat-model.json>")
        sys.exit(1)

    report_path = Path(sys.argv[1])
    threat_model_path = Path(sys.argv[2])

    if not report_path.exists():
        print(f"ERROR: Report file not found: {report_path}")
        sys.exit(1)

    if not threat_model_path.exists():
        print(f"ERROR: Threat model file not found: {threat_model_path}")
        sys.exit(1)

    sys.exit(triage(report_path, threat_model_path))
