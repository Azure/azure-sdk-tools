"""Extract historical Teams channel posts from markdown into ConversationMessageItem JSON.

Parses each .md file in ``historical_messages/``, extracts threads (## sections),
maps to ``ConversationMessageItem`` schema, detects bot redirect hints
("Not resolved? Please re-post in [Channel]"), and writes per-tenant JSON files.

Usage:
    python scripts/extract_historical_messages.py
    python scripts/extract_historical_messages.py --dry-run   # parse only, no write
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import unquote, urlparse, parse_qs

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------

_SCRIPT_DIR = Path(__file__).resolve().parent
_HIST_DIR = _SCRIPT_DIR.parent.parent / "historical_messages"
_JSON_DIR = _HIST_DIR / "json"

# ---------------------------------------------------------------------------
# Channel name → tenant_id mapping (for "Not resolved?" redirects)
# ---------------------------------------------------------------------------

_CHANNEL_TO_TENANT: dict[str, str] = {
    "Azure SDK Onboarding": "azure_sdk_onboarding",
    "API Spec Review": "api_spec_review_bot",
    "TypeSpec Discussion": "azure_sdk_qa_bot",
    "General": "general_qa_bot",
    "Language – JS ＆ TS 🥷": "javascript_channel_qa_bot",
    "Language - Java": "java_channel_qa_bot",
    "Language - Go": "golang_channel_qa_bot",
    "Language - Python": "python_channel_qa_bot",
    "Language - .NET": "dotnet_channel_qa_bot",
}

BOT_NAME = "Azure SDK Q&A Bot"

# ---------------------------------------------------------------------------
# Regex patterns
# ---------------------------------------------------------------------------

_TENANT_ID_RE = re.compile(r"^tenant_id:\s*(.+)$", re.MULTILINE)
_HEADER_RE = re.compile(
    r"\*\*From:\*\*\s*(.+?)[\s　]+\*\*Time:\*\*\s*(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2})"
)
_VIEW_POST_RE = re.compile(r"\[View post\]\(([^)]+)\)")
_REPLY_RE = re.compile(
    r"^>\s*\*\*(.+?)\*\*\s*·\s*(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2})\s*$"
)
_REDIRECT_RE = re.compile(
    r"Not resolved\? Please re-post in the.*?\[([^\]]+)\]"
)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _parse_datetime(s: str) -> str:
    """Parse 'YYYY-MM-DD HH:MM' → ISO 8601 UTC string."""
    dt = datetime.strptime(s.strip(), "%Y-%m-%d %H:%M").replace(tzinfo=timezone.utc)
    return dt.isoformat()


def _make_id(seed: str) -> str:
    """Deterministic short ID from seed string."""
    return "msg-" + hashlib.sha256(seed.encode()).hexdigest()[:12]


def _make_sender_id(name: str) -> str:
    if name == BOT_NAME:
        return "bot_azure_sdk_qa"
    sanitized = re.sub(r"[^A-Za-z0-9_-]", "", name.replace(" ", "_"))
    return f"user_{sanitized}"


def _extract_from_url(url: str) -> tuple[str, str]:
    """Extract (channel_id, conversation_id) from Teams URL."""
    parsed = urlparse(url)
    path_parts = unquote(parsed.path).split("/")
    channel_id = ""
    for part in path_parts:
        if part.startswith("19:") or part.startswith("19%3A"):
            channel_id = unquote(part)
            break

    qs = parse_qs(parsed.query)
    conversation_id = ""
    if "parentMessageId" in qs:
        conversation_id = qs["parentMessageId"][0]
    elif "createdTime" in qs:
        conversation_id = qs["createdTime"][0]

    return channel_id, conversation_id


def _detect_redirect(bot_content: str) -> str | None:
    """If bot message contains a redirect hint, return target tenant_id."""
    m = _REDIRECT_RE.search(bot_content)
    if m:
        channel_name = m.group(1).strip()
        return _CHANNEL_TO_TENANT.get(channel_name)
    return None


def _strip_blockquote(lines: list[str]) -> str:
    """Remove leading '> ' from blockquote lines and join."""
    stripped = []
    for line in lines:
        if line.startswith("> "):
            stripped.append(line[2:])
        elif line.strip() == ">":
            stripped.append("")
        else:
            stripped.append(line)
    return "\n".join(stripped).strip()


# ---------------------------------------------------------------------------
# Thread parser
# ---------------------------------------------------------------------------

def _parse_thread(section: str, default_tenant_id: str, file_channel_id: str) -> list[dict] | None:
    """Parse a single ## section into a list of ConversationMessageItem dicts."""
    lines = section.split("\n")

    # Title
    title_line = lines[0].strip()
    if not title_line.startswith("## "):
        return None
    title = title_line[3:].strip()

    # Header: poster, time, URL
    header_text = "\n".join(lines[1:20])
    hm = _HEADER_RE.search(header_text)
    if not hm:
        return None
    poster_name = hm.group(1).strip()
    poster_time = hm.group(2).strip()

    url_match = _VIEW_POST_RE.search(header_text)
    channel_id = file_channel_id
    conversation_id = ""
    if url_match:
        channel_id, conversation_id = _extract_from_url(url_match.group(1))
        if not channel_id:
            channel_id = file_channel_id

    # Split body vs replies
    replies_idx = section.find("### Replies")
    if replies_idx == -1:
        # No replies section — just the poster message
        # Find where the body starts (after the header line)
        header_end = section.find("\n\n", section.find("**From:**"))
        body = section[header_end:].strip() if header_end != -1 else ""
        reply_text = ""
    else:
        header_end = section.find("\n\n", section.find("**From:**"))
        body = section[header_end:replies_idx].strip() if header_end != -1 else ""
        reply_text = section[replies_idx + len("### Replies"):].strip()

    # Build poster message
    partition = f"teams_channel:{conversation_id}" if conversation_id else f"channel:{channel_id}"
    messages: list[dict] = []

    poster_msg = {
        "id": _make_id(f"{conversation_id}:{poster_name}:{poster_time}:poster"),
        "channel_id": channel_id,
        "sender_role": "system" if poster_name == BOT_NAME else "user",
        "sender_id": _make_sender_id(poster_name),
        "sender_name": poster_name,
        "content": body,
        "created_at": _parse_datetime(poster_time),
        "conversation_id": conversation_id,
        "conversation_type": "teams_channel",
        "tenant_id": default_tenant_id,
        "conversation_partition": partition,
        "document_type": "conversation_message",
    }
    messages.append(poster_msg)

    # Parse replies
    if reply_text:
        reply_blocks = _split_replies(reply_text)
        for name, time_str, content in reply_blocks:
            msg = {
                "id": _make_id(f"{conversation_id}:{name}:{time_str}"),
                "channel_id": channel_id,
                "sender_role": "system" if name == BOT_NAME else "user",
                "sender_id": _make_sender_id(name),
                "sender_name": name,
                "content": content,
                "created_at": _parse_datetime(time_str),
                "conversation_id": conversation_id,
                "conversation_type": "teams_channel",
                "tenant_id": default_tenant_id,
                "conversation_partition": partition,
                "document_type": "conversation_message",
            }
            messages.append(msg)

    # Sort chronologically
    messages.sort(key=lambda m: m["created_at"])

    # Detect redirect from bot messages → override tenant_id for the whole thread
    redirect_tenant = None
    for msg in messages:
        if msg["sender_name"] == BOT_NAME:
            rt = _detect_redirect(msg["content"])
            if rt:
                redirect_tenant = rt
                break
    if redirect_tenant:
        for msg in messages:
            msg["tenant_id"] = redirect_tenant

    return messages if messages else None


