"""Tests for hosted-agent deployment helpers."""

from scripts.deploy_hosted_agent import _qualify_rai_policy_id


def test_qualifies_short_rai_policy_name() -> None:
    assert _qualify_rai_policy_id(
        "Microsoft.DefaultV2",
        subscription_id="00000000-0000-0000-0000-000000000001",
        resource_group="qabot-dev",
        project_endpoint=(
            "https://qabot-ai.services.ai.azure.com/api/projects/qabot"
        ),
    ) == (
        "/subscriptions/00000000-0000-0000-0000-000000000001"
        "/resourceGroups/qabot-dev/providers/Microsoft.CognitiveServices"
        "/accounts/qabot-ai/raiPolicies/Microsoft.DefaultV2"
    )


def test_preserves_qualified_rai_policy_id() -> None:
    policy_id = (
        "/subscriptions/00000000-0000-0000-0000-000000000001"
        "/resourceGroups/qabot-dev/providers/Microsoft.CognitiveServices"
        "/accounts/qabot-ai/raiPolicies/custom"
    )
    assert _qualify_rai_policy_id(
        policy_id,
        subscription_id="ignored",
        resource_group="ignored",
        project_endpoint="https://ignored",
    ) == policy_id
