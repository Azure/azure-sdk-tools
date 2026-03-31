#!/usr/bin/env python3
"""
APIView Token File Line ID Coverage Analyzer

Analyzes APIView token files (ReviewLines format) to determine what percentage of
lines with elementIds (LineIds) can have unique IDs computed purely from the tree
structure -- without any language-specific knowledge or heuristics.

Categories:
  1. Total Line IDs:          Every line in the token file with a LineId
  2. Unique Reachable:        Lines where a unique ID can be derived from tree structure alone
  3. Non-unique Reachable:    Lines where an ID can be derived but it collides with another line
  4. Unreachable:             Lines with a LineId but no calculated ID can be derived

Usage:
  python analyze_token_coverage.py <file_or_directory> [--verbose] [--show-collisions]
  python analyze_token_coverage.py <file> --dump-json [-o output.json]
  python analyze_token_coverage.py <file_or_directory> --results-md Results.md
"""

import json
import sys
import os
import re
import glob
from datetime import datetime, timezone
from collections import defaultdict, Counter
from typing import Optional


def get_line_text(tokens: list) -> str:
    """Concatenate token Values on a line, skipping Keyword (2) and Punctuation (1) tokens."""
    parts = []
    for tok in tokens:
        kind = tok.get("Kind", 0)
        if kind in (1, 2):  # Punctuation, Keyword
            continue
        val = tok.get("Value", "")
        if not val:
            continue
        if tok.get("HasPrefixSpace") and parts:
            parts.append(" ")
        parts.append(val)
        if tok.get("HasSuffixSpace"):
            parts.append(" ")
    return "".join(parts)


def sanitize_id_part(s: str) -> str:
    """Sanitize a line text string to be a safe ID component.
    Purely mechanical — no language-aware interpretation of characters."""
    s = s.strip()
    # Replace whitespace runs with a single underscore
    s = re.sub(r'\s+', '_', s)
    return s


def classify_collision(entry: dict) -> str:
    """Classify a non-unique calculated ID into a coarse collision class."""
    line_text = entry.get("line_text", "").strip()
    calculated = entry.get("calculated_id", "")

    if not line_text:
        return "empty-or-whitespace"

    if re.fullmatch(r"[-+]?\d+(\.\d+)?([eE][-+]?\d+)?", line_text):
        return "literal-numeric"

    if line_text in {"/**", "*/", "*"} or line_text.startswith("*"):
        return "comment-or-doc-boilerplate"

    if line_text.startswith("@"):
        return "annotation-or-attribute"

    if line_text.startswith("package ") or line_text.startswith("namespace ") or line_text.startswith("module "):
        return "namespace-or-module-declaration"

    if "." in calculated and calculated.count(".") >= 2:
        return "path-like-identifier"

    if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", line_text):
        return "short-identifier"

    return "other"


def walk_review_lines(lines: list, parent_text_chain: tuple = (),
                      results: list = None) -> list:
    """
    Recursively walk the ReviewLines tree, collecting every line that has a LineId.

    For each line with a LineId, the calculated ID is the chain of line texts
    from root to that line (parent_text.child_text.grandchild_text).

    Returns a list of dicts.
    """
    if results is None:
        results = []

    for line in lines:
        tokens = line.get("Tokens", [])
        children = line.get("Children", [])
        line_id = line.get("LineId")
        line_text = get_line_text(tokens)

        if line_id is not None and line_id != "":
            current_chain = parent_text_chain + (line_text,)

            results.append({
                "line_id": line_id,
                "line_text": line_text,
                "text_chain": current_chain,
            })

            # Children see this line's text in their chain
            if children:
                walk_review_lines(children, current_chain, results)
        else:
            # Lines without LineId: children inherit the same parent chain
            if children:
                walk_review_lines(children, parent_text_chain, results)

    return results


