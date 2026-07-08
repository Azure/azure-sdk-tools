"""Parse historical Teams channel markdown files into conversation models.

Each MD file has the format::

    # <Channel> Teams Channel Posts
    tenant_id: <tenant_id>

    ---

    ## <Thread Title>

    **From:** <Name>　　**Time:** <datetime>　　[View post](<url>)

    <original post content>

    ### Replies

    > **<Name>** · <datetime>
    >
    > <reply content>

    ---

Replies are listed newest-first and must be reversed for chronological order.

Usage::

    from scripts.dev.md_thread_parser import parse_md_file, ParsedThread

    threads, tenant_id = parse_md_file(Path("historical_messages/general.md"))
    for t in threads:
        print(t.title, len(t.messages))
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import unquote

from models.conversation import (
    ConversationDocumentType,
    ConversationMessage,
    ConversationMessageItem,
    Role,
)

# ---------------------------------------------------------------------------
# Regexes
# ---------------------------------------------------------------------------

_FROM_RE = re.compile(
    r"\*\*From:\*\*\s*(.+?)(?:　|&nbsp;|\s{2,})\s*\*\*Time:\*\*\s*(.+?)(?:　|&nbsp;|\s{2,})",
)
_REPLY_HEADER_RE = re.compile(
    r"^>\s*\*\*(.+?)\*\*\s*·\s*(.+)$",
)
_VIEW_POST_RE = re.compile(
    r"\[View post\]\(([^)]+)\)",
)

_BOT_NAME = "Azure SDK Q&A Bot"


# ---------------------------------------------------------------------------
# Data classes
# ---------------------------------------------------------------------------

@dataclass
class ParsedThread:
    """A single parsed thread with properly typed message objects."""

    index: int
    title: str
    poster: str
    original_post: str
    conversation_id: str
    messages: list[ConversationMessageItem] = field(default_factory=list)
    raw_messages: list[dict] = field(default_factory=list)


# ---------------------------------------------------------------------------
# URL → conversation_id
# ---------------------------------------------------------------------------

def _extract_conversation_id(url: str) -> str:
    """Extract conversation_id from a Teams View post URL.

    URL format::
        https://teams.microsoft.com/l/message/
            19%3Axxx%40thread.skype/yyy?groupId=...

    Returns ``19:xxx@thread.skype;messageid=yyy``.
    """
    decoded = unquote(url)
    # Pattern: /message/<thread_part>/<message_id>?
    m = re.search(r"/message/(19:[^/]+)/(\d+)", decoded)
    if m:
        return f"{m.group(1)};messageid={m.group(2)}"
    return "unknown"


# ---------------------------------------------------------------------------
# Sender helpers
# ---------------------------------------------------------------------------

def _is_bot(name: str) -> bool:
    return "bot" in name.lower() or "azure sdk q&a" in name.lower()


def _sender_id(name: str) -> str:
    if _is_bot(name):
        return "bot_qa"
    return f"user_{name.replace(' ', '_')}"


def _sender_role(name: str) -> Role:
    return Role.System if _is_bot(name) else Role.User


# ---------------------------------------------------------------------------
# Reply parser
# ---------------------------------------------------------------------------

def _parse_replies(
    replies_block: str,
    conversation_id: str,
    tenant_id: str,
) -> list[tuple[ConversationMessageItem, dict]]:
    """Parse blockquoted replies into (ConversationMessageItem, raw_dict) pairs.

    Returns newest-first (as they appear in the markdown).
    Caller should reverse for chronological order.
    """
    results: list[tuple[ConversationMessageItem, dict]] = []
    current_name: str | None = None
    current_time: str | None = None
    current_lines: list[str] = []

    def _flush():
        nonlocal current_name, current_time, current_lines
        if current_name is None:
            return
        content = "\n".join(current_lines).strip()
        content = re.sub(r"^>\s?", "", content, flags=re.MULTILINE).strip()
        if not content:
            current_name = None
            current_lines = []
            return

        ts = _parse_time(current_time) if current_time else datetime.now(timezone.utc)
        role = _sender_role(current_name)
        sid = _sender_id(current_name)

        raw = {
            "sender_role": role.value,
            "sender_id": sid,
            "sender_name": current_name,
            "content": content,
            "conversation_id": conversation_id,
        }
        item = ConversationMessageItem(
            id=f"msg_placeholder",  # will be renumbered later
            tenant_id=tenant_id,
            sender_role=role,
            sender_id=sid,
            sender_name=current_name,
            content=content,
            created_at=ts,
            conversation_id=conversation_id,
            conversation_partition=conversation_id,
        )
        results.append((item, raw))
        current_name = None
        current_lines = []

    for line in replies_block.split("\n"):
        header_match = _REPLY_HEADER_RE.match(line)
        if header_match:
            _flush()
            current_name = header_match.group(1).strip()
            current_time = header_match.group(2).strip()
            current_lines = []
        elif current_name is not None:
            current_lines.append(line)

    _flush()
    return results


def _parse_time(time_str: str | None) -> datetime:
    """Best-effort parse of a time string like '2026-03-12 05:58'."""
    if not time_str:
        return datetime.now(timezone.utc)
    time_str = time_str.strip()
    for fmt in ("%Y-%m-%d %H:%M", "%Y-%m-%d %H:%M:%S", "%Y-%m-%d"):
        try:
            return datetime.strptime(time_str, fmt).replace(tzinfo=timezone.utc)
        except ValueError:
            continue
    return datetime.now(timezone.utc)


# ---------------------------------------------------------------------------
# Thread parser
# ---------------------------------------------------------------------------

def _parse_single_thread(
    section: str,
    index: int,
    tenant_id: str,
) -> ParsedThread | None:
    """Parse one markdown section (between ``---`` separators) into a ParsedThread."""
    section = section.strip()
    if not section:
        return None

    # Title — sections without a ## heading are not threads (e.g. file header)
    title_match = re.search(r"^## (.+)$", section, re.MULTILINE)
    if not title_match:
        return None
    title = title_match.group(1).strip()

    # Poster + time
    from_match = _FROM_RE.search(section)
    poster = from_match.group(1).strip() if from_match else "Unknown"
    post_time_str = from_match.group(2).strip() if from_match else None

    # Conversation ID from View post link
    link_match = _VIEW_POST_RE.search(section)
    conversation_id = _extract_conversation_id(link_match.group(1)) if link_match else f"thread-{index}"

    # Split into original post and replies
    replies_split = re.split(r"^### Replies\s*$", section, flags=re.MULTILINE)
    original_post_block = replies_split[0]
    replies_block = replies_split[1] if len(replies_split) > 1 else ""

    # Extract original post content (everything after the From/Time/View post line)
    post_lines = original_post_block.split("\n")
    content_start = 0
    for j, line in enumerate(post_lines):
        if "**From:**" in line or line.startswith("## ") or line.startswith("# "):
            content_start = j + 1
    original_post_content = "\n".join(post_lines[content_start:]).strip()

    # Build messages
    messages: list[ConversationMessageItem] = []
    raw_messages: list[dict] = []

    # Original post as first message
    if original_post_content:
        poster_role = _sender_role(poster)
        poster_sid = _sender_id(poster)
        post_ts = _parse_time(post_time_str)

        raw = {
            "sender_role": poster_role.value,
            "sender_id": poster_sid,
            "sender_name": poster,
            "content": original_post_content,
            "conversation_id": conversation_id,
        }
        item = ConversationMessageItem(
            id="msg_0",
            tenant_id=tenant_id,
            sender_role=poster_role,
            sender_id=poster_sid,
            sender_name=poster,
            content=original_post_content,
            created_at=post_ts,
            conversation_id=conversation_id,
            conversation_partition=conversation_id,
        )
        messages.append(item)
        raw_messages.append(raw)

    # Replies (newest-first in MD → reverse for chronological)
    if replies_block.strip():
        reply_pairs = _parse_replies(replies_block, conversation_id, tenant_id)
        for item, raw in reversed(reply_pairs):
            messages.append(item)
            raw_messages.append(raw)

    # Renumber message IDs sequentially
    for i, msg in enumerate(messages):
        msg.id = f"msg_{i}"

    return ParsedThread(
        index=index,
        title=title,
        poster=poster,
        original_post=original_post_content[:500],
        conversation_id=conversation_id,
        messages=messages,
        raw_messages=raw_messages,
    )


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def parse_md_file(path: Path) -> tuple[list[ParsedThread], str]:
    """Parse a historical messages markdown file.

    Returns (threads, tenant_id).
    """
    text = path.read_text(encoding="utf-8")

    # Extract tenant_id from line 2
    lines = text.split("\n", 3)
    tenant_id = "unknown"
    for line in lines[:3]:
        if line.startswith("tenant_id:"):
            tenant_id = line.split(":", 1)[1].strip()
            break

    # Split into thread sections
    sections = re.split(r"\n---\n", text)
    threads: list[ParsedThread] = []

    for i, section in enumerate(sections):
        thread = _parse_single_thread(section, i, tenant_id)
        if thread and thread.messages:
            threads.append(thread)

    return threads, tenant_id


def find_last_expert_message(
    thread: ParsedThread,
) -> ConversationMessage | None:
    """Find the last message that is not from the original poster or the bot.

    This is used as the ``message`` argument to ``_extract_episode`` so the
    sender check naturally passes (Option 1 strategy).

    Returns None if no expert reply exists.
    """
    if not thread.messages:
        return None

    poster_id = thread.messages[0].sender_id

    for msg in reversed(thread.messages):
        if msg.sender_id == poster_id:
            continue
        if _is_bot(msg.sender_name):
            continue
        # Found an expert reply — convert to ConversationMessage
        return ConversationMessage(
            id=msg.id,
            tenant_id=msg.tenant_id,
            sender_role=msg.sender_role,
            sender_id=msg.sender_id,
            sender_name=msg.sender_name,
            content=msg.content,
            created_at=msg.created_at,
            conversation_id=msg.conversation_id,
        )

    return None
