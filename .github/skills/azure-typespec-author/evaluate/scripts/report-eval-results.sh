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
import json
from collections import defaultdict

REPORT_GRADERS = {"tool-calls", "skill-invocation"}

def trial_passed(grade):
    score = grade.get("score", 1)
    return isinstance(score, (int, float)) and score >= 1.0

# With config.runs > 1, Vally emits one record per trial. Group trials by
# stimulus so a flaky stimulus is judged with pass@k semantics: it passes the
# suite if it succeeds in at least one of its trials.
trials = defaultdict(list)  # stimulus name -> list of gradeResult dicts
with open("$LATEST") as f:
    for line in f:
        r = json.loads(line)
        grade = r.get("gradeResult", {})
        name = grade.get("stimulusName", "")
        if not name:
            continue
        trials[name].append(grade)

failed_count = 0
flaky_count = 0
# Per case (numeric prefix) summary.
case_data = {}

for name in sorted(trials):
    grades = trials[name]
    total = len(grades)
    passed_trials = sum(1 for g in grades if trial_passed(g))
    # pass@k: the stimulus passes if any trial passed.
    passed = passed_trials > 0
    flaky = 0 < passed_trials < total
    case_id = name.split("-")[0] if name else "unknown"

    # tool-calls grader is considered satisfied if it passed in any trial.
    tool_calls_passed = None
    for g in grades:
        for d in g.get("details", []):
            if d.get("name", d.get("type", "?")) == "tool-calls":
                tc = d.get("passed", True)
                tool_calls_passed = tc if tool_calls_passed is None else (tool_calls_passed or tc)
    case_data[case_id] = {
        "passed": passed,
        "flaky": flaky,
        "passed_trials": passed_trials,
        "total": total,
        "tool_calls_passed": tool_calls_passed,
        "trial_statuses": [trial_passed(g) for g in grades],
    }

    if flaky:
        flaky_count += 1
        print(f"[FLAKY] {name} (passed {passed_trials}/{total} trials)")

    if passed:
        continue

    # Hard failure: every trial failed. Report evidence from the first trial.
    failed_count += 1
    print(f"[FAILED] {name} (passed {passed_trials}/{total} trials)")
    for g in grades[:1]:
        for d in g.get("details", []):
            gname = d.get("name", d.get("type", "?"))
            if gname not in REPORT_GRADERS:
                continue
            if not d.get("passed", True):
                evidence = d.get("evidence", "no evidence")
                evidence_lines = [l for l in evidence.splitlines() if not l.startswith("No disallowed")]
                evidence = "\n".join(evidence_lines).strip()
                if evidence:
                    print(f'  - {gname}: {evidence}')
                else:
                    print(f'  - {gname}')
            elif isinstance(d.get("score"), (int, float)) and d.get("score") < 1.0:
                evidence = d.get("evidence", "")
                evidence_lines = [l for l in evidence.splitlines() if not l.startswith("No disallowed")]
                evidence = "\n".join(evidence_lines).strip()
                if evidence:
                    print(f'  - {gname} (score: {d.get("score")}): {evidence}')
                else:
                    print(f'  - {gname} (score: {d.get("score")})')

# Per-trial pass rate: for trial index t, how many stimuli passed that trial.
# The i-th record of a stimulus is its i-th trial (Vally writes one record per
# trial when config.runs > 1).
max_trials = max((len(g) for g in trials.values()), default=0)
trial_passed_counts = [0] * max_trials
trial_total_counts = [0] * max_trials
for grades in trials.values():
    for t, g in enumerate(grades):
        trial_total_counts[t] += 1
        if trial_passed(g):
            trial_passed_counts[t] += 1

# Print per-trial summary, then the count of stimuli that failed every trial.
total_cases = len(case_data)
total_passed = sum(1 for v in case_data.values() if v["passed"])
print(f"\n--- Suite $SUITE_NAME trial results ---")
for t in range(max_trials):
    print(f"trial {t + 1} ({trial_passed_counts[t]}/{trial_total_counts[t]})")
print(f"fail in {'both' if max_trials == 2 else 'all'} trials: {failed_count}")

# Per-case pass@k breakdown with each trial's status (a case passes if any trial passes).
print(f"\nPass@k: {total_passed}/{total_cases} ({total_passed*100//total_cases if total_cases else 0}%), {flaky_count} flaky")
for case_id in sorted(case_data.keys()):
    r = case_data[case_id]
    status = "PASS" if r["passed"] else "FAIL"
    trial_marks = " ".join(
        f"trial{i + 1}:{'PASS' if ok else 'FAIL'}" for i, ok in enumerate(r["trial_statuses"])
    )
    tc = r.get("tool_calls_passed")
    tc_mark = "tool-calls:pass" if tc else "tool-calls:fail" if tc is not None else ""
    print(f"  {case_id} [{status}] {trial_marks} {tc_mark}".rstrip())

if failed_count > 0:
    print(f"\n##vso[task.logissue type=error]Suite $SUITE_NAME: {failed_count} stimulus/stimuli failed all trials (pass@k: {total_passed}/{total_cases})")
else:
    print(f"Suite $SUITE_NAME: all stimuli passed within trials ({total_passed}/{total_cases}, {flaky_count} flaky)")
EOF
