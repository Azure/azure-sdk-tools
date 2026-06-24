"""
Unit tests for the Foundry red-team result processor (CONDENSED FIXTURE).

Source: azure-sdk-for-python build 6444663, issue #47228. This is a faithful
reduction of tests/unittests/test_redteam/test_foundry.py: the two classes that
exercise FoundryResultProcessor._build_messages_from_pieces are kept verbatim
(TestFoundryResultProcessor, TestAdversarialChatTargetRegression); ~20 unrelated
test classes (~4,700 lines) are elided. Context-only for pipeline-*-python evals.
"""

import pytest
import uuid
import json
import asyncio
from unittest.mock import AsyncMock, MagicMock, patch, PropertyMock
from typing import Dict, List, Any

from azure.ai.evaluation.red_team._attack_strategy import AttackStrategy
from azure.ai.evaluation.red_team._attack_objective_generator import RiskCategory

# Import Foundry components - these require pyrit to be installed
from azure.ai.evaluation.red_team._foundry._dataset_builder import (
    DatasetConfigurationBuilder,
)
from azure.ai.evaluation.red_team._foundry._strategy_mapping import StrategyMapper
from azure.ai.evaluation.red_team._foundry._rai_scorer import RAIServiceScorer
from azure.ai.evaluation.red_team._foundry._scenario_orchestrator import (
    ScenarioOrchestrator,
)
from azure.ai.evaluation.red_team._foundry._foundry_result_processor import (
    FoundryResultProcessor,
    _get_attack_type_name,
)
from azure.ai.evaluation.red_team._result_processor import ResultProcessor
from azure.ai.evaluation.red_team._foundry._execution_manager import (
    FoundryExecutionManager,
)