def compute_calculated_id(entry: dict) -> str:
    """
    Compute a deterministic calculated line ID from the chain of line texts
    in the tree. If a child's text already starts with the parent's ID,
    use the child text directly (no redundant prefix). Otherwise chain with '.'.
    """
    parts = entry["text_chain"]
    if not parts:
        return "_unreachable"

    calculated = sanitize_id_part(parts[0])
    for text in parts[1:]:
        sanitized = sanitize_id_part(text)
        if not sanitized:
            continue
        if sanitized.startswith(calculated):
            # Child already includes parent path — no need to re-prefix
            calculated = sanitized
        else:
            calculated = calculated + "." + sanitized

    return calculated if calculated else "_unreachable"


def analyze_file(filepath: str, verbose: bool = False, show_collisions: bool = False,
                  dump_json: bool = False) -> Optional[dict]:
    """Analyze a single token file and return coverage stats."""
    try:
        with open(filepath, "r", encoding="utf-8") as f:
            data = json.load(f)
    except (json.JSONDecodeError, OSError) as e:
        print(f"  ERROR: Could not parse {filepath}: {e}", file=sys.stderr)
        return None

    if not isinstance(data, dict):
        print(f"  SKIP: {filepath} JSON root is not an object", file=sys.stderr)
        return None

    language = data.get("Language", "Unknown")
    package = data.get("PackageName", "Unknown")
    review_lines = data.get("ReviewLines", [])

    if not review_lines:
        # Check for legacy Tokens format
        tokens = data.get("Tokens", [])
        if tokens:
            print(f"  SKIP: {filepath} uses legacy flat Tokens format (not ReviewLines)", file=sys.stderr)
            return None
        print(f"  SKIP: {filepath} has no ReviewLines or Tokens", file=sys.stderr)
        return None

    # Walk the tree and collect all lines with LineIds
    all_lines = walk_review_lines(review_lines)

    total_line_ids = len(all_lines)
    if total_line_ids == 0:
        print(f"  SKIP: {filepath} has ReviewLines but no lines with LineId", file=sys.stderr)
        return None

    # Check existing LineId uniqueness
    existing_id_counts = Counter(entry["line_id"] for entry in all_lines)

    # Compute calculated IDs for all lines
    for entry in all_lines:
        entry["calculated_id"] = compute_calculated_id(entry)

    # Check calculated ID uniqueness
    calculated_id_counts = Counter(entry["calculated_id"] for entry in all_lines)

    # Handle dump_json mode
    if dump_json:
        json_output = []
        line_id_seen = defaultdict(int)
        for entry in all_lines:
            # Index of this occurrence for the given LineId in traversal order.
            line_id_index = line_id_seen[entry["line_id"]]
            line_id_seen[entry["line_id"]] += 1
            json_output.append({
                "existing": entry["line_id"],
                "lineIdIndex": line_id_index,
                "existingUnique": existing_id_counts[entry["line_id"]] == 1,
                "calculated": entry["calculated_id"],
                "calculatedUnique": calculated_id_counts[entry["calculated_id"]] == 1,
            })
        return {"_dump_json": json_output}

    # Categorize based on calculated IDs
    unique_reachable = []       # calculated ID is unique
    nonunique_reachable = []    # calculated ID collides with another line
    unreachable = []            # no calculated ID could be derived

    for entry in all_lines:
        cid = entry["calculated_id"]
        if cid == "_unreachable":
            unreachable.append(entry)
        elif calculated_id_counts[cid] == 1:
            unique_reachable.append(entry)
        else:
            nonunique_reachable.append(entry)

    nonunique_class_counts = Counter()
    for entry in nonunique_reachable:
        collision_class = classify_collision(entry)
        entry["collision_class"] = collision_class
        nonunique_class_counts[collision_class] += 1

    collision_groups = defaultdict(list)
    for entry in nonunique_reachable:
        collision_groups[entry["calculated_id"]].append(entry)

    key_examples = []
    for cid, lines in sorted(collision_groups.items(), key=lambda x: (-len(x[1]), x[0]))[:5]:
        sample = lines[0]
        key_examples.append({
            "calculated_id": cid,
            "group_size": len(lines),
            "collision_class": sample.get("collision_class", "other"),
            "line_text": sample.get("line_text", "")[:120],
            "line_id": sample.get("line_id", ""),
        })

    stats = {
        "filepath": filepath,
        "file": os.path.basename(filepath),
        "language": language,
        "package": package,
        "total_line_ids": total_line_ids,
        "unique_reachable": len(unique_reachable),
        "nonunique_reachable": len(nonunique_reachable),
        "unreachable": len(unreachable),
        "unique_pct": len(unique_reachable) / total_line_ids * 100,
        "nonunique_pct": len(nonunique_reachable) / total_line_ids * 100,
        "unreachable_pct": len(unreachable) / total_line_ids * 100,
        "nonunique_class_counts": dict(nonunique_class_counts),
        "key_examples": key_examples,
    }

    if verbose or show_collisions:
        if collision_groups and show_collisions:
            print(f"\n  Collisions ({len(collision_groups)} groups):")
            for cid, lines in sorted(collision_groups.items(), key=lambda x: -len(x[1])):
                print(f"    [{len(lines)} lines] Calculated ID: {cid}")
                for e in lines:
                    print(f"      LineId: {e['line_id']}")
                    if verbose:
                        print(f"        Text: {e['line_text'][:100]}")

        if unreachable and show_collisions:
            print(f"\n  Unreachable lines ({len(unreachable)}):")
            for e in unreachable[:20]:
                print(f"    LineId: {e['line_id']}")

    return stats