def _split_replies(text: str) -> list[tuple[str, str, str]]:
    """Split reply blockquotes into (name, time, content) tuples."""
    results: list[tuple[str, str, str]] = []
    lines = text.split("\n")
    i = 0
    while i < len(lines):
        m = _REPLY_RE.match(lines[i])
        if m:
            name = m.group(1).strip()
            time_str = m.group(2).strip()
            # Collect content lines (blockquoted)
            content_lines: list[str] = []
            i += 1
            while i < len(lines):
                line = lines[i]
                if line.startswith("> ") or line.strip() == ">":
                    # Check if this is a new reply header
                    if _REPLY_RE.match(line):
                        break
                    content_lines.append(line)
                elif line.strip() == "":
                    # Empty line might separate replies
                    # Peek ahead to see if next non-empty line is a new reply
                    j = i + 1
                    while j < len(lines) and lines[j].strip() == "":
                        j += 1
                    if j < len(lines) and _REPLY_RE.match(lines[j]):
                        break
                    if j < len(lines) and lines[j].startswith("> "):
                        content_lines.append(line)
                    else:
                        break
                else:
                    break
                i += 1
            content = _strip_blockquote(content_lines)
            if name:
                results.append((name, time_str, content))
        else:
            i += 1
    return results


# ---------------------------------------------------------------------------
# File parser
# ---------------------------------------------------------------------------

