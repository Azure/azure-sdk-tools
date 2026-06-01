"""Data models for web content retrieval tools."""

from __future__ import annotations

from pydantic import BaseModel


class FetchWebpageResult(BaseModel):
    """Tool output for fetching a public web page or llms.txt resource."""

    success: bool = True
    url: str
    resolved_url: str
    status_code: int | None = None
    content_type: str
    title: str = ""
    headings: list[str] = []
    content_excerpt: str = ""
    used_llms_txt_hint: bool = False
    error: str | None = None
