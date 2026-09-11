from __future__ import annotations

import time
from dataclasses import dataclass

from azure.search.documents import SearchClient
from azure.search.documents.models import VectorizableTextQuery

from .index_schema import SEMANTIC_CONFIGURATION


@dataclass(frozen=True, slots=True)
class ValidationResult:
    chunks: int
    languages: dict[str, int]


def validate_generation(
    client: SearchClient,
    git_url: str,
    git_ref: str,
    generation: int,
    timeout_seconds: int,
    expected_opened_min: int,
    expected_closed_min: int,
) -> ValidationResult:
    visibility_filter = (
        f"git_url eq '{_escape(git_url)}' and git_ref eq '{_escape(git_ref)}' "
        f"and valid_from_generation le {generation} and "
        f"(valid_to_generation eq 0 or valid_to_generation gt {generation})"
    )
    deadline = time.monotonic() + timeout_seconds
    prior_count: int | None = None
    stable_reads = 0
    sample: dict | None = None
    languages: dict[str, int] = {}
    while time.monotonic() < deadline:
        results = client.search(
            search_text="*",
            filter=visibility_filter,
            include_total_count=True,
            facets=["language,count:100"],
            top=1,
        )
        rows = list(results)
        count = int(results.get_count() or 0)
        facets = results.get_facets() or {}
        languages = {
            str(item["value"]): int(item["count"])
            for item in facets.get("language", [])
        }
        sample = dict(rows[0]) if rows else None
        opened = _count(
            client,
            f"git_url eq '{_escape(git_url)}' and git_ref eq '{_escape(git_ref)}' "
            f"and valid_from_generation eq {generation}",
        )
        closed = _count(
            client,
            f"git_url eq '{_escape(git_url)}' and git_ref eq '{_escape(git_ref)}' "
            f"and valid_to_generation eq {generation}",
        )
        stable_reads = stable_reads + 1 if count == prior_count else 1
        prior_count = count
        if (
            stable_reads >= 2
            and opened >= expected_opened_min
            and closed >= expected_closed_min
        ):
            break
        time.sleep(2)
    else:
        raise RuntimeError(
            f"Azure AI Search generation {generation} did not become stable "
            f"within {timeout_seconds} seconds"
        )

    if not prior_count:
        raise RuntimeError(
            f"Azure AI Search generation {generation} contains no visible chunks"
        )

    if sample is not None:
        query = str(sample.get("symbol_name") or sample.get("path") or "")[:500]
        if query:
            hybrid = client.search(
                search_text=query,
                filter=visibility_filter,
                query_type="semantic",
                semantic_configuration_name=SEMANTIC_CONFIGURATION,
                vector_queries=[
                    VectorizableTextQuery(
                        text=query,
                        k_nearest_neighbors=3,
                        fields="content_vector",
                    )
                ],
                top=1,
            )
            if not list(hybrid):
                raise RuntimeError(
                    f"hybrid validation returned no result for generation {generation}"
                )
    return ValidationResult(prior_count or 0, languages)


def _escape(value: str) -> str:
    return value.replace("'", "''")


def _count(client: SearchClient, filter_value: str) -> int:
    results = client.search(
        search_text="*",
        filter=filter_value,
        include_total_count=True,
        top=0,
    )
    return int(results.get_count() or 0)
