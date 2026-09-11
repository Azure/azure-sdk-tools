"""Code retrieval tool for the Azure SDK QA Bot Agent."""

from __future__ import annotations

from typing import Annotated

from agent_framework import FunctionInvocationContext

from config.tenant_config import TenantID
from models.code_search import GitRepositoryRef, SearchIndexedCodeResult
from tools import tool
from utils.code_search import CodeSearchClient


class CodeSearchTools:
    def __init__(self, client: CodeSearchClient | None = None) -> None:
        self._client = client

    def _get_client(self) -> CodeSearchClient:
        if self._client is None:
            self._client = CodeSearchClient()
        return self._client

    @tool
    async def search_indexed_code(
        self,
        *,
        queries: Annotated[
            list[str],
            "One to three implementation-focused search formulations. Keep exact "
            "symbols, decorators, error text, and API names in the first query, "
            "then add broader conceptual wording only when useful.",
        ],
        repositories: Annotated[
            list[GitRepositoryRef] | None,
            "Optional Git URL/full-ref pairs selected from repositories configured "
            "for the active tenant. Omit to search every configured repository.",
        ] = None,
        languages: Annotated[
            list[str] | None,
            "Optional language names such as typespec, python, or typescript.",
        ] = None,
        path_prefixes: Annotated[
            list[str] | None,
            "Optional repository-relative path prefixes. Globs and parent traversal "
            "are not supported.",
        ] = None,
        context: FunctionInvocationContext,
    ) -> SearchIndexedCodeResult:
        """Search commit-pinned indexed source code for implementation evidence."""
        tenant_value = context.kwargs.get("tenant_id")
        if not isinstance(tenant_value, str):
            raise RuntimeError("tenant context was not injected for code search")
        try:
            tenant_id = TenantID(tenant_value)
        except ValueError as exc:
            raise RuntimeError(f"unknown injected tenant context: {tenant_value}") from exc
        repository_refs = (
            [GitRepositoryRef.model_validate(item) for item in repositories]
            if repositories
            else None
        )
        return await self._get_client().search(
            tenant_id=tenant_id,
            queries=queries,
            repositories=repository_refs,
            languages=languages,
            path_prefixes=path_prefixes,
        )
