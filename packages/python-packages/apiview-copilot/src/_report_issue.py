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

Three issue categories are supported:

* ``apiview`` — a problem with the APIView UI / service itself.
  Title prefix: ``[APIView]``. Label: ``APIView``.
* ``copilot`` — a problem with an APIView Copilot suggestion.
  Title prefix: ``[AVC]``. Label: ``APIView Copilot``.
* ``parser`` — a problem with the language-specific APIView parser
  (incorrect / missing tokens, broken element ids, etc.). Requires
  ``language``. Title prefix: ``[{Language} APIView]``. Labels:
  ``APIView`` plus the language label.
"""

from __future__ import annotations

import json
import logging
import os
from typing import Optional

from src._github_manager import GithubManager
from src._prompt_runner import run_prompt
from src.mention._github_issue_helpers import create_issue

logger = logging.getLogger(__name__)

REPORT_ISSUE_REPO = "azure-sdk-tools"

# Mirrors OpenParserIssueWorkflow.LANGUAGE_LABELS so that issues raised
# manually via this endpoint end up with the same language label as ones
# raised automatically through the @mention workflow.
LANGUAGE_LABELS: dict[str, str] = {
    "python": "Python",
    "py": "Python",
    "c#": ".NET",
    "csharp": ".NET",
    "dotnet": ".NET",
    ".net": ".NET",
    "c++": "C++",
    "cpp": "C++",
    "c99": "C99",
    "java": "Java",
    "android": "Java",
    "swift": "Swift",
    "javascript": "javascript",
    "js": "javascript",
    "go": "Go",
    "golang": "Go",
    "rust": "Rust",
}

VALID_CATEGORIES = ("apiview", "copilot", "parser")

# Length caps mirror the FastAPI request models in app.py so that the
# core behaves the same regardless of entry point (HTTP endpoint, CLI,
# or any direct Python caller).
MAX_DESCRIPTION_LENGTH = 5000
MAX_COMMENT_FIELD_LENGTH = 10000
_BOUNDED_COMMENT_CONTEXT_FIELDS = ("comment_text", "code_snippet")


def get_owner() -> str:
    """Return the GitHub owner the issue should be filed against.

    Production targets ``Azure``; everything else targets ``tjprescott``
    so local / staging traffic does not pollute the real repo.
    """
    return "Azure" if os.getenv("ENVIRONMENT_NAME") == "production" else "tjprescott"


def _title_prefix(category: str, language: Optional[str]) -> str:
    if category == "copilot":
        return "[AVC]"
    if category == "parser":
        label = _language_label(language) or (language or "").strip() or "Unknown"
        return f"[{label} APIView]"
    return "[APIView]"


def _language_label(language: Optional[str]) -> Optional[str]:
    if not language:
        return None
    return LANGUAGE_LABELS.get(language.strip().lower())


def _build_labels(category: str, language: Optional[str]) -> list[str]:
    if category == "copilot":
        return ["APIView Copilot"]
    if category == "parser":
        labels = ["APIView"]
        lbl = _language_label(language)
        if lbl:
            labels.append(lbl)
        return labels
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
    """Assemble a deterministic body when the LLM is unavailable.

    The user's description is included verbatim because we have nothing
    better to fall back on.
    """
    sections: list[str] = []
    if review_link:
        sections.append(f"## Review Link\n\n{review_link}")
    sections.append(f"## Description\n\n{description}")
    ctx_text = _format_comment_context_for_prompt(comment_context)
    if ctx_text:
        sections.append("## Comment Context\n\n" + ctx_text)
    sections.append("---\n*Reported via APIView*")
    return "\n\n".join(sections)


def _build_fallback_title(category: str, description: str, language: Optional[str]) -> str:
    snippet = description[:80].replace("\n", " ").strip()
    if len(description) > 80:
        snippet += "..."
    prefix = _title_prefix(category, language)
    return f"{prefix} {snippet}"


def _generate_issue_content(
    *,
    category: str,
    description: str,
    review_link: Optional[str],
    language: Optional[str],
    comment_context: Optional[dict],
) -> dict:
    """Generate ``{title, body}`` via the LLM, falling back to a template on failure."""
    title: Optional[str] = None
    body: Optional[str] = None
    try:
        inputs = {
            "category": category,
            "title_prefix": _title_prefix(category, language),
            "description": description,
            "review_link": review_link or "",
            "language": language or "",
            "comment_context": _format_comment_context_for_prompt(comment_context),
        }
        raw = run_prompt(folder="report_issue", filename="generate_issue.prompty", inputs=inputs)
        result = json.loads(raw)
        title = (result.get("title") or "").strip() or None
        body = (result.get("body") or "").strip() or None
        if not title:
            logger.warning("LLM returned empty title; falling back to template.")
        if not body:
            logger.warning("LLM returned empty body; falling back to template.")
    except Exception as e:  # pylint: disable=broad-except
        logger.warning("LLM generation failed; falling back to template: %s", e)

    if not title:
        title = _build_fallback_title(category, description, language)
    if not body:
        body = _build_fallback_body(description, review_link, comment_context)
    return {"title": title, "body": body}


def handle_report_issue_request(
    *,
    category: str,
    description: str,
    review_link: Optional[str] = None,
    language: Optional[str] = None,
    comment_context: Optional[dict] = None,
) -> dict:
    """Handle a report-issue request end-to-end.

    Args:
        category: One of ``apiview``, ``copilot``, ``parser``.
        description: Free-form user description of the issue.
        review_link: Optional URL to the APIView review the user is on.
        language: Required for ``parser`` category; otherwise optional.
        comment_context: Optional dict describing the comment that
            triggered the report. Recognised keys: ``comment_id``,
            ``comment_text``, ``code_snippet``, ``language``,
            ``element_id``, ``comment_source``.

    Returns:
        Dict with ``issue_url``, ``issue_number``, ``title``, ``body``.

    Raises:
        ValueError: For invalid input (unknown category, missing
            ``language`` for parser category, etc.).
    """
    if category not in VALID_CATEGORIES:
        raise ValueError(f"Invalid category {category!r}; expected one of {VALID_CATEGORIES}.")
    if not description or not description.strip():
        raise ValueError("description must be a non-empty string.")
    if len(description) > MAX_DESCRIPTION_LENGTH:
        raise ValueError(f"description must be at most {MAX_DESCRIPTION_LENGTH} characters.")
    if category == "parser" and not (language and language.strip()):
        raise ValueError("language is required for parser-category reports.")
    if comment_context:
        for field in _BOUNDED_COMMENT_CONTEXT_FIELDS:
            value = comment_context.get(field)
            if value is not None and len(value) > MAX_COMMENT_FIELD_LENGTH:
                raise ValueError(
                    f"comment_context.{field} must be at most {MAX_COMMENT_FIELD_LENGTH} characters."
                )

    content = _generate_issue_content(
        category=category,
        description=description,
        review_link=review_link,
        language=language,
        comment_context=comment_context,
    )
    labels = _build_labels(category, language)
    owner = get_owner()

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
