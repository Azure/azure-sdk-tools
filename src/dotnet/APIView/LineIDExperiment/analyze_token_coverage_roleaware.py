#!/usr/bin/env python3
"""
Role-aware APIView LineId coverage analyzer.

This script intentionally uses a different parsing/fingerprinting strategy from
analyze_token_coverage.py. Instead of using a plain text-chain ID, it assigns a
structural role to each line and builds role-specific fingerprints.

Roles:
- doc: documentation/comment lines
- attribute: decorator/annotation lines
- signature: declaration/callable lines (overload-sensitive)
- other: fallback

Usage:
  python analyze_token_coverage_roleaware.py <file_or_directory> [--results-md Analysis.RoleAware.md]
"""

from __future__ import annotations

import argparse
import glob
import json
import os
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from typing import Iterable


COMMENT_CLASS_HINTS = {
    "comment",
    "javadoc",
    "doc",
    "documentation",
}

SIGNATURE_CLASS_HINTS = {
    "methodname",
    "membername",
    "typename",
    "parametertype",
    "parametername",
    "class",
    "interface",
    "enum",
    "struct",
    "function",
    "constructor",
    "returntype",
}


def normalize_spaces(s: str) -> str:
    return re.sub(r"\s+", " ", s.strip())


def normalize_id_part(s: str) -> str:
    s = normalize_spaces(s)
    return re.sub(r"\s+", "_", s)


def render_classes(tok: dict) -> set[str]:
    classes = tok.get("RenderClasses") or []
    return {str(c).strip().lower() for c in classes if str(c).strip()}


def line_text(tokens: list[dict]) -> str:
    parts: list[str] = []
    for tok in tokens:
        val = str(tok.get("Value", ""))
        if not val:
            continue
        if tok.get("HasPrefixSpace") and parts:
            parts.append(" ")
        parts.append(val)
        if tok.get("HasSuffixSpace"):
            parts.append(" ")
    return "".join(parts)


def non_punctuation_token_values(tokens: list[dict]) -> list[str]:
    values: list[str] = []
    for tok in tokens:
        kind = tok.get("Kind", 0)
        val = str(tok.get("Value", "")).strip()
        if not val:
            continue
        if kind == 1:  # punctuation
            continue
        values.append(val)
    return values


def is_doc_line(tokens: list[dict]) -> bool:
    for tok in tokens:
        if tok.get("IsDocumentation"):
            return True
        classes = render_classes(tok)
        if classes & COMMENT_CLASS_HINTS:
            return True
    return False


def is_annotation_like(text: str) -> bool:
    stripped = text.lstrip()
    if not stripped:
        return False
    if re.match(r"^[@#]\w", stripped):
        return True
    if re.match(r"^\[[^\]]+\]", stripped):
        return True
    if stripped.startswith("@"):
        return True
    return False


def has_signature_hints(tokens: list[dict], text: str) -> bool:
    for tok in tokens:
        classes = render_classes(tok)
        if classes & SIGNATURE_CLASS_HINTS:
            return True

    # Fallback when render classes are sparse.
    stripped = text.strip()
    if "(" in stripped and ")" in stripped and not stripped.startswith(("//", "/*", "*")):
        return True
    return False


def first_identifier_after_sigil(text: str) -> str:
    stripped = text.lstrip()
    stripped = re.sub(r"^[@#\[]+", "", stripped)
    m = re.search(r"[A-Za-z_][A-Za-z0-9_.-]*", stripped)
    if not m:
        return "attribute"
    return m.group(0)


def extract_callable_name(tokens: list[dict], text: str) -> str:
    for tok in tokens:
        classes = render_classes(tok)
        if "methodname" in classes or "membername" in classes:
            val = str(tok.get("Value", "")).strip()
            if val:
                return val

    # Fallback: identifier before first '('
    m = re.search(r"([A-Za-z_][A-Za-z0-9_]*)\s*\(", text)
    if m:
        return m.group(1)

    # Last identifier fallback
    names = re.findall(r"[A-Za-z_][A-Za-z0-9_]*", text)
    return names[-1] if names else "callable"


