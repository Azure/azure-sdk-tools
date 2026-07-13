"""Score an evaluation run against the ground-truth baseline.

Reads baseline.json (conversation_id -> verdict) and a directory of per-channel
evaluation JSON files (the same format the pipeline publishes, e.g. the
`channel-conversation-evaluation` artifact). Reports overall accuracy, a
per-verdict confusion matrix, and lists every mismatch so regressions are easy
to spot.

Usage:
    python eval_baseline/score_baseline.py --run eval_baseline/artifact/dl
    python eval_baseline/score_baseline.py --run <dir> --baseline eval_baseline/baseline.json
    python eval_baseline/score_baseline.py --run <dir> --min-accuracy 0.8 --summary-json out.json

Exit codes:
    0  scoring passed (or no baseline conversations were present in the run).
    1  accuracy fell below --min-accuracy for the conversations that overlapped.
"""

import argparse
import json
import sys
from collections import defaultdict
from pathlib import Path

_DIR = Path(__file__).resolve().parent
_DEFAULT_BASELINE = _DIR / "baseline.json"
_VERDICTS = ("correct", "incorrect", "unknown")


def _load_baseline(path: Path) -> dict[str, str]:
    data = json.loads(path.read_text(encoding="utf-8"))
    return {entry["conversation_id"]: entry["verdict"] for entry in data}


def _load_run(run_dir: Path) -> dict[str, str]:
    verdicts: dict[str, str] = {}
    for f in sorted(run_dir.glob("*.json")):
        if f.name in ("index.json", "baseline.json", "baseline_score.json"):
            continue
        group = json.loads(f.read_text(encoding="utf-8"))
        for conv in group.get("conversations", []):
            verdicts[conv["conversation_id"]] = conv["verdict"]
    return verdicts


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--run",
        required=True,
        type=Path,
        help="Directory of per-channel evaluation JSON files to score.",
    )
    parser.add_argument(
        "--baseline",
        type=Path,
        default=_DEFAULT_BASELINE,
        help="Path to baseline.json (default: eval_baseline/baseline.json).",
    )
    parser.add_argument(
        "--min-accuracy",
        type=float,
        default=0.0,
        help=(
            "Fail (exit 1) when accuracy over the overlapping conversations "
            "falls below this fraction (0-1). Default 0 = report only."
        ),
    )
    parser.add_argument(
        "--summary-json",
        type=Path,
        default=None,
        help="Optional path to write a machine-readable summary JSON.",
    )
    args = parser.parse_args()

    baseline = _load_baseline(args.baseline)
    run = _load_run(args.run)

    matched = sorted(set(baseline) & set(run))
    missing = sorted(set(baseline) - set(run))  # in baseline, absent from run
    extra = sorted(set(run) - set(baseline))    # in run, absent from baseline

    correct = 0
    # confusion[expected][actual] = count
    confusion: dict[str, dict[str, int]] = {
        e: defaultdict(int) for e in _VERDICTS
    }
    mismatches: list[tuple[str, str, str]] = []
    for cid in matched:
        expected = baseline[cid]
        actual = run[cid]
        confusion.setdefault(expected, defaultdict(int))[actual] += 1
        if expected == actual:
            correct += 1
        else:
            mismatches.append((cid, expected, actual))

    total = len(matched)
    accuracy = correct / total if total else 0.0

    print("=" * 60)
    print("Evaluation accuracy vs. baseline")
    print("=" * 60)
    print(f"Baseline entries : {len(baseline)}")
    print(f"Run entries      : {len(run)}")
    print(f"Scored (matched) : {total}")
    print(f"Correct          : {correct}")
    print(f"Accuracy         : {accuracy:.1%}")
    if missing:
        print(f"In baseline but missing from run : {len(missing)}")
    if extra:
        print(f"In run but not in baseline       : {len(extra)}")

    print("\nPer-verdict (baseline label):")
    print(f"  {'verdict':<10}{'total':>7}{'correct':>9}{'recall':>9}")
    for exp in _VERDICTS:
        row = confusion.get(exp, {})
        exp_total = sum(row.values())
        exp_correct = row.get(exp, 0)
        recall = exp_correct / exp_total if exp_total else 0.0
        print(f"  {exp:<10}{exp_total:>7}{exp_correct:>9}{recall:>8.0%}")

    print("\nConfusion matrix (rows = baseline, cols = run):")
    header = "  " + " " * 12 + "".join(f"{a:>11}" for a in _VERDICTS)
    print(header)
    for exp in _VERDICTS:
        row = confusion.get(exp, {})
        cells = "".join(f"{row.get(a, 0):>11}" for a in _VERDICTS)
        print(f"  {exp:<12}{cells}")

    if mismatches:
        print(f"\nMismatches ({len(mismatches)}):")
        for cid, expected, actual in mismatches:
            print(f"  expected={expected:<9} run={actual:<9} {cid}")

    if missing:
        print(f"\nNot found in run ({len(missing)}):")
        for cid in missing:
            print(f"  {cid}")

    if args.summary_json:
        summary = {
            "baseline_entries": len(baseline),
            "run_entries": len(run),
            "scored": total,
            "correct": correct,
            "accuracy": accuracy,
            "min_accuracy": args.min_accuracy,
            "missing_from_run": len(missing),
            "extra_in_run": len(extra),
            "mismatches": [
                {"conversation_id": cid, "expected": exp, "actual": act}
                for cid, exp, act in mismatches
            ],
        }
        args.summary_json.parent.mkdir(parents=True, exist_ok=True)
        args.summary_json.write_text(
            json.dumps(summary, indent=2) + "\n", encoding="utf-8"
        )
        print(f"\nWrote summary to {args.summary_json}")

    # Gate: only enforce when the run actually overlapped the baseline set,
    # so a rolling window with no baseline conversations is a no-op (pass).
    if total == 0:
        print("\nNo baseline conversations were present in this run; skipping gate.")
        return
    if args.min_accuracy > 0 and accuracy < args.min_accuracy:
        print(
            f"\nFAILED: accuracy {accuracy:.1%} is below the required "
            f"{args.min_accuracy:.1%}."
        )
        sys.exit(1)
    print("\nPASSED baseline accuracy gate.")


if __name__ == "__main__":
    main()
