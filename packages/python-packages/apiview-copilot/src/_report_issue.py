# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Core logic for the report-issue feature.

Used by both the FastAPI endpoint (``app.py``) and the CLI (``cli.py``)
so that both code paths produce identical GitHub issues from the same
input.

The LLM determines which kind of issue this is (``apiview`` or
``parser``) and emits the affected language when relevant. The server
then deterministically derives a title prefix and labels:

* ``apiview`` → title prefix ``[APIView]``, labels ``["APIView"]``.
* ``parser``  → title prefix ``[{Language} APIView]`` (or ``[APIView]``
  when no known language label is available), labels
  ``["APIView", "{Language}"]`` (language label appended only when
  recognised by ``GithubManager.LANGUAGE_LABELS``).
"""

from __future__ import annotations

import json
import logging
from typing import Optional

from src._apiview import get_comment_with_context
from src._github_manager import GithubManager
from src._prompt_runner import run_prompt
from src.mention._github_issue_helpers import create_issue

logger = logging.getLogger(__name__)

REPORT_ISSUE_REPO = "azure-sdk-tools"

# Allowed categories the LLM may emit. ``copilot`` is intentionally
# excluded — problems with APIView Copilot suggestions have a separate,
# more actionable feedback mechanism.
_ALLOWED_LLM_CATEGORIES = ("apiview", "parser")

# Length caps mirror the FastAPI request models in app.py so that the
# core behaves the same regardless of entry point (HTTP endpoint, CLI,
# or any direct Python caller).
MAX_DESCRIPTION_LENGTH = 5000
MAX_COMMENT_FIELD_LENGTH = 10000
_BOUNDED_COMMENT_CONTEXT_FIELDS = ("comment_text", "code_snippet")


def _title_prefix(category: str, language: Optional[str]) -> str:
    """Return the bracketed prefix the server prepends to the LLM title."""
    if category == "parser":
        label = GithubManager.language_label(language)
        if label:
            return f"[{label.value} APIView]"
    return "[APIView]"


def _build_labels(category: str, language: Optional[str]) -> list[str]:
    """Build the GitHub labels list for a report-issue."""
    if category == "parser":
        return GithubManager.build_issue_labels(["APIView"], language)
    return ["APIView"]


def _format_comment_context_for_prompt(ctx: Optional[dict]) -> str:
    """Render the optional comment context as plain text for the LLM."""
    if not ctx:
        return ""
    parts: list[str] = []
    for key, label in (
        ("comment_source", "Source"),
        ("language", "Language"),
        ("comment_text", "Comment"),
        ("code_snippet", "Code Snippet"),
        ("element_id", "Element ID"),
    ):
        value = ctx.get(key)
        if value:
            parts.append(f"{label}: {value}")
    return "\n".join(parts)


def _build_fallback_body(description: str, review_link: Optional[str], comment_context: Optional[dict]) -> str:
    """Assemble a deterministic body when the LLM is unavailable."""
    sections: list[str] = []
    if review_link:
        sections.append(f"## Review Link\n\n{review_link}")
    sections.append(f"## Description\n\n{description}")
    ctx_text = _format_comment_context_for_prompt(comment_context)
    if ctx_text:
        sections.append("## Comment Context\n\n" + ctx_text)
    sections.append("---\n*Reported via APIView*")
    return "\n\n".join(sections)


def _build_fallback_title(description: str) -> str:
    """Build a usable title from the user's description if the LLM fails.

    Conservative single-line title; the LLM is told to be concise, but
    if it fails entirely we take the first line of the description and
    cap to ~14 words so the issue still has a readable title.
    """
    stripped = description.strip()
    first_line = stripped.splitlines()[0].strip() if stripped else ""
    words = first_line.split()
    snippet = " ".join(words[:14]) if words else "Issue reported from APIView"
    return f"[APIView] {snippet}"


def _lookup_comment_context(comment_id: str) -> Optional[dict]:
    """Fetch comment context from the APIView database by comment id.

    Returns a dict matching the shape of the ``commentContext`` request
    field, or ``None`` if the comment cannot be found.
    """
    try:
        ctx = get_comment_with_context(comment_id)
    except Exception as e:  # pylint: disable=broad-except
        logger.warning("Failed to look up comment %s: %s", comment_id, e)
        return None
    if not ctx:
        return None
    comment_obj = ctx.get("comment") or {}
    return {
        "comment_id": comment_id,
        "comment_text": comment_obj.get("CommentText"),
        "comment_source": comment_obj.get("CommentSource"),
        "code_snippet": ctx.get("code"),
        "language": ctx.get("language"),
        "element_id": comment_obj.get("ElementId"),
    }


def _generate_issue_content(
    *,
    description: str,
    review_link: Optional[str],
    language: Optional[str],
    comment_context: Optional[dict],
) -> dict:
    """Run the LLM and return ``{category, language, title, body}``.

    Falls back to a deterministic ``apiview`` template on any LLM error
    so the endpoint always succeeds in filing an issue.
    """
    category: Optional[str] = None
    final_language: Optional[str] = language
    title: Optional[str] = None
    body: Optional[str] = None
    try:
        inputs = {
            "description": description,
            "review_link": review_link or "",
            "language": language or "",
            "comment_context": _format_comment_context_for_prompt(comment_context),
        }
        raw = run_prompt(folder="report_issue", filename="generate_issue.prompty", inputs=inputs)
        result = json.loads(raw)
        emitted_category = (result.get("category") or "").strip().lower()
        if emitted_category in _ALLOWED_LLM_CATEGORIES:
            category = emitted_category
        else:
            logger.warning(
                "LLM returned unexpected category %r; defaulting to apiview.",
                result.get("category"),
            )
        final_language = (result.get("language") or language or None) or None
        title = (result.get("title") or "").strip() or None
        body = (result.get("body") or "").strip() or None
        if not title:
            logger.warning("LLM returned empty title; falling back to template.")
        if not body:
            logger.warning("LLM returned empty body; falling back to template.")
    except Exception as e:  # pylint: disable=broad-except
        logger.warning("LLM generation failed; falling back to template: %s", e)

    if not category:
        category = "apiview"
    if category != "parser":
        final_language = None
    if not body:
        body = _build_fallback_body(description, review_link, comment_context)
    if not title:
        title = _build_fallback_title(description)
        # Fallback already contains a prefix; return as-is.
        return {"category": category, "language": final_language, "title": title, "body": body}

    prefix = _title_prefix(category, final_language)
    full_title = title if title.startswith(prefix) else f"{prefix} {title}"
    return {"category": category, "language": final_language, "title": full_title, "body": body}


def handle_report_issue_request(
    *,
    description: str,
    review_link: Optional[str] = None,
    language: Optional[str] = None,
    comment_context: Optional[dict] = None,
    comment_id: Optional[str] = None,
) -> dict:
    """Handle a report-issue request end-to-end.

    Args:
        description: Free-form user description of the issue (required).
        review_link: Optional URL to the APIView review the user is on.
        language: Optional programming language hint.
        comment_context: Optional dict describing the comment that
            triggered the report. Recognised keys: ``comment_id``,
            ``comment_text``, ``code_snippet``, ``language``,
            ``element_id``, ``comment_source``.
        comment_id: Optional APIView comment id. When provided and
            ``comment_context`` is not, the context is fetched from the
            APIView database via ``get_comment_with_context``.

    Returns:
        Dict with ``issue_url``, ``issue_number``, ``title``, ``body``.

    Raises:
        ValueError: For invalid input (empty / oversized description,
            oversized comment context fields).
    """
    if not description or not description.strip():
        raise ValueError("description must be a non-empty string.")
    if len(description) > MAX_DESCRIPTION_LENGTH:
        raise ValueError(f"description must be at most {MAX_DESCRIPTION_LENGTH} characters.")

    effective_context = comment_context
    if comment_id and not effective_context:
        effective_context = _lookup_comment_context(comment_id)

    if effective_context:
        for field in _BOUNDED_COMMENT_CONTEXT_FIELDS:
            value = effective_context.get(field)
            if value is not None and len(value) > MAX_COMMENT_FIELD_LENGTH:
                raise ValueError(
                    f"comment_context.{field} must be at most {MAX_COMMENT_FIELD_LENGTH} characters."
                )
        if not language:
            language = effective_context.get("language") or language

    content = _generate_issue_content(
        description=description,
        review_link=review_link,
        language=language,
        comment_context=effective_context,
    )
    labels = _build_labels(content["category"], content["language"])
    owner = GithubManager.resolve_owner()

    client = GithubManager.get_instance()
    issue = create_issue(
        client,
        owner=owner,
        repo=REPORT_ISSUE_REPO,
        title=content["title"],
        body=content["body"],
        workflow_tag="report-issue",
        source_tag="APIView",
        labels=labels,
    )
    return {
        "issue_url": issue["html_url"],
        "issue_number": issue["number"],
        "title": content["title"],
        "body": content["body"],
    }
