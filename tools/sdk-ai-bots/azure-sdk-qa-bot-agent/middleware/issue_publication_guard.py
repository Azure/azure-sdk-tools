"""Middleware sanitizing sensitive content before issue writes."""

from __future__ import annotations

from collections.abc import Awaitable, Callable, MutableMapping

from agent_framework import FunctionInvocationContext, FunctionMiddleware

from utils.content_sanitization import is_public_github_repo, sanitize_issue_text


class IssuePublicationGuardMiddleware(FunctionMiddleware):
    """Sanitize secrets, PII, and internal links before publishing issues or comments."""

    _WRITE_TOOLS = frozenset(
        {
            "issue_write",
            "add_issue_comment",
            "create_ado_issue",
            "wit_add_work_item_comment",
        }
    )
    _TEXT_FIELDS = ("title", "body", "comment", "description", "commentText")

    async def process(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        tool_name = context.function.name
        if tool_name not in self._WRITE_TOOLS or not isinstance(
            context.arguments, MutableMapping
        ):
            await call_next()
            return

        is_public = False
        if tool_name in ("issue_write", "add_issue_comment"):
            # GitHub MCP tools accept either separate owner/repo or a combined repository string.
            owner = context.arguments.get("owner")
            repo = context.arguments.get("repo")
            if (
                not (owner and repo)
                and isinstance(context.arguments.get("repository"), str)
                and "/" in context.arguments["repository"]
            ):
                owner, repo = context.arguments["repository"].split("/", 1)
            is_public = is_public_github_repo(owner, repo)

        # Mutate arguments in-place so the downstream tool receives sanitized text.
        for field in self._TEXT_FIELDS:
            val = context.arguments.get(field)
            if isinstance(val, str) and val:
                context.arguments[field] = sanitize_issue_text(
                    val, is_public_target=is_public
                )

        await call_next()
