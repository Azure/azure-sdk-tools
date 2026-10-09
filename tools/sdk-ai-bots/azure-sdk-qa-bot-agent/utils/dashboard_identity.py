"""Display identity derived from Azure App Service EasyAuth headers."""

import base64
import binascii
import json
from typing import TypedDict


_DISPLAY_NAME_CLAIMS = (
    "name",
    "preferred_username",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
)


class DashboardIdentity(TypedDict):
    authenticated: bool
    display_name: str


def get_dashboard_identity(client_principal: str | None) -> DashboardIdentity:
    """Return the signed-in user's display identity, or the local fallback."""
    if not client_principal:
        return {"authenticated": False, "display_name": "Local development"}

    try:
        padding = "=" * (-len(client_principal) % 4)
        principal = json.loads(
            base64.b64decode(client_principal + padding, validate=True)
        )
        if not isinstance(principal, dict):
            raise ValueError("EasyAuth principal must be an object")
        claims = principal.get("claims", [])
        if not isinstance(claims, list):
            raise ValueError("EasyAuth claims must be a list")
    except (binascii.Error, json.JSONDecodeError, UnicodeDecodeError, ValueError):
        return {"authenticated": False, "display_name": "Local development"}

    claim_values: dict[str, str] = {}
    for claim in claims:
        if not isinstance(claim, dict):
            continue
        claim_type = claim.get("typ")
        claim_value = claim.get("val")
        if isinstance(claim_type, str) and isinstance(claim_value, str):
            claim_values[claim_type] = claim_value
    display_name = next(
        (
            claim_values[claim_type]
            for claim_type in _DISPLAY_NAME_CLAIMS
            if claim_values.get(claim_type)
        ),
        "Signed in",
    )
    return {"authenticated": True, "display_name": display_name}