def extract_arity_and_type_shape(tokens: list[dict], text: str) -> tuple[int, str]:
    parameter_types: list[str] = []
    for tok in tokens:
        classes = render_classes(tok)
        if "parametertype" in classes or "typename" in classes:
            val = str(tok.get("Value", "")).strip()
            if val:
                parameter_types.append(val)

    # Parse first parameter list from text.
    m = re.search(r"\((.*)\)", text)
    arity = 0
    if m:
        inside = m.group(1).strip()
        if inside:
            # Split commas while tolerating nested generic brackets.
            depth_angle = 0
            depth_round = 0
            count = 1
            for ch in inside:
                if ch == "<":
                    depth_angle += 1
                elif ch == ">":
                    depth_angle = max(0, depth_angle - 1)
                elif ch == "(":
                    depth_round += 1
                elif ch == ")":
                    depth_round = max(0, depth_round - 1)
                elif ch == "," and depth_angle == 0 and depth_round == 0:
                    count += 1
            arity = count

    if parameter_types:
        type_shape = ",".join(parameter_types)
    else:
        # Token-kind profile fallback is language-agnostic and stable enough.
        kinds = [str(tok.get("Kind", 0)) for tok in tokens if str(tok.get("Value", "")).strip()]
        type_shape = "k" + "-".join(kinds[:20])

    return arity, type_shape


def generic_arity_hint(text: str) -> int:
    # Approximate generic arity by counting top-level commas in first <...> block.
    m = re.search(r"<([^<>]*)>", text)
    if not m:
        return 0
    inner = m.group(1).strip()
    if not inner:
        return 0
    return inner.count(",") + 1


def doc_kind(text: str) -> str:
    s = text.strip()
    if s in {"/**", "/*", "///", "//", "*"}:
        return "doc_marker"
    if s == "*/":
        return "doc_close"
    if s.startswith("* @"):
        return "doc_tag"
    if s.startswith(("//", "*", "///", "/*")):
        return "doc_body"
    return "doc_other"


@dataclass
class Entry:
    file: str
    language: str
    package: str
    index: int
    line_id: str
    line_text: str
    related_to_line: str
    owner_anchor: str
    role: str
    base_fingerprint: str
    final_fingerprint: str = ""


def compute_owner_anchor(chain: tuple[str, ...], related_to_line: str) -> str:
    if related_to_line:
        return normalize_id_part(related_to_line)
    for item in reversed(chain):
        item_norm = normalize_id_part(item)
        if item_norm:
            return item_norm
    return "root"


def iter_lines_with_line_id(review_lines: list[dict], parent_chain: tuple[str, ...] = ()) -> Iterable[tuple[dict, tuple[str, ...]]]:
    for line in review_lines:
        tokens = line.get("Tokens") or []
        text = line_text(tokens)
        line_id = str(line.get("LineId") or "")
        children = line.get("Children") or []

        if line_id:
            chain = parent_chain + (text,)
            yield line, chain
            if children:
                yield from iter_lines_with_line_id(children, chain)
        else:
            if children:
                yield from iter_lines_with_line_id(children, parent_chain)


def role_for_line(tokens: list[dict], text: str, related_to_line: str) -> str:
    if is_doc_line(tokens):
        return "doc"
    if related_to_line and is_annotation_like(text):
        return "attribute"
    if has_signature_hints(tokens, text):
        return "signature"
    return "other"