def write_results_markdown(all_stats: list, source_paths: list, output_path: str):
    """Write a markdown results report with class and example breakdowns."""
    generated_utc = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%SZ")

    total_files = len(all_stats)
    total_line_ids = sum(s["total_line_ids"] for s in all_stats)
    total_unique = sum(s["unique_reachable"] for s in all_stats)
    total_nonunique = sum(s["nonunique_reachable"] for s in all_stats)
    total_unreachable = sum(s["unreachable"] for s in all_stats)

    langs = defaultdict(list)
    for s in all_stats:
        langs[s["language"]].append(s)

    lines = []
    lines.append("# Token Coverage Results")
    lines.append("")
    lines.append(f"Generated (UTC): {generated_utc}")
    lines.append(f"Input paths: {', '.join(source_paths)}")
    lines.append(f"Analyzed files: {total_files}")
    lines.append("")

    lines.append("## Executive Summary")
    lines.append("")
    if total_line_ids > 0:
        lines.append(f"- Total line IDs: {total_line_ids}")
        lines.append(f"- Unique reachable: {total_unique} ({total_unique/total_line_ids*100:.1f}%)")
        lines.append(f"- Non-unique reachable: {total_nonunique} ({total_nonunique/total_line_ids*100:.1f}%)")
        lines.append(f"- Unreachable: {total_unreachable} ({total_unreachable/total_line_ids*100:.1f}%)")
    else:
        lines.append("- No line IDs were analyzed.")
    lines.append("")

    lines.append("## Per-Language Averages")
    lines.append("")
    lines.append("| Language | Files | Avg Unique% | Avg Non-Unique% | Avg Unreachable% | Weighted Non-Unique% |")
    lines.append("|---|---:|---:|---:|---:|---:|")
    for lang in sorted(langs.keys()):
        items = langs[lang]
        n = len(items)
        avg_unique = sum(s["unique_pct"] for s in items) / n
        avg_nonunique = sum(s["nonunique_pct"] for s in items) / n
        avg_unreachable = sum(s["unreachable_pct"] for s in items) / n
        lang_total = sum(s["total_line_ids"] for s in items)
        lang_nonunique = sum(s["nonunique_reachable"] for s in items)
        weighted_nonunique = (lang_nonunique / lang_total * 100) if lang_total else 0.0
        lines.append(
            f"| {lang} | {n} | {avg_unique:.1f}% | {avg_nonunique:.1f}% | {avg_unreachable:.1f}% | {weighted_nonunique:.1f}% |"
        )
    lines.append("")

    lines.append("## Collision Classes By Language")
    lines.append("")
    for lang in sorted(langs.keys()):
        items = langs[lang]
        class_counts = Counter()
        nonunique_total = sum(s["nonunique_reachable"] for s in items)
        for s in items:
            class_counts.update(s.get("nonunique_class_counts", {}))

        lines.append(f"### {lang}")
        lines.append("")
        if nonunique_total == 0:
            lines.append("No non-unique collisions found.")
            lines.append("")
            continue

        lines.append("| Collision Class | Count | Share Of Language Non-Unique |")
        lines.append("|---|---:|---:|")
        for cls, count in class_counts.most_common(8):
            pct = count / nonunique_total * 100
            lines.append(f"| {cls} | {count} | {pct:.1f}% |")
        lines.append("")

        lang_examples = []
        for s in items:
            for ex in s.get("key_examples", []):
                lang_examples.append({
                    "file": s["file"],
                    **ex,
                })
        lang_examples.sort(key=lambda e: (-e["group_size"], e["calculated_id"]))

        lines.append("Key examples:")
        lines.append("")
        lines.append("| Group Size | Class | Calculated ID | File | Example Line Text |")
        lines.append("|---:|---|---|---|---|")
        for ex in lang_examples[:5]:
            cid = ex["calculated_id"].replace("|", "\\|")
            text = (ex.get("line_text", "") or "").replace("|", "\\|")
            lines.append(
                f"| {ex['group_size']} | {ex['collision_class']} | {cid} | {ex['file']} | {text} |"
            )
        lines.append("")

    lines.append("## Visualization Ideas")
    lines.append("")
    lines.append("- Stacked bars per language: unique vs non-unique vs unreachable percentages.")
    lines.append("- Pareto chart of collision classes per language.")
    lines.append("- Heatmap: language x collision class, normalized as collisions per 1k line IDs.")
    lines.append("")

    lines.append("## Notes")
    lines.append("")
    lines.append("- Collision classes are heuristic and language-agnostic to preserve the no-language-specific constraint.")
    lines.append("- Use this report to prioritize algorithm improvements by class frequency and blast radius.")
    lines.append("")

    with open(output_path, "w", encoding="utf-8") as out:
        out.write("\n".join(lines))


