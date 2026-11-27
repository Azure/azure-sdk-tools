"""Middleware sanitizing sensitive content before outbound issue, PR, or comment writes."""

from __future__ import annotations

from collections.abc import Awaitable, Callable, Iterable, MutableMapping

from agent_framework import FunctionInvocationContext, FunctionMiddleware

from utils.content_sanitization import is_public_github_repo, sanitize_text


class OutboundSanitizationMiddleware(FunctionMiddleware):
    """Sanitize credentials, PII, and internal Teams links before outbound writes."""

    _DEFAULT_WRITE_TOOLS = frozenset(
        {
            # GitHub issues, PRs, and comments
            "issue_write",
            "add_issue_comment",
            "create_pull_request",
            "update_pull_request",
            "add_comment_to_pending_review",
            "pull_request_review_write",
            # Azure DevOps work items and comments
            "create_ado_issue",
            "wit_add_work_item_comment",
        }
    )
    _TEXT_FIELDS = ("title", "body", "comment", "description", "commentText")

    def __init__(self, extra_write_tools: Iterable[str] = ()) -> None:
        self._write_tools = self._DEFAULT_WRITE_TOOLS | frozenset(extra_write_tools)

    async def process(
        self,
        context: FunctionInvocationContext,
        call_next: Callable[[], Awaitable[None]],
    ) -> None:
        tool_name = context.function.name
        if tool_name not in self._write_tools or not isinstance(
            context.arguments, MutableMapping
        ):
            await call_next()
            return

        # GitHub MCP tools accept either separate owner/repo or a combined repository string.
        owner = context.arguments.get("owner")
        repo = context.arguments.get("repo")
        if (
            not (owner and repo)
            and isinstance(context.arguments.get("repository"), str)
            and "/" in context.arguments["repository"]
        ):
            owner, repo = context.arguments["repository"].split("/", 1)
        is_public = (
            is_public_github_repo(owner, repo)
            if isinstance(owner, str) and isinstance(repo, str) and owner and repo
            else False
        )

        # Mutate arguments in-place so the downstream tool receives sanitized text.
        for field in self._TEXT_FIELDS:
            val = context.arguments.get(field)
            if isinstance(val, str) and val:
                context.arguments[field] = sanitize_text(
                    val, is_public_target=is_public
                )

        await call_next()
