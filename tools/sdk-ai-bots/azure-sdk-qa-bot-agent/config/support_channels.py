"""Structured catalog of Azure SDK support channels for unresolved handoffs."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class SupportChannel:
    """A Teams support channel that the agent can recommend when unresolved."""

    name: str
    link: str
    description: str


SUPPORT_CHANNELS: tuple[SupportChannel, ...] = (
    SupportChannel(
        name="TypeSpec Discussion",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3A906c1efbbec54dc8949ac736633e6bdf%40thread.skype/"
            "TypeSpec%20Discussion?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description=(
            "TypeSpec language, ARM/data-plane TypeSpec patterns, validation, "
            "tspconfig.yaml, emitters, and OpenAPI migration."
        ),
    ),
    SupportChannel(
        name="API Spec Review",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3A0351f5f9404446e4b4fd4eaf2c27448d%40thread.skype/"
            "API%20Spec%20Review?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description=(
            "azure-rest-api-specs / azure-rest-api-specs-pr PR reviews, CI checks, "
            "LintDiff, Avocado, breaking-change approvals, and merge blockers."
        ),
    ),
    SupportChannel(
        name="Azure API Stewardship Board - General",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3A3ebb18fded0e47938f998e196a52952f%40thread.tacv2/General"
            "?groupId=1a10b50c-e870-4fe0-8483-bf5542a8d2d8"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description=(
            "Data-plane API design, API Stewardship Board async reviews, Office Hours, "
            "signoff, data-plane breaking changes, and retirements (data-plane only)."
        ),
    ),
    SupportChannel(
        name="Azure SDK Onboarding",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3Ade3fce22c2994be18cac50502c55f717%40thread.skype/"
            "Azure%20SDK%20Onboarding?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description=(
            "Service/SDK onboarding prerequisites, release plan creation, readiness, "
            "and lifecycle."
        ),
    ),
    SupportChannel(
        name="SDK release support",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3A084875bb626242ed95f3ac8dddfaa12a%40thread.skype/"
            "SDK%20release%20support?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description=(
            "SDK release execution, package publishing blockers, release schedules, "
            "and release pipelines."
        ),
    ),
    SupportChannel(
        name="AzSDK Tools Agent",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3A6d2c19322c254a80bcc521675134da03%40thread.skype/"
            "AzSDK%20Tools%20Agent?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description="azsdk CLI / MCP setup, failures, and agent/skill tooling.",
    ),
    SupportChannel(
        name="Azure MCP Server - General",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3AJwo_T5Rls-7KVRFup69ij9R4CoBCqtVuV3_TC7JwfRM1%40thread.tacv2/General"
            "?groupId=256e3336-e328-4683-a24a-7a36c8a123fc"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description="Azure MCP Server onboarding, commands, authentication, and tool support.",
    ),
    SupportChannel(
        name="Language - Python",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3Ab97d98e6d22c41e0970a1150b484d935%40thread.skype/"
            "Language%20-%20Python?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description="Python SDK development, generation, CI, and API/PR reviews.",
    ),
    SupportChannel(
        name="Language - DotNet",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3A7b87fb348f224b37b6206fa9d89a105b%40thread.skype/"
            "Language%20-%20DotNet?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description=".NET SDK development, APIView/PR reviews, and testing/release.",
    ),
    SupportChannel(
        name="Code Generation - .NET",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3Aacbd512e57bd475198ea6bf4564599e3%40thread.skype/"
            "Code%20Generation%20-%20.NET?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description=".NET SDK code generation and C# TypeSpec emitter issues.",
    ),
    SupportChannel(
        name="Language - Go",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3A104f00188bb64ef48d1b4d94ccb7a361%40thread.skype/"
            "Language%20-%20Go?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description="Go SDK development, generation, CI, and reviews.",
    ),
    SupportChannel(
        name="Language - Java",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3A5e673e41085f4a7eaaf20823b85b2b53%40thread.skype/"
            "Language%20-%20Java?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description="Java SDK development, generation, CI, and reviews.",
    ),
    SupportChannel(
        name="Language – JS & TS",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3A344f6b5b36ba414daa15473942c7477b%40thread.skype/"
            "Language%20%E2%80%93%20JS%E2%80%89%EF%BC%86%E2%80%89TS%20%F0%9F%A5%B7"
            "?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description="JS/TS SDK development, generation, CI, and reviews.",
    ),
    SupportChannel(
        name="Language - C++",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3Aa5180a2de11a40cb83a822f588d60c15%40thread.skype/"
            "Language%20-%20C%E1%90%A9%E1%90%A9?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description="C++ SDK support.",
    ),
    SupportChannel(
        name="Language - Rust",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3A93ad56ee1ff149eb9f233bcf6432399e%40thread.skype/"
            "Language%20-%20Rust?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description="Rust SDK support.",
    ),
    SupportChannel(
        name="Language - Mobile",
        link=(
            "https://teams.microsoft.com/l/channel/"
            "19%3Aae8aa86450294f31872cb9e2b1b13290%40thread.skype/"
            "Language%20-%20Mobile?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121"
            "&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47"
        ),
        description="Mobile SDK support.",
    ),
)

_BY_NAME: dict[str, SupportChannel] = {c.name.casefold(): c for c in SUPPORT_CHANNELS}
_BY_LINK: dict[str, SupportChannel] = {c.link: c for c in SUPPORT_CHANNELS}


def get_support_channel(
    name: str | None = None,
    link: str | None = None,
) -> SupportChannel | None:
    """Resolve a configured support channel by link or case-insensitive name."""
    if link:
        found = _BY_LINK.get(link.strip())
        if found is not None:
            return found
    if name:
        return _BY_NAME.get(name.strip().casefold())
    return None