def print_stats_table(all_stats: list):
    """Print a formatted table of stats across all files."""
    if not all_stats:
        print("\nNo files analyzed.")
        return

    # Header
    print("\n" + "=" * 120)
    print(f"{'File':<45} {'Lang':<10} {'Total':>7} {'Unique':>10} {'%':>7} "
          f"{'NonUniq':>10} {'%':>7} {'Unreach':>10} {'%':>7}")
    print("-" * 120)

    # Sort by language then package
    all_stats.sort(key=lambda s: (s["language"], s["package"]))

    for s in all_stats:
        fname = s["file"]
        if len(fname) > 44:
            fname = fname[:41] + "..."
        print(f"{fname:<45} {s['language']:<10} {s['total_line_ids']:>7} "
              f"{s['unique_reachable']:>10} {s['unique_pct']:>6.1f}% "
              f"{s['nonunique_reachable']:>10} {s['nonunique_pct']:>6.1f}% "
              f"{s['unreachable']:>10} {s['unreachable_pct']:>6.1f}%")

    print("-" * 120)

    # Totals
    total = sum(s["total_line_ids"] for s in all_stats)
    unique = sum(s["unique_reachable"] for s in all_stats)
    nonuniq = sum(s["nonunique_reachable"] for s in all_stats)
    unreach = sum(s["unreachable"] for s in all_stats)

    if total > 0:
        print(f"{'TOTAL':<45} {'':10} {total:>7} "
              f"{unique:>10} {unique/total*100:>6.1f}% "
              f"{nonuniq:>10} {nonuniq/total*100:>6.1f}% "
              f"{unreach:>10} {unreach/total*100:>6.1f}%")

    # Averages per file
    n = len(all_stats)
    avg_unique = sum(s["unique_pct"] for s in all_stats) / n
    avg_nonuniq = sum(s["nonunique_pct"] for s in all_stats) / n
    avg_unreach = sum(s["unreachable_pct"] for s in all_stats) / n
    print(f"{'AVG (per file)':<45} {'':10} {'':>7} "
          f"{'':>10} {avg_unique:>6.1f}% "
          f"{'':>10} {avg_nonuniq:>6.1f}% "
          f"{'':>10} {avg_unreach:>6.1f}%")

    print("=" * 120)

    # Per-language breakdown
    langs = defaultdict(list)
    for s in all_stats:
        langs[s["language"]].append(s)

    if len(langs) > 1:
        print("\nPer-Language Averages:")
        print(f"  {'Language':<15} {'Files':>5} {'Avg Unique%':>12} {'Avg NonUniq%':>13} {'Avg Unreach%':>13}")
        print(f"  {'-'*60}")
        for lang in sorted(langs.keys()):
            stats_for_lang = langs[lang]
            n_l = len(stats_for_lang)
            avg_u = sum(s["unique_pct"] for s in stats_for_lang) / n_l
            avg_n = sum(s["nonunique_pct"] for s in stats_for_lang) / n_l
            avg_r = sum(s["unreachable_pct"] for s in stats_for_lang) / n_l
            print(f"  {lang:<15} {n_l:>5} {avg_u:>11.1f}% {avg_n:>12.1f}% {avg_r:>12.1f}%")