def _parse_file(filepath: Path) -> tuple[str, list[list[dict]]]:
    """Parse a markdown file → (default_tenant_id, list of threads).

    Each thread is a list of ConversationMessageItem dicts.
    """
    text = filepath.read_text(encoding="utf-8")

    # Extract tenant_id from line 2
    tm = _TENANT_ID_RE.search(text[:200])
    default_tenant_id = tm.group(1).strip() if tm else filepath.stem.lower()

    # Derive a file-level channel_id from filename
    file_channel_id = f"channel:{filepath.stem}"

    # Split into ## sections
    sections = re.split(r"\n(?=## )", text)
    threads: list[list[dict]] = []
    for section in sections:
        section = section.strip()
        if not section.startswith("## "):
            continue
        thread = _parse_thread(section, default_tenant_id, file_channel_id)
        if thread:
            threads.append(thread)

    return default_tenant_id, threads


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(description="Extract historical posts to JSON.")
    parser.add_argument("--dry-run", action="store_true", help="Parse only, don't write files")
    args = parser.parse_args()

    md_files = sorted(_HIST_DIR.glob("*.md"))
    if not md_files:
        print(f"No .md files found in {_HIST_DIR}")
        return

    # Collect all threads grouped by tenant_id
    tenant_threads: dict[str, list[list[dict]]] = defaultdict(list)
    stats: dict[str, dict] = {}

    for filepath in md_files:
        default_tenant_id, threads = _parse_file(filepath)
        redirected = 0
        for thread in threads:
            effective_tenant = thread[0]["tenant_id"] if thread else default_tenant_id
            if effective_tenant != default_tenant_id:
                redirected += 1
            tenant_threads[effective_tenant].append(thread)

        total_messages = sum(len(t) for t in threads)
        stats[filepath.name] = {
            "default_tenant": default_tenant_id,
            "threads": len(threads),
            "messages": total_messages,
            "redirected": redirected,
        }
        print(f"  {filepath.name}: {len(threads)} threads, {total_messages} msgs, {redirected} redirected")

    # Write JSON per tenant
    if not args.dry_run:
        _JSON_DIR.mkdir(parents=True, exist_ok=True)
        for tenant_id, threads in sorted(tenant_threads.items()):
            output = {"tenant_id": tenant_id, "threads": threads}
            outpath = _JSON_DIR / f"{tenant_id}.json"
            outpath.write_text(json.dumps(output, indent=2, ensure_ascii=False), encoding="utf-8")
            msg_count = sum(len(t) for t in threads)
            print(f"  → {outpath.name}: {len(threads)} threads, {msg_count} messages")

    # Summary
    print(f"\nTotal: {sum(len(t) for t in tenant_threads.values())} threads across {len(tenant_threads)} tenants")
    if args.dry_run:
        print("(dry-run — no files written)")


if __name__ == "__main__":
    main()
