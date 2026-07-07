#!/usr/bin/env bash
# report-eval-results.sh - Report failures from the latest Vally evaluation results.
# Usage: report-eval-results.sh <suite-name>

set -euo pipefail

SUITE_NAME="${1:?Usage: report-eval-results.sh <suite-name>}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EVAL_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
RESULTS_DIR="$EVAL_DIR/results"

LATEST=""
if [ -d "$RESULTS_DIR" ]; then
  LATEST=$(find "$RESULTS_DIR" -name 'results.jsonl' -type f -printf '%T@ %p\n' 2>/dev/null | sort -rn | head -1 | cut -d' ' -f2-)
fi

if [ -z "$LATEST" ]; then
  echo "##vso[task.logissue type=warning]No results found for suite $SUITE_NAME"
  exit 0
fi

python3 << EOF
import json
from collections import defaultdict

RESULTS_FILE = r"""$LATEST"""
REPORT_GRADERS = {"tool-calls", "skill-invocation"}


def stimulus_name(record, grade):
    name = grade.get("stimulusName") or record.get("stimulusName") or record.get("stimulus") or ""
    if isinstance(name, dict):
        name = name.get("name", "")
    if not name and record.get("itemId"):
        parts = record["itemId"].split("::")
        name = parts[3] if len(parts) > 3 else parts[-1]
    return name


def trial_passed(grade):
    if not grade:
        return False

    details = grade.get("details", [])
    if details:
        return all(g.get("passed", True) for g in details)

    score = grade.get("score", 1)
    return grade.get("passed", isinstance(score, (int, float)) and score >= 1.0)


def clean_evidence(evidence):
    evidence_lines = [line for line in evidence.splitlines() if not line.startswith("No disallowed")]
    return "\n".join(evidence_lines).strip()


def report_grader_failure(grader):
    gname = grader.get("name", grader.get("type", "?"))
    if gname not in REPORT_GRADERS:
        return []

    lines = []
    if not grader.get("passed", True):
        evidence = clean_evidence(grader.get("evidence", "no evidence"))
        if evidence:
            lines.append(f"  - {gname}: {evidence}")
        else:
            lines.append(f"  - {gname}")
    elif isinstance(grader.get("score"), (int, float)) and grader.get("score") < 1.0:
        evidence = clean_evidence(grader.get("evidence", ""))
        if evidence:
            lines.append(f"  - {gname} (score: {grader.get('score')}): {evidence}")
        else:
            lines.append(f"  - {gname} (score: {grader.get('score')})")
    return lines


# With config.runs > 1, Vally emits one record per trial. Group trials by
# stimulus so report verdicts use pass@k semantics: a stimulus passes if at
# least one trial passes. Keep per-trial status visible so instability is not
# hidden by the pass@k verdict.
trials = defaultdict(list)

with open(RESULTS_FILE, encoding="utf-8") as results:
    for line in results:
        record = json.loads(line)
        grade = record.get("gradeResult") or {}
        name = stimulus_name(record, grade)
        if not name:
            # No grade and no stimulus name - nothing actionable to report.
            continue
        trials[name].append(grade)

failed_count = 0
flaky_count = 0
case_data = {}
detail_lines = []

for name in sorted(trials):
    grades = trials[name]
    total = len(grades)
    trial_statuses = [trial_passed(grade) for grade in grades]
    passed_trials = sum(1 for passed in trial_statuses if passed)
    passed = passed_trials > 0
    flaky = 0 < passed_trials < total
    case_id = name.split("-")[0]

    # The tool-calls grader is considered satisfied if it passed in any trial.
    tool_calls_passed = None
    for grade in grades:
        for detail in grade.get("details", []):
            if detail.get("name", detail.get("type", "?")) == "tool-calls":
                detail_passed = detail.get("passed", True)
                tool_calls_passed = detail_passed if tool_calls_passed is None else (tool_calls_passed or detail_passed)

    case_data[case_id] = {
        "passed": passed,
        "flaky": flaky,
        "passed_trials": passed_trials,
        "total": total,
        "tool_calls_passed": tool_calls_passed,
        "trial_statuses": trial_statuses,
    }

    if flaky:
        flaky_count += 1
        detail_lines.append(f"[FLAKY] {name} (passed {passed_trials}/{total} trials)")

    if passed:
        continue

    # Hard failure: every trial failed. Report evidence from the first trial
    # with grade details; no-grade trials are still represented in the summary.
    failed_count += 1
    detail_lines.append(f"[FAILED] {name} (passed {passed_trials}/{total} trials)")
    first_grade_with_details = next((grade for grade in grades if grade.get("details")), None)
    if first_grade_with_details:
        for detail in first_grade_with_details.get("details", []):
            detail_lines.extend(report_grader_failure(detail))
    else:
        detail_lines.append("  - no grade result - stimulus errored or timed out")

# Per-trial pass rate: for trial index t, how many stimuli passed that trial.
# The i-th record of a stimulus is its i-th trial when Vally writes one record
# per trial.
max_trials = max((len(grades) for grades in trials.values()), default=0)
trial_passed_counts = [0] * max_trials
trial_total_counts = [0] * max_trials
for grades in trials.values():
    for trial_index, grade in enumerate(grades):
        trial_total_counts[trial_index] += 1
        if trial_passed(grade):
            trial_passed_counts[trial_index] += 1

total_cases = len(case_data)
total_passed = sum(1 for result in case_data.values() if result["passed"])
fail_label = "both" if max_trials == 2 else "all"

print(f"\n--- Suite $SUITE_NAME summary ---")
for trial_index in range(max_trials):
    print(f"trial {trial_index + 1} ({trial_passed_counts[trial_index]}/{trial_total_counts[trial_index]})")
print(f"fail in {fail_label} trials: {failed_count}")

pass_rate = total_passed * 100 // total_cases if total_cases else 0
print(f"Effective pass rate (pass@k): {total_passed}/{total_cases} ({pass_rate}%), {flaky_count} flaky")
for case_id in sorted(case_data.keys()):
    result = case_data[case_id]
    status = "PASS" if result["passed"] else "FAIL"
    trial_marks = " ".join(
        f"trial{index + 1}:{'PASS' if passed else 'FAIL'}"
        for index, passed in enumerate(result["trial_statuses"])
    )
    tool_calls_passed = result.get("tool_calls_passed")
    tool_calls_mark = "tool-calls:pass" if tool_calls_passed else "tool-calls:fail" if tool_calls_passed is not None else ""
    print(f"  {case_id} [{status}] {trial_marks} {tool_calls_mark}".rstrip())

if detail_lines:
    print(f"\n--- Suite $SUITE_NAME flaky/failed details ---")
    for line in detail_lines:
        print(line)

if failed_count > 0:
    print(f"\n##vso[task.logissue type=error]Suite $SUITE_NAME: {failed_count} stimulus/stimuli failed all trials (effective pass rate pass@k: {total_passed}/{total_cases})")
else:
    print(f"Suite $SUITE_NAME: all stimuli passed within trials ({total_passed}/{total_cases}, {flaky_count} flaky)")
EOF
