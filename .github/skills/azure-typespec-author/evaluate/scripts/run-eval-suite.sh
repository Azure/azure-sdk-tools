#!/usr/bin/env bash
# run-eval-suite.sh — Run a single Vally evaluation suite and report failures.
# Usage: run-eval-suite.sh <eval-file> <suite-name>

set -euo pipefail

EVAL_FILE="${1:?Usage: run-eval-suite.sh <eval-file> <suite-name>}"
SUITE_NAME="${2:?Usage: run-eval-suite.sh <eval-file> <suite-name>}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EVAL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$EVAL_DIR"

vally eval --eval-spec "suites/$EVAL_FILE" --output-dir results --verbose || true
EXIT_CODE=${PIPESTATUS[0]:-$?}

if [ $EXIT_CODE -ne 0 ]; then
  echo "##vso[task.logissue type=error]Suite $SUITE_NAME failed (exit code $EXIT_CODE)"
  LATEST=$(find results -name 'results.jsonl' -type f -printf '%T@ %p\n' | sort -rn | head -1 | cut -d' ' -f2)
  if [ -n "$LATEST" ]; then
    python3 << EOF
import json, sys

REPORT_GRADERS = {"tool-calls", "skill-invocation"}

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
EOF
  fi
  exit 0
fi
