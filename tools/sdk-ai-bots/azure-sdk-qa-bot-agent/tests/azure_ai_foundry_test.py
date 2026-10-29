from unittest.mock import ANY, Mock

import pytest

from utils import azure_ai_foundry


@pytest.mark.parametrize(
    ("model", "expected"),
    [(None, "configured-model"), ("gpt-6.1-sol", "gpt-6.1-sol")],
)
def test_agent_client_uses_model_override_or_configuration(
    monkeypatch: pytest.MonkeyPatch, model: str | None, expected: str
) -> None:
    settings = {
        "AI_FOUNDRY_PROJECT_ENDPOINT": "https://example.test/api/projects/test",
        "AI_FOUNDRY_AGENT_COMPLETION_MODEL": "configured-model",
    }
    credential = Mock()
    factory = Mock()
    monkeypatch.setattr(azure_ai_foundry, "_agent_client", None)
    monkeypatch.setattr(azure_ai_foundry, "cfg", settings.get)
    monkeypatch.setattr(azure_ai_foundry, "get_credential", lambda: credential)
    monkeypatch.setattr(azure_ai_foundry, "FoundryChatClient", factory)

    client = azure_ai_foundry.get_agent_client(model=model)

    assert client is factory.return_value
    factory.assert_called_once_with(
        project_endpoint=settings["AI_FOUNDRY_PROJECT_ENDPOINT"],
        model=expected,
        credential=credential,
        compaction_strategy=ANY,
    )
    assert azure_ai_foundry.get_agent_client(model=model) is client
    factory.assert_called_once()
