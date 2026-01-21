import json
import logging
from typing import Any, Dict, Set
from azure.ai.evaluation import SimilarityEvaluator, GroundednessEvaluator, ResponseCompletenessEvaluator
from .constants import QA_BOT_EVALS_WEIGHT, EVALUATION_PASS_FAIL_MAPPING


class AzureBotEvaluator:
    RESULT_KEY = "bot_evals"

    def __init__(
        self,
        model_config: dict[Any, Any],
        *,
        threshold: int = 3,
        weight: Dict[str, float] = QA_BOT_EVALS_WEIGHT,
        higher_is_better: bool = True,
        credential: Any | None = None,
        **kwargs: Any,
    ) -> None:
        self._similarity = SimilarityEvaluator(model_config=model_config, credential=credential)
        self._groundedness = GroundednessEvaluator(model_config=model_config, credential=credential)
        self._response_completion = ResponseCompletenessEvaluator(model_config=model_config, credential=credential)
        self._threshold = threshold
        self._higher_is_better = higher_is_better
        self._weight = weight

    
    def __call__(
        self,
        *,
        query: str,
        response: str,
        ground_truth: str,
    ) -> Dict[str, float]:
        base_key = f"{AzureBotEvaluator.RESULT_KEY}"
        result: dict[str, Any] = {}
        similarity = self._similarity(query=query, response=response, ground_truth=ground_truth)
        response_completion = self._response_completion(ground_truth=ground_truth, response=response)

        # Calculate weighted score including reference matching
        score_value = (
            float(similarity["similarity"]) * self._weight["similarity_weight"]
            + float(response_completion["response_completeness"]) * self._weight["response_completeness_weight"]
        )

        result[f"{base_key}"] = score_value
        result[f"{base_key}_similarity"] = similarity["similarity"]
        result[f"{base_key}_response_completeness"] = response_completion["response_completeness"]
        result[f"{base_key}_threshold"] = self._threshold

        result_key = f"{base_key}_result"
        if self._higher_is_better:
            if float(score_value) >= self._threshold:
                result[result_key] = EVALUATION_PASS_FAIL_MAPPING[True]
            else:
                result[result_key] = EVALUATION_PASS_FAIL_MAPPING[False]
        else:
            if float(score_value) <= self._threshold:
                result[result_key] = EVALUATION_PASS_FAIL_MAPPING[True]
            else:
                result[result_key] = EVALUATION_PASS_FAIL_MAPPING[False]

        logging.info(f"qa evl result: {json.dumps(result, ensure_ascii=False)}")
        return result
