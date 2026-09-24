"""Shared display-name policy for excluding feedback/reporting test traffic."""

import re

_TESTING_CHANNEL_PATTERN = re.compile(r"\btesting\b", re.IGNORECASE)
_TESTING_CHANNEL_NAMES = {
    "azure sdk qa bot - auto reply - test",
    "smoke-tests",
}


def is_testing_channel(name: str) -> bool:
    normalized = name.strip().casefold()
    return (
        _TESTING_CHANNEL_PATTERN.search(normalized) is not None
        or normalized in _TESTING_CHANNEL_NAMES
    )