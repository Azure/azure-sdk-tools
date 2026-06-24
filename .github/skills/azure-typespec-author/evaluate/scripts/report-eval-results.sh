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
from collections import defaultdict

REPORT_GRADERS = {"tool-calls", "skill-invocation"}

failed_count = 0
# Track per case: overall pass and non-tool grader results
case_data = {}

with open("$LATEST") as f:
    for line in f:
        r = json.loads(line)
        grade = r.get("gradeResult") or {}
        name = grade.get("stimulusName") or r.get("stimulusName", "")
        if not name:
            # No grade and no stimulus name — nothing actionable to report.
            continue
        case_id = name.split("-")[0]
        if not grade:
            # Stimulus produced no grade result (e.g. it errored or timed out).
            # Surface it as a failure instead of crashing the report.
            failed_count += 1
            case_data[case_id] = {"passed": False, "score": 0, "tool_calls_passed": None}
            print(f"[FAILED] {name} (no grade result — stimulus errored or timed out)")
            continue
        score = grade.get("score", 1)
        # A case passes when every grader meets its own threshold. Do NOT use the
        # aggregate score (a weighted average of raw grader scores), because a
        # passing LLM prompt grader (e.g. threshold 0.77 on a scale_1_10 rubric)
        # can score below 1.0 and incorrectly drag the aggregate under 1.0.
        details = grade.get("details", [])
        if details:
            passed = all(g.get("passed", True) for g in details)
        else:
            passed = grade.get("passed", isinstance(score, (int, float)) and score >= 1.0)

        # Collect tool-calls grader result
        tool_calls_passed = None
        for g in grade.get("details", []):
            gname = g.get("name", g.get("type", "?"))
            if gname == "tool-calls":
                tool_calls_passed = g.get("passed", True)
                break

        case_data[case_id] = {"passed": passed, "score": score, "tool_calls_passed": tool_calls_passed}

        if passed:
            continue
        failed_count += 1
        print(f"[FAILED] {name} (score: {score})")
        for g in grade.get("details", []):
            gname = g.get("name", g.get("type", "?"))
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

# Print per-case pass rate summary
total_cases = len(case_data)
total_passed = sum(1 for v in case_data.values() if v["passed"])
print(f"\n--- Suite $SUITE_NAME Pass Rate: {total_passed}/{total_cases} ({total_passed*100//total_cases if total_cases else 0}%) ---")
for case_id in sorted(case_data.keys()):
    r = case_data[case_id]
    status = "PASS" if r["passed"] else "FAIL"
    tc = r.get("tool_calls_passed")
    tc_mark = "✔tool-calls" if tc else "✘tool-calls" if tc is not None else ""
    print(f"  {case_id} [{status}] {tc_mark}")

if failed_count > 0:
    print(f"\n##vso[task.logissue type=error]Suite $SUITE_NAME: {failed_count} stimulus/stimuli failed (pass rate: {total_passed}/{total_cases})")
else:
    print(f"Suite $SUITE_NAME: all stimuli passed ({total_passed}/{total_cases})")
EOF
