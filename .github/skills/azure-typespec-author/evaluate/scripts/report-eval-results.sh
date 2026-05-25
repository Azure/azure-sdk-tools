#!/usr/bin/env bash
# report-eval-results.sh — Report failures from the latest Vally evaluation results.
# Usage: report-eval-results.sh <suite-name>

set -euo pipefail

SUITE_NAME="${1:?Usage: report-eval-results.sh <suite-name>}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EVAL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$EVAL_DIR"

LATEST=$(find results -name 'results.jsonl' -type f -printf '%T@ %p\n' 2>/dev/null | sort -rn | head -1 | cut -d' ' -f2)
if [ -z "$LATEST" ]; then
  echo "##vso[task.logissue type=warning]No results found for suite $SUITE_NAME"
  exit 0
fi

python3 << EOF
import json, sys

REPORT_GRADERS = {"tool-calls", "skill-invocation"}

failed_count = 0
with open("$LATEST") as f:
    for line in f:
        r = json.loads(line)
        name = r.get("gradeResult", {}).get("stimulusName", "")
        if not name:
            continue
        grade = r.get("gradeResult", {})
        score = grade.get("score", 1)
        if not isinstance(score, (int, float)) or score >= 1.0:
            continue
        failed_count += 1
        print(f"[FAILED] {name} (score: {score})")
        for g in grade.get("details", []):
            gname = g.get("name", "?")
            if gname not in REPORT_GRADERS:
                continue
            if not g.get("passed", True):
                evidence = g.get("evidence", "no evidence")
                evidence_lines = [l for l in evidence.splitlines() if not l.startswith("No disallowed")]
                evidence = "\n".join(evidence_lines).strip()
                if evidence:
                    print(f'  - {gname}: {evidence}')
                else:
                    print(f'  - {gname}')
            elif isinstance(g.get("score"), (int, float)) and g.get("score") < 1.0:
                evidence = g.get("evidence", "")
                evidence_lines = [l for l in evidence.splitlines() if not l.startswith("No disallowed")]
                evidence = "\n".join(evidence_lines).strip()
                if evidence:
                    print(f'  - {gname} (score: {g.get("score")}): {evidence}')
                else:
                    print(f'  - {gname} (score: {g.get("score")})')

if failed_count > 0:
    print(f"\n##vso[task.logissue type=error]Suite $SUITE_NAME: {failed_count} stimulus/stimuli failed")
else:
    print(f"Suite $SUITE_NAME: all stimuli passed")
EOF
