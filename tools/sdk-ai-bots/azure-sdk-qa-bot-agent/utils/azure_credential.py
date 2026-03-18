"""Azure credential singleton.

Provides a single shared ``DefaultAzureCredential`` instance so that every
module uses the same credential without creating duplicates.
"""

from azure.identity.aio import DefaultAzureCredential

_credential: DefaultAzureCredential | None = None


def get_credential() -> DefaultAzureCredential:
    """Return the shared DefaultAzureCredential (created once on first call)."""
    global _credential
    if _credential is None:
        _credential = DefaultAzureCredential()
    return _credential


async def close_credential() -> None:
    """Close the shared credential.  Safe to call even if never created."""
    global _credential
    if _credential is not None:
        await _credential.close()
        _credential = None