def build_base_fingerprint(role: str, owner_anchor: str, text: str, tokens: list[dict]) -> str:
    if role == "doc":
        return f"DOC|{owner_anchor}|{doc_kind(text)}"

    if role == "attribute":
        attr_name = first_identifier_after_sigil(text)
        return f"ATTR|{owner_anchor}|{normalize_id_part(attr_name)}"

    if role == "signature":
        callable_name = extract_callable_name(tokens, text)
        arity, type_shape = extract_arity_and_type_shape(tokens, text)
        g_arity = generic_arity_hint(text)
        return (
            f"SIG|{owner_anchor}|{normalize_id_part(callable_name)}"
            f"|a{arity}|g{g_arity}|t{normalize_id_part(type_shape)}"
        )

    payload_tokens = non_punctuation_token_values(tokens)
    payload = normalize_id_part(" ".join(payload_tokens) if payload_tokens else text)
    if not payload:
        return "_unreachable"
    return f"OTHER|{owner_anchor}|{payload}"


def classify_base_collision(entry: Entry) -> str:
    if entry.role == "doc":
        return "doc-comment"
    if entry.role == "attribute":
        return "decorator-annotation"
    if entry.role == "signature":
        return "overload-signature"
    if entry.base_fingerprint == "_unreachable":
        return "empty-or-whitespace"
    return "other"


def analyze_file(path: str) -> dict | None:
    try:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
    except Exception as ex:
        print(f"  ERROR: failed to parse {path}: {ex}", file=sys.stderr)
        return None

    review_lines = data.get("ReviewLines") or []
    if not review_lines:
        return None

    language = str(data.get("Language") or "Unknown")
    package = str(data.get("PackageName") or "Unknown")

    entries: list[Entry] = []
    for i, (line, chain) in enumerate(iter_lines_with_line_id(review_lines)):
        tokens = line.get("Tokens") or []
        text = line_text(tokens)
        line_id = str(line.get("LineId") or "")
        related_to_line = str(line.get("RelatedToLine") or "")
        owner = compute_owner_anchor(chain[:-1], related_to_line)
        role = role_for_line(tokens, text, related_to_line)
        base = build_base_fingerprint(role, owner, text, tokens)

        entries.append(
            Entry(
                file=os.path.basename(path),
                language=language,
                package=package,
                index=i,
                line_id=line_id,
                line_text=text,
                related_to_line=related_to_line,
                owner_anchor=owner,
                role=role,
                base_fingerprint=base,
            )
        )

    if not entries:
        return None

    # Role-aware ordinalization to disambiguate repeated docs/attributes tied to same owner.
    doc_owner_counter: defaultdict[str, int] = defaultdict(int)
    attr_owner_counter: defaultdict[tuple[str, str], int] = defaultdict(int)

    for entry in entries:
        if entry.role == "doc":
            doc_owner_counter[entry.owner_anchor] += 1
            entry.final_fingerprint = f"{entry.base_fingerprint}|d{doc_owner_counter[entry.owner_anchor]}"
        elif entry.role == "attribute":
            # Keep deterministic ordinal per owner+base attribute.
            key = (entry.owner_anchor, entry.base_fingerprint)
            attr_owner_counter[key] += 1
            entry.final_fingerprint = f"{entry.base_fingerprint}|n{attr_owner_counter[key]}"
        else:
            entry.final_fingerprint = entry.base_fingerprint

    # Final backoff ordinal for any remaining collisions in a file.
    final_counts = Counter(e.final_fingerprint for e in entries)
    final_seen: defaultdict[str, int] = defaultdict(int)
    for entry in entries:
        if final_counts[entry.final_fingerprint] > 1:
            final_seen[entry.final_fingerprint] += 1
            entry.final_fingerprint = f"{entry.final_fingerprint}|x{final_seen[entry.final_fingerprint]}"

    base_counts = Counter(e.base_fingerprint for e in entries)
    final_counts = Counter(e.final_fingerprint for e in entries)

    base_unique = 0
    base_nonunique = 0
    unreachable = 0
    final_unique = 0
    final_nonunique = 0

    class_counts = Counter()
    examples_by_class: defaultdict[str, list[Entry]] = defaultdict(list)

    for entry in entries:
        if entry.base_fingerprint == "_unreachable":
            unreachable += 1
        elif base_counts[entry.base_fingerprint] == 1:
            base_unique += 1
        else:
            base_nonunique += 1
            cls = classify_base_collision(entry)
            class_counts[cls] += 1
            if len(examples_by_class[cls]) < 3:
                examples_by_class[cls].append(entry)

        if final_counts[entry.final_fingerprint] == 1:
            final_unique += 1
        else:
            final_nonunique += 1

    total = len(entries)

    return {
        "file": os.path.basename(path),
        "filepath": path,
        "language": language,
        "package": package,
        "total": total,
        "base_unique": base_unique,
        "base_nonunique": base_nonunique,
        "unreachable": unreachable,
        "final_unique": final_unique,
        "final_nonunique": final_nonunique,
        "class_counts": dict(class_counts),
        "examples_by_class": {
            k: [
                {
                    "line_id": e.line_id,
                    "line_text": normalize_spaces(e.line_text)[:160],
                    "owner": e.owner_anchor[:120],
                    "base": e.base_fingerprint[:180],
                }
                for e in v
            ]
            for k, v in examples_by_class.items()
        },
    }