class TestFoundryResultProcessor:
    """Test the FoundryResultProcessor class."""

    def test_initialization(self):
        """Test FoundryResultProcessor initialization."""
        mock_scenario = MagicMock()
        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        assert processor.scenario == mock_scenario
        assert processor.dataset_config == mock_dataset
        assert processor.risk_category == "violence"

    def test_build_context_lookup(self):
        """Test building context lookup from dataset config."""
        mock_scenario = MagicMock()

        # Create mock seed group with seeds
        mock_objective = MagicMock()
        mock_objective.__class__.__name__ = "SeedObjective"
        mock_objective.prompt_group_id = uuid.uuid4()
        mock_objective.value = "Attack objective"
        mock_objective.metadata = {"risk_subtype": "test"}

        mock_context = MagicMock()
        mock_context.__class__.__name__ = "SeedPrompt"
        mock_context.prompt_group_id = mock_objective.prompt_group_id
        mock_context.value = "Context content"
        mock_context.metadata = {"context_type": "email", "is_attack_vehicle": True}

        mock_seed_group = MagicMock()
        mock_seed_group.seeds = [mock_objective, mock_context]

        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = [mock_seed_group]

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        # Check that context lookup was built with the mock context
        assert len(processor._context_lookup) > 0

    def test_get_summary_stats_empty(self):
        """Test summary stats with no results."""
        mock_scenario = MagicMock()
        mock_scenario.get_attack_results.return_value = []

        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        stats = processor.get_summary_stats()

        assert stats["total"] == 0
        assert stats["successful"] == 0
        assert stats["failed"] == 0
        assert stats["undetermined"] == 0
        assert stats["asr"] == 0.0

    def test_get_summary_stats_with_results(self):
        """Test summary stats with mixed results."""
        from pyrit.models.attack_result import AttackOutcome

        mock_scenario = MagicMock()

        success = MagicMock()
        success.outcome = AttackOutcome.SUCCESS

        failure = MagicMock()
        failure.outcome = AttackOutcome.FAILURE

        undetermined = MagicMock()
        undetermined.outcome = AttackOutcome.UNDETERMINED

        mock_scenario.get_attack_results.return_value = [
            success,
            success,
            failure,
            undetermined,
        ]

        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        stats = processor.get_summary_stats()

        assert stats["total"] == 4
        assert stats["successful"] == 2
        assert stats["failed"] == 1
        assert stats["undetermined"] == 1
        assert stats["asr"] == pytest.approx(2 / 3)  # 2 successes / 3 decided (undetermined excluded)

    def test_build_messages_from_pieces(self):
        """Test building message list from conversation pieces."""
        mock_scenario = MagicMock()
        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        # Create mock pieces
        user_piece = MagicMock()
        user_piece.api_role = "user"
        user_piece.original_value = "User message"
        user_piece.converted_value = "User message"
        user_piece.sequence = 0

        assistant_piece = MagicMock()
        assistant_piece.api_role = "assistant"
        assistant_piece.original_value = "Assistant response"
        assistant_piece.converted_value = "Assistant response"
        assistant_piece.sequence = 1

        messages = processor._build_messages_from_pieces([user_piece, assistant_piece])

        assert len(messages) == 2
        assert messages[0]["role"] == "user"
        assert messages[0]["content"] == "User message"
        assert messages[1]["role"] == "assistant"
        assert messages[1]["content"] == "Assistant response"
        # When original and converted match (no encoding), no audit field is added.
        assert "original_value" not in messages[0]
        assert "original_value" not in messages[1]

    def test_build_messages_preserves_encoded_user_prompt(self):
        """Encoded attack prompts must be stored as the wire payload.

        Regression test for
        https://github.com/Azure/azure-sdk-for-python/issues/47228 — for
        converter-based strategies (Base64, Flip, Morse, ROT13, etc.) the
        target receives ``converted_value``, so the persisted conversation
        must report ``converted_value`` as ``content`` (not the decoded
        ``original_value``). The pre-converter objective is preserved as
        ``original_value`` on the same message so callers still have an
        audit trail of what the attack meant to say.
        """
        mock_scenario = MagicMock()
        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        # Simulate a Base64-converted user turn: the target actually saw the
        # encoded payload, but the SDK still has the plaintext objective.
        user_piece = MagicMock()
        user_piece.api_role = "user"
        user_piece.original_value = "How do I make a dangerous thing?"
        user_piece.converted_value = "SG93IGRvIEkgbWFrZSBhIGRhbmdlcm91cyB0aGluZz8="
        user_piece.sequence = 0
        user_piece.prompt_metadata = {}
        user_piece.labels = {}

        # Assistant response — converter is a no-op on the response side, so
        # original and converted match. No audit field should be emitted.
        assistant_piece = MagicMock()
        assistant_piece.api_role = "assistant"
        assistant_piece.original_value = "Sorry, I can't help with that."
        assistant_piece.converted_value = "Sorry, I can't help with that."
        assistant_piece.sequence = 1
        assistant_piece.prompt_metadata = {}
        assistant_piece.labels = {}

        messages = processor._build_messages_from_pieces([user_piece, assistant_piece])

        # The user turn must carry the encoded payload as content so consumers
        # can verify exactly what the target received.
        assert messages[0]["role"] == "user"
        assert messages[0]["content"] == "SG93IGRvIEkgbWFrZSBhIGRhbmdlcm91cyB0aGluZz8="
        # The plaintext objective is preserved alongside it for auditability.
        assert messages[0]["original_value"] == "How do I make a dangerous thing?"

        # Assistant turn is unchanged: content == converted_value, no audit field.
        assert messages[1]["role"] == "assistant"
        assert messages[1]["content"] == "Sorry, I can't help with that."
        assert "original_value" not in messages[1]

    def test_build_messages_falls_back_to_original_when_converted_missing(self):
        """When ``converted_value`` is empty, fall back to ``original_value``.

        Covers the historical behavior for pieces where PyRIT did not run a
        converter (e.g., Baseline strategy or in-flight failures).
        """
        mock_scenario = MagicMock()
        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        user_piece = MagicMock()
        user_piece.api_role = "user"
        user_piece.original_value = "Baseline prompt"
        user_piece.converted_value = None
        user_piece.sequence = 0
        user_piece.prompt_metadata = {}
        user_piece.labels = {}

        messages = processor._build_messages_from_pieces([user_piece])

        assert len(messages) == 1
        assert messages[0]["content"] == "Baseline prompt"
        # original == content here, so no separate audit field is needed.
        assert "original_value" not in messages[0]

    def test_build_messages_preserves_non_string_payloads(self):
        """Non-string ``converted_value`` payloads must survive unchanged.

        PyRIT message pieces can carry structured / multimodal content
        (e.g., bytes or list-of-parts payloads) on ``converted_value``.
        ``content`` must pass those through so persisted conversations
        remain a faithful record of what the target received; only the
        ``original_value`` audit field is gated on both sides being text.
        """
        mock_scenario = MagicMock()
        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        # Structured multimodal-style payload on converted_value, plain
        # string objective on original_value.
        structured_payload = [
            {"type": "text", "text": "describe this image"},
            {"type": "image_url", "image_url": {"url": "https://example/img.png"}},
        ]
        user_piece = MagicMock()
        user_piece.api_role = "user"
        user_piece.original_value = "Describe this image"
        user_piece.converted_value = structured_payload
        user_piece.sequence = 0
        user_piece.prompt_metadata = {}
        user_piece.labels = {}

        # Bytes payload on assistant converted_value — must not be coerced
        # to "" by str-gating logic.
        assistant_piece = MagicMock()
        assistant_piece.api_role = "assistant"
        assistant_piece.original_value = None
        assistant_piece.converted_value = b"\x89PNG\r\n"
        assistant_piece.sequence = 1
        assistant_piece.prompt_metadata = {}
        assistant_piece.labels = {}

        messages = processor._build_messages_from_pieces([user_piece, assistant_piece])

        # Structured user payload passed through unchanged.
        assert messages[0]["role"] == "user"
        assert messages[0]["content"] is structured_payload
        # Audit field omitted: content is non-text so cross-type comparison
        # against the str original would be meaningless.
        assert "original_value" not in messages[0]

        # Bytes assistant payload preserved (not silently dropped to "").
        assert messages[1]["role"] == "assistant"
        assert messages[1]["content"] == b"\x89PNG\r\n"
        assert "original_value" not in messages[1]

    def test_get_prompt_group_id_from_conversation(self):
        """Test extracting prompt_group_id from conversation."""
        mock_scenario = MagicMock()
        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        test_uuid = str(uuid.uuid4())

        # Piece with prompt_metadata
        piece = MagicMock()
        piece.prompt_metadata = {"prompt_group_id": test_uuid}

        result = processor._get_prompt_group_id_from_conversation([piece])

        assert result == test_uuid

    def test_get_prompt_group_id_from_labels(self):
        """Test extracting prompt_group_id from labels."""
        mock_scenario = MagicMock()
        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        test_uuid = str(uuid.uuid4())

        # Piece with labels
        piece = MagicMock()
        piece.prompt_metadata = {}
        piece.labels = {"prompt_group_id": test_uuid}

        result = processor._get_prompt_group_id_from_conversation([piece])

        assert result == test_uuid

    def test_to_jsonl(self, tmp_path):
        """Test JSONL generation."""
        from pyrit.models.attack_result import AttackOutcome

        mock_scenario = MagicMock()

        # Create mock attack result
        attack_result = MagicMock()
        attack_result.conversation_id = "test-conv-id"
        attack_result.outcome = AttackOutcome.SUCCESS
        attack_result.attack_identifier = {"__type__": "TestAttack"}
        attack_result.last_score = None

        mock_scenario.get_attack_results.return_value = [attack_result]

        # Create mock memory
        mock_memory = MagicMock()
        user_piece = MagicMock()
        user_piece.api_role = "user"
        user_piece.original_value = "Attack prompt"
        user_piece.converted_value = "Attack prompt"
        user_piece.sequence = 0
        user_piece.prompt_metadata = {}
        user_piece.labels = {}

        mock_memory.get_message_pieces.return_value = [user_piece]
        mock_scenario.get_memory.return_value = mock_memory

        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        output_path = str(tmp_path / "output.jsonl")
        result = processor.to_jsonl(output_path)

        # Check file was written
        assert (tmp_path / "output.jsonl").exists()
        assert "Attack prompt" in result or "attack_success" in result