def main():
    import argparse
    parser = argparse.ArgumentParser(description="Analyze APIView token file line ID coverage")
    parser.add_argument("path", nargs="+", help="Token file(s) or directory to analyze. Supports glob patterns.")
    parser.add_argument("--verbose", "-v", action="store_true", help="Show detailed collision info")
    parser.add_argument("--show-collisions", "-c", action="store_true",
                        help="Show which lines collide (non-unique reachable)")
    parser.add_argument("--dump-json", "-d", action="store_true",
                        help="Output JSON array mapping each existing LineId to its calculated ID")
    parser.add_argument("-o", "--output", type=str, default=None,
                        help="Output file for --dump-json (default: stdout)")
    parser.add_argument("--results-md", type=str, default=None,
                        help="Write a markdown results report across analyzed files")
    args = parser.parse_args()

    files = []
    for p in args.path:
        if os.path.isdir(p):
            files.extend(glob.glob(os.path.join(p, "**", "*.json"), recursive=True))
        elif os.path.isfile(p):
            files.append(p)
        else:
            # Try as glob
            matched = glob.glob(p, recursive=True)
            files.extend(matched)

    if not files:
        print(f"No files found for: {args.path}", file=sys.stderr)
        sys.exit(1)

    files = sorted(set(files))

    if args.dump_json:
        # Dump mode: process single file (or first file) and output JSON
        if len(files) > 1:
            print(f"Warning: --dump-json works best with a single file. Using first: {files[0]}",
                  file=sys.stderr)
        result = analyze_file(files[0], dump_json=True)
        if result and "_dump_json" in result:
            json_str = json.dumps(result["_dump_json"], indent=2)
            if args.output:
                with open(args.output, "w", encoding="utf-8") as out:
                    out.write(json_str)
                print(f"Wrote {len(result['_dump_json'])} entries to {args.output}", file=sys.stderr)
            else:
                print(json_str)
        else:
            print("No data to dump.", file=sys.stderr)
            sys.exit(1)
        return

    print(f"Analyzing {len(files)} file(s)...\n")

    all_stats = []
    for f in files:
        print(f"  Processing: {os.path.basename(f)}")
        stats = analyze_file(f, verbose=args.verbose, show_collisions=args.show_collisions)
        if stats:
            all_stats.append(stats)

    print_stats_table(all_stats)

    if args.results_md:
        write_results_markdown(all_stats, args.path, args.results_md)
        print(f"\nWrote results report: {args.results_md}")


if __name__ == "__main__":
    main()