def gather_files(inputs: list[str]) -> list[str]:
    files: list[str] = []
    for p in inputs:
        if any(ch in p for ch in "*?[]"):
            for candidate in glob.glob(p, recursive=True):
                if os.path.isfile(candidate) and candidate.lower().endswith(".json"):
                    files.append(os.path.abspath(candidate))
            continue

        abs_p = os.path.abspath(p)
        if os.path.isfile(abs_p):
            if abs_p.lower().endswith(".json"):
                files.append(abs_p)
            continue

        if os.path.isdir(abs_p):
            for root, _, names in os.walk(abs_p):
                for name in names:
                    if name.lower().endswith(".json"):
                        files.append(os.path.abspath(os.path.join(root, name)))

    files = sorted(set(files))
    return files


def write_markdown(stats: list[dict], source_paths: list[str], output_path: str) -> None:
    langs: defaultdict[str, list[dict]] = defaultdict(list)
    for s in stats:
        langs[s["language"]].append(s)

    total = sum(s["total"] for s in stats)
    base_unique = sum(s["base_unique"] for s in stats)
    base_nonunique = sum(s["base_nonunique"] for s in stats)
    unreachable = sum(s["unreachable"] for s in stats)
    final_unique = sum(s["final_unique"] for s in stats)
    final_nonunique = sum(s["final_nonunique"] for s in stats)

    all_class_counts = Counter()
    for s in stats:
        all_class_counts.update(s.get("class_counts", {}))

    lines: list[str] = []
    lines.append("# Role-Aware Token Coverage Results")
    lines.append("")
    lines.append(f"Input paths: {', '.join(source_paths)}")
    lines.append(f"Analyzed files: {len(stats)}")
    lines.append("")
    lines.append("## Methodology")
    lines.append("")
    lines.append(
        "This report uses a role-aware parser that classifies each `LineId` line as `doc`, "
        "`attribute`, `signature`, or `other`, then computes role-specific fingerprints. "
        "Signatures include overload-sensitive shape (`name`, arity, generic arity, and type profile). "
        "Docs and attributes are stabilized with deterministic owner-scoped ordinals. "
        "A final collision backoff ordinal is only applied when a collision still remains."
    )
    lines.append("")
    lines.append("## Executive Summary")
    lines.append("")
    if total:
        lines.append(f"- Total line IDs: {total}")
        lines.append(f"- Base unique reachable: {base_unique} ({base_unique / total * 100:.1f}%)")
        lines.append(f"- Base non-unique reachable: {base_nonunique} ({base_nonunique / total * 100:.1f}%)")
        lines.append(f"- Unreachable: {unreachable} ({unreachable / total * 100:.1f}%)")
        lines.append(f"- Final unique (after role + backoff): {final_unique} ({final_unique / total * 100:.1f}%)")
        lines.append(f"- Final non-unique (after role + backoff): {final_nonunique} ({final_nonunique / total * 100:.1f}%)")
    else:
        lines.append("- No lines analyzed.")
    lines.append("")

    lines.append("## Issue Class Coverage")
    lines.append("")
    lines.append("| Class | Base Collision Count | Share Of Base Non-Unique |")
    lines.append("|---|---:|---:|")
    denom = max(base_nonunique, 1)
    for cls in ["overload-signature", "doc-comment", "decorator-annotation"]:
        count = all_class_counts.get(cls, 0)
        lines.append(f"| {cls} | {count} | {count / denom * 100:.1f}% |")
    lines.append("")

    lines.append("## Per-Language")
    lines.append("")
    lines.append("| Language | Files | Avg Line IDs | Base Non-Unique% | Final Non-Unique% |")
    lines.append("|---|---:|---:|---:|---:|")
    for lang in sorted(langs.keys()):
        items = langs[lang]
        n = len(items)
        avg_total = sum(s["total"] for s in items) / n
        lang_total = sum(s["total"] for s in items)
        lang_base_non = sum(s["base_nonunique"] for s in items)
        lang_final_non = sum(s["final_nonunique"] for s in items)
        base_pct = (lang_base_non / lang_total * 100) if lang_total else 0
        final_pct = (lang_final_non / lang_total * 100) if lang_total else 0
        lines.append(f"| {lang} | {n} | {avg_total:.0f} | {base_pct:.1f}% | {final_pct:.1f}% |")
    lines.append("")

    lines.append("## Key Collision Examples (Base)")
    lines.append("")
    for cls in ["overload-signature", "doc-comment", "decorator-annotation"]:
        lines.append(f"### {cls}")
        lines.append("")
        lines.append("| Language | Existing LineId | Base Fingerprint | Original Line |")
        lines.append("|---|---|---|---|")
        rows = []
        for s in stats:
            for ex in s.get("examples_by_class", {}).get(cls, []):
                rows.append((s["language"], ex))
        if not rows:
            lines.append("| n/a | n/a | n/a | n/a |")
        else:
            for lang, ex in rows[:10]:
                lid = (ex.get("line_id") or "").replace("|", "\\|")
                base = (ex.get("base") or "").replace("|", "\\|")
                text = (ex.get("line_text") or "").replace("|", "\\|")
                lines.append(f"| {lang} | {lid} | {base} | {text} |")
        lines.append("")

    with open(output_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))