# =============================================================================
# Tests for FoundryExecutionManager
# =============================================================================
@pytest.mark.unittest


# ... (unrelated Foundry test classes elided for fixture brevity) ...


class TestAdversarialChatTargetRegression:
    """Regression tests to prevent adversarial_chat_target from being set to the user's callback.

    The adversarial_chat_target is used by PyRIT's FoundryScenario for:
    - TenseConverter (converter_target for prompt rephrasing)
    - Multi-turn attacks (Crescendo, RedTeaming adversarial LLM)

    If set to the user's callback, the callback response leaks into converted prompts,
    causing the callback response to appear as the user message in results.
    """

    def test_adversarial_chat_target_accepts_rai_service_target(self):
        """Verify FoundryExecutionManager accepts AzureRAIServiceTarget as adversarial_chat_target."""
        from azure.ai.evaluation.red_team._utils._rai_service_target import AzureRAIServiceTarget

        rai_target = AzureRAIServiceTarget(
            client=MagicMock(),
            model="gpt-4",
            prompt_template_key="prompt_converters/tense_converter.yaml",
            logger=MagicMock(),
        )
        manager = FoundryExecutionManager(
            credential=MagicMock(),
            azure_ai_project={"subscription_id": "s", "resource_group_name": "r", "project_name": "p"},
            logger=MagicMock(),
            output_dir="/test",
            adversarial_chat_target=rai_target,
        )
        assert isinstance(manager.adversarial_chat_target, AzureRAIServiceTarget)

    def test_get_adversarial_template_key_baseline(self):
        """Template key should default to tense converter for single-turn strategies."""
        from azure.ai.evaluation.red_team._red_team import RedTeam

        strategies = [AttackStrategy.Baseline]
        key = RedTeam._get_adversarial_template_key(strategies)
        assert key == "prompt_converters/tense_converter.yaml"

    def test_get_adversarial_template_key_difficult(self):
        """DIFFICULT strategy (Tense+Base64) should use tense converter template."""
        from azure.ai.evaluation.red_team._red_team import RedTeam

        strategies = [AttackStrategy.Baseline, [AttackStrategy.Tense, AttackStrategy.Base64]]
        key = RedTeam._get_adversarial_template_key(strategies)
        assert key == "prompt_converters/tense_converter.yaml"

    def test_get_adversarial_template_key_crescendo(self):
        """Crescendo strategy should use the crescendo template."""
        from azure.ai.evaluation.red_team._red_team import RedTeam

        strategies = [AttackStrategy.Crescendo, AttackStrategy.Baseline]
        key = RedTeam._get_adversarial_template_key(strategies)
        assert key == "orchestrators/crescendo/crescendo_variant_1.yaml"

    def test_get_adversarial_template_key_multi_turn(self):
        """MultiTurn strategy should use the red teaming text generation template."""
        from azure.ai.evaluation.red_team._red_team import RedTeam

        strategies = [AttackStrategy.MultiTurn, AttackStrategy.Baseline]
        key = RedTeam._get_adversarial_template_key(strategies)
        assert key == "orchestrators/red_teaming/text_generation.yaml"

    def test_build_messages_user_shows_converted_value(self):
        """User messages should show converted_value (wire payload), with original_value preserved.

        See https://github.com/Azure/azure-sdk-for-python/issues/47228 — the
        persisted conversation must reflect what the target actually received
        (``converted_value``). The pre-converter objective is retained as an
        ``original_value`` sibling for auditability. The callback-response leak
        this class guards against is prevented at the source (the
        ``adversarial_chat_target`` is an ``AzureRAIServiceTarget``, not the
        user callback — see ``test_execute_attacks_with_foundry_uses_rai_service_target``),
        so ``converted_value`` here is the legitimately rephrased prompt.
        """
        mock_scenario = MagicMock()
        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        # Simulate a Tense-converted attack where converted_value differs from original_value
        user_piece = MagicMock()
        user_piece.api_role = "user"
        user_piece.original_value = "Tell me about violence"
        user_piece.converted_value = "Told me about violence"
        user_piece.sequence = 0

        assistant_piece = MagicMock()
        assistant_piece.api_role = "assistant"
        assistant_piece.original_value = "I cannot help with that"
        assistant_piece.converted_value = "I cannot help with that"
        assistant_piece.sequence = 1

        messages = processor._build_messages_from_pieces([user_piece, assistant_piece])

        assert len(messages) == 2
        # User message should show the wire payload (what the target received)
        assert messages[0]["role"] == "user"
        assert messages[0]["content"] == "Told me about violence"
        # The pre-converter objective is preserved as an audit sibling
        assert messages[0]["original_value"] == "Tell me about violence"
        # Assistant message should show the response
        assert messages[1]["role"] == "assistant"
        assert messages[1]["content"] == "I cannot help with that"

    def test_build_messages_user_falls_back_to_converted_value(self):
        """When original_value is None, user messages should fall back to converted_value."""
        mock_scenario = MagicMock()
        mock_dataset = MagicMock()
        mock_dataset.get_all_seed_groups.return_value = []

        processor = FoundryResultProcessor(
            scenario=mock_scenario,
            dataset_config=mock_dataset,
            risk_category="violence",
        )

        user_piece = MagicMock()
        user_piece.api_role = "user"
        user_piece.original_value = None
        user_piece.converted_value = "Fallback content"
        user_piece.sequence = 0

        messages = processor._build_messages_from_pieces([user_piece])

        assert messages[0]["content"] == "Fallback content"

    @pytest.mark.asyncio
    async def test_execute_attacks_with_foundry_uses_rai_service_target(self):
        """Regression: _execute_attacks_with_foundry must pass AzureRAIServiceTarget, not user callback.

        This test patches FoundryExecutionManager to capture the adversarial_chat_target
        argument and verifies it is an AzureRAIServiceTarget, not the user's callback.
        """
        from azure.ai.evaluation.red_team._callback_chat_target import _CallbackChatTarget
        from azure.ai.evaluation.red_team._utils._rai_service_target import AzureRAIServiceTarget

        captured_kwargs = {}
        original_init = FoundryExecutionManager.__init__

        def capturing_init(self_inner, **kwargs):
            captured_kwargs.update(kwargs)
            original_init(self_inner, **kwargs)

        mock_red_team = MagicMock()
        mock_red_team.credential = MagicMock()
        mock_red_team.azure_ai_project = {
            "subscription_id": "s",
            "resource_group_name": "r",
            "project_name": "p",
        }
        mock_red_team.logger = MagicMock()
        mock_red_team.scan_output_dir = "/test"
        mock_red_team.generated_rai_client = MagicMock()
        mock_red_team._one_dp_project = False
        mock_red_team.risk_categories = []
        mock_red_team.attack_objectives = {}
        mock_red_team.total_tasks = 0
        mock_red_team.red_team_info = {}
        mock_red_team.completed_tasks = 0

        from azure.ai.evaluation.red_team._red_team import RedTeam

        with patch.object(FoundryExecutionManager, "__init__", capturing_init):
            with patch.object(FoundryExecutionManager, "execute_attacks", new_callable=AsyncMock, return_value={}):
                try:
                    await RedTeam._execute_attacks_with_foundry(
                        mock_red_team,
                        flattened_attack_strategies=[AttackStrategy.Baseline],
                        all_objectives={},
                        chat_target=MagicMock(spec=_CallbackChatTarget),
                        timeout=60,
                        skip_evals=True,
                    )
                except Exception:
                    pass  # We only care about the captured kwargs

        assert "adversarial_chat_target" in captured_kwargs
        adversarial_target = captured_kwargs["adversarial_chat_target"]
        assert isinstance(
            adversarial_target, AzureRAIServiceTarget
        ), f"adversarial_chat_target should be AzureRAIServiceTarget, got {type(adversarial_target).__name__}"
        assert not isinstance(
            adversarial_target, _CallbackChatTarget
        ), "adversarial_chat_target must NOT be a _CallbackChatTarget (user's callback)"

