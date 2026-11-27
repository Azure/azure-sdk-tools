"""Sanitization and log-redaction helpers backed by Presidio and detect-secrets."""

from __future__ import annotations

import hashlib
import logging
import re
import threading
from functools import lru_cache

import spacy
from detect_secrets.plugins.azure_storage_key import AzureStorageKeyDetector
from detect_secrets.plugins.basic_auth import BasicAuthDetector
from detect_secrets.plugins.github_token import GitHubTokenDetector
from detect_secrets.plugins.jwt import JwtTokenDetector
from detect_secrets.plugins.keyword import KeywordDetector
from detect_secrets.plugins.private_key import PrivateKeyDetector
from presidio_analyzer import AnalyzerEngine, RecognizerRegistry
from presidio_analyzer.nlp_engine import SpacyNlpEngine
from presidio_analyzer.predefined_recognizers import (
    CreditCardRecognizer,
    EmailRecognizer,
    IpRecognizer,
    PhoneRecognizer,
    UsSsnRecognizer,
)
from presidio_anonymizer import AnonymizerEngine
from presidio_anonymizer.entities import OperatorConfig

# Azure/azure-sdk-pr is the only private GitHub issue target; all other GitHub repos are treated as public.
_PRIVATE_GITHUB_REPOS = frozenset({"azure/azure-sdk-pr"})
_TEAMS_LINK_RE = re.compile(r"https://teams\.microsoft\.com/\S+", re.I)


class _BlankSpacyNlpEngine(SpacyNlpEngine):
    """Blank spaCy pipeline so containers do not need a large NLP model download."""

    def load(self) -> None:
        self.nlp = {"en": spacy.blank("en")}


@lru_cache(maxsize=1)
def _presidio() -> tuple[AnalyzerEngine, AnonymizerEngine]:
    logging.getLogger("presidio-analyzer").setLevel(logging.WARNING)
    registry = RecognizerRegistry()
    # Register only the PII recognizers needed for developer/SDK text so URLs, dates, and UUIDs stay intact.
    for recognizer_cls in (
        EmailRecognizer,
        PhoneRecognizer,
        IpRecognizer,
        CreditCardRecognizer,
        UsSsnRecognizer,
    ):
        registry.add_recognizer(recognizer_cls())
    return AnalyzerEngine(registry=registry, nlp_engine=_BlankSpacyNlpEngine()), AnonymizerEngine()


@lru_cache(maxsize=1)
def _secret_plugins():
    # Cache secret detectors once per process for thread-safe reuse.
    return (
        GitHubTokenDetector(),
        AzureStorageKeyDetector(),
        PrivateKeyDetector(),
        JwtTokenDetector(),
        BasicAuthDetector(),
        KeywordDetector(),
    )


def mask_identifier(value: str | None) -> str:
    """Return a non-PII short hash token for log correlation."""
    return f"id:{hashlib.sha256(value.encode()).hexdigest()[:10]}" if value else "none"


def sanitize_issue_text(text: str, *, is_public_target: bool) -> tuple[str, list[str]]:
    """Redact secrets via ``detect-secrets``, PII via Presidio, and Teams links for public targets."""
    if not text:
        return text, []

    redacted: list[str] = []
    plugins = _secret_plugins()
    new_lines: list[str] = []
    for line in text.splitlines():
        for plugin in plugins:
            for secret in plugin.analyze_line("<inline>", line):
                if secret.secret_value:
                    # GitHubTokenDetector's capturing group yields only the prefix (e.g. 'ghp'); redact the full token.
                    if secret.type == "GitHub Token":
                        line = re.sub(
                            rf"\b{re.escape(secret.secret_value)}_[A-Za-z0-9_]+\b",
                            "[redacted-secret]",
                            line,
                        )
                    else:
                        line = line.replace(secret.secret_value, "[redacted-secret]")
                    redacted.append(secret.type)
        new_lines.append(line)
    updated = "\n".join(new_lines)

    analyzer, anonymizer = _presidio()
    results = analyzer.analyze(text=updated, language="en")
    if results:
        updated = anonymizer.anonymize(
            text=updated,
            analyzer_results=results,
            operators={"DEFAULT": OperatorConfig("replace", {"new_value": "[redacted-pii]"})},
        ).text
        redacted.extend(r.entity_type for r in results)

    if is_public_target:
        updated, count = _TEAMS_LINK_RE.subn("n/a (internal Teams thread)", updated)
        if count:
            redacted.append("teams_link")

    return updated, sorted(set(redacted))


def sanitize_log_text(text: str | None, *, max_length: int = 200) -> str:
    """Redact secrets, PII, and Teams links before writing to logs."""
    if not text:
        return ""
    sanitized, _ = sanitize_issue_text(text, is_public_target=True)
    return sanitized[:max_length] + "...[truncated]" if len(sanitized) > max_length else sanitized


def is_public_github_repo(owner: str | None, repo: str | None) -> bool:
    """Return True unless the target is a known private GitHub repository."""
    if not owner or not repo:
        return True
    return f"{owner.strip()}/{repo.strip()}".casefold() not in _PRIVATE_GITHUB_REPOS


class LogSanitizationFilter(logging.Filter):
    """Centralized logging filter that redacts secrets, PII, and Teams links."""

    def __init__(self, max_length: int = 4000) -> None:
        super().__init__()
        self._max_length = max_length
        self._local = threading.local()

    def filter(self, record: logging.LogRecord) -> bool:
        # Guard against re-entrant logging from Presidio/detect-secrets during sanitization.
        if getattr(self._local, "active", False):
            return True
        self._local.active = True
        try:
            # Format once so %-args are also sanitized, then replace msg/args safely.
            try:
                message = record.getMessage()
            except Exception:
                message = str(record.msg)
            record.msg = sanitize_log_text(message, max_length=self._max_length)
            record.args = ()
            if record.exc_text:
                record.exc_text = sanitize_log_text(record.exc_text, max_length=self._max_length)
            return True
        finally:
            self._local.active = False


def install_log_sanitization(
    *logger_names: str,
    max_length: int = 4000,
) -> None:
    """Attach :class:`LogSanitizationFilter` idempotently to root and named loggers."""
    targets = [logging.getLogger(), *(logging.getLogger(name) for name in logger_names)]
    for target in targets:
        for handler in target.handlers:
            if not any(isinstance(f, LogSanitizationFilter) for f in handler.filters):
                handler.addFilter(LogSanitizationFilter(max_length=max_length))