def print_console_summary(stats: list[dict]) -> None:
    total = sum(s["total"] for s in stats)
    base_nonunique = sum(s["base_nonunique"] for s in stats)
    final_nonunique = sum(s["final_nonunique"] for s in stats)
    unreachable = sum(s["unreachable"] for s in stats)

    print("\nRole-aware summary")
    print("=" * 80)
    print(f"Files analyzed: {len(stats)}")
    print(f"Total line IDs: {total}")
    if total:
        print(f"Base non-unique: {base_nonunique} ({base_nonunique / total * 100:.1f}%)")
        print(f"Final non-unique: {final_nonunique} ({final_nonunique / total * 100:.1f}%)")
        print(f"Unreachable: {unreachable} ({unreachable / total * 100:.1f}%)")


def main() -> int:
    parser = argparse.ArgumentParser(description="Analyze APIView token files with role-aware heuristics")
    parser.add_argument("path", nargs="+", help="Token file(s), glob(s), or folder(s) to analyze")
    parser.add_argument(
        "--results-md",
        default="Analysis.RoleAware.md",
        help="Write markdown report to this path (default: Analysis.RoleAware.md)",
    )
    args = parser.parse_args()

    files = gather_files(args.path)
    if not files:
        print("No JSON files found.", file=sys.stderr)
        return 2

    stats: list[dict] = []
    for fpath in files:
        s = analyze_file(fpath)
        if s:
            stats.append(s)

    if not stats:
        print("No analyzable files with ReviewLines + LineId.", file=sys.stderr)
        return 3

    write_markdown(stats, args.path, args.results_md)
    print_console_summary(stats)
    print(f"\nWrote markdown report: {os.path.abspath(args.results_md)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
