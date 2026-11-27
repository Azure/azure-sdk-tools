"""Sanitization and log-redaction helpers backed by Microsoft Presidio."""

from __future__ import annotations

import logging
import re
import threading
from functools import lru_cache

import spacy
from presidio_analyzer import AnalyzerEngine, Pattern, PatternRecognizer, RecognizerRegistry
from presidio_analyzer.nlp_engine import SpacyNlpEngine
from presidio_analyzer.predefined_recognizers import (
    CreditCardRecognizer,
    EmailRecognizer,
    IpRecognizer,
    PhoneRecognizer,
    UsSsnRecognizer,
)
from presidio_anonymizer import AnonymizerEngine

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

    # Add a Presidio PatternRecognizer for credentials so all redaction runs through Presidio.
    registry.add_recognizer(
        PatternRecognizer(
            supported_entity="CREDENTIAL_OR_SECRET",
            patterns=[
                Pattern("github_token", r"\b(?:gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,})\b", 0.95),
                Pattern("private_key", r"-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----", 0.95),
                Pattern("azure_key_or_sas", r"(?i)(?:AccountKey|SharedAccessSignature)=[^;\s]+|[?&]sig=[A-Za-z0-9%+/=]{16,}", 0.9),
                Pattern("bearer_or_jwt", r"\bBearer\s+[A-Za-z0-9\-._~+/]{16,}=*|\beyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", 0.9),
            ],
        )
    )
    return AnalyzerEngine(registry=registry, nlp_engine=_BlankSpacyNlpEngine()), AnonymizerEngine()


def sanitize_text(text: str, *, is_public_target: bool) -> str:
    """Redact credentials and PII via Presidio, plus Teams links for public targets."""
    if not text:
        return text

    updated = text
    analyzer, anonymizer = _presidio()
    results = analyzer.analyze(text=updated, language="en")
    if results:
        updated = anonymizer.anonymize(
            text=updated,
            analyzer_results=results,
        ).text

    if is_public_target:
        updated = _TEAMS_LINK_RE.sub("n/a (internal Teams thread)", updated)

    return updated


def is_public_github_repo(owner: str | None, repo: str | None) -> bool:
    """Return True unless the target is a known private GitHub repository."""
    if not owner or not repo:
        return True
    return f"{owner.strip()}/{repo.strip()}".casefold() not in _PRIVATE_GITHUB_REPOS


class LogSanitizationFilter(logging.Filter):
    """Centralized logging filter that redacts secrets, PII, and Teams links."""

    def __init__(self) -> None:
        super().__init__()
        self._local = threading.local()

    def filter(self, record: logging.LogRecord) -> bool:
        # Guard against re-entrant logging from Presidio during sanitization.
        if getattr(self._local, "active", False):
            return True
        self._local.active = True
        try:
            # Format once so %-args are also sanitized, then replace msg/args safely.
            try:
                message = record.getMessage()
            except Exception:
                message = str(record.msg)
            record.msg = sanitize_text(message, is_public_target=True)
            record.args = ()
            if record.exc_text:
                record.exc_text = sanitize_text(record.exc_text, is_public_target=True)
            return True
        finally:
            self._local.active = False


def install_log_sanitization(*logger_names: str) -> None:
    """Attach :class:`LogSanitizationFilter` idempotently to root and named loggers."""
    targets = [logging.getLogger(), *(logging.getLogger(name) for name in logger_names)]
    for target in targets:
        for handler in target.handlers:
            if not any(isinstance(f, LogSanitizationFilter) for f in handler.filters):
                handler.addFilter(LogSanitizationFilter())
