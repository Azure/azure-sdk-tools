"""Shared display-name policy for excluding feedback/reporting test traffic."""

import re

_TESTING_CHANNEL_PATTERN = re.compile(r"\btesting\b", re.IGNORECASE)
_TESTING_CHANNEL_NAMES = {
    "azure sdk qa bot - auto reply - test",
    "smoke-tests",
}

# Standard Teams channel thread IDs start with "19:" (e.g.
# ``19:abc...@thread.tacv2``).  Personal chats (``a:...``),
# synthetic test IDs, and other non-channel conversation IDs
# use different prefixes and should be excluded from reporting.
_CHANNEL_ID_PREFIX = "19:"


def is_channel_conversation_id(conversation_id: str) -> bool:
    """Return *True* when *conversation_id* looks like a Teams channel thread."""
    return conversation_id.startswith(_CHANNEL_ID_PREFIX)


def is_testing_channel(name: str) -> bool:
    normalized = name.strip().casefold()
    return (
        _TESTING_CHANNEL_PATTERN.search(normalized) is not None
        or normalized in _TESTING_CHANNEL_NAMES
    )
