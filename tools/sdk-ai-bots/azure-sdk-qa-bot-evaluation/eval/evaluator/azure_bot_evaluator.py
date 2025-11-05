import json
import logging
from typing import Dict, Any, Set
import copy
from azure.ai.evaluation import evaluate, SimilarityEvaluator, GroundednessEvaluator, ResponseCompletenessEvaluator
from .constants import QA_BOT_EVALS_WEIGHT, EVALUATION_PASS_FAIL_MAPPING

class AzureBotEvaluator:
    RESULT_KEY = "bot_evals"
    
    def __init__(self, model_config, *, threshold=3, weight:Dict[str, float] = QA_BOT_EVALS_WEIGHT, higher_is_better: bool = True, credential=None, **kwargs):
        self._similarity = SimilarityEvaluator(model_config=model_config)
        self._groundedness = GroundednessEvaluator(model_config=model_config)
        self._response_completion = ResponseCompletenessEvaluator(model_config=model_config)
        self._threshold = threshold
        self._higher_is_better = higher_is_better
        self._weight = weight
    
    def _normalize_url(self, url: str) -> str:
        """Normalize URL for comparison by removing fragments, anchors, and common variations."""
        from urllib.parse import urlparse, urlunparse
        
        parsed = urlparse(url)
        # Normalize scheme and netloc to lowercase
        scheme = parsed.scheme.lower()
        netloc = parsed.netloc.lower()
        
        # Remove 'www.' prefix if present
        if netloc.startswith('www.'):
            netloc = netloc[4:]
            
        # Remove trailing slash from path
        path = parsed.path.rstrip('/')
        if not path:
            path = '/'
            
        # Remove fragment (anchor) - this removes #section, #line-numbers, etc.
        # Remove query parameters that might indicate line numbers or sections
        query = parsed.query
        if query:
            # Filter out common line/section indicators from query params
            filtered_params = []
            for param in query.split('&'):
                if '=' in param:
                    key, value = param.split('=', 1)
                    # Skip parameters that typically indicate line numbers or sections
                    if key.lower() not in ['line', 'lines', 'section', 'anchor', 'highlight']:
                        filtered_params.append(param)
                else:
                    # Keep parameters without values if they're not line indicators
                    if param.lower() not in ['line', 'lines', 'section', 'anchor', 'highlight']:
                        filtered_params.append(param)
            query = '&'.join(filtered_params) if filtered_params else ''
            
        # Reconstruct without fragments and filtered query
        normalized = urlunparse((scheme, netloc, path, parsed.params, query, ''))
        return normalized

    def _get_refence_matches(self, expected: list[str], actual: list[str]) -> tuple[Set, Set, Set, float]:
        """Compare reference URLs between expected and actual lists with normalized comparison."""
        # Create mappings from normalized URL to original URL
        expected_map = {self._normalize_url(url): url for url in expected}
        actual_map = {self._normalize_url(url): url for url in actual}
        
        # Get normalized sets for comparison
        expected_normalized = set(expected_map.keys())
        actual_normalized = set(actual_map.keys())
        
        # Find matches based on normalized URLs
        matched_normalized = expected_normalized.intersection(actual_normalized)
        
        # Convert back to original URLs for results
        exact_matches = {expected_map[norm_url] for norm_url in matched_normalized}
        unexpected_refs = {actual_map[norm_url] for norm_url in (actual_normalized - expected_normalized)}
        missing_refs = {expected_map[norm_url] for norm_url in (expected_normalized - actual_normalized)}

        # Calculate match percentage based on expected URLs
        if len(expected_normalized) == 0:
            match_percentage = 1.0  # 100% if no references expected
        else:
            match_percentage = len(matched_normalized) / len(expected_normalized)
        
        return exact_matches, unexpected_refs, missing_refs, match_percentage

    
    def __call__(self, *, query: str, response: str, ground_truth: str, reference_urls: list[str], expected_reference_urls: list[str] = None) -> Dict[str, float]:
        similarity = self._similarity(query=query, response=response, ground_truth=ground_truth)
        # groundness = self._groundedness(response=response, context=context, query=query)
        response_completion = self._response_completion(ground_truth=ground_truth, response=response)
        
        # Calculate reference matching if expected references are provided
        reference_match_score = 1.0  # Default to perfect match if no expected references
        match_percentage = 1.0  # Default value

        result = {}
        base_key = f"{AzureBotEvaluator.RESULT_KEY}"
        if expected_reference_urls:
            exact_matches, unexpected_refs, missing_refs, match_percentage = self._get_refence_matches(expected_reference_urls, reference_urls)
            reference_match_score = match_percentage
            result[f"{base_key}_reference_match"] = match_percentage
            result[f"{base_key}_exact_matches"] = list(exact_matches)
            result[f"{base_key}_unexpected_refs"] = list(unexpected_refs)
            result[f"{base_key}_missing_refs"] = list(missing_refs)
        
        # Calculate weighted score including reference matching
        # reference_weight = self._weight.get("reference_weight", 0.0)
        score_value = float(similarity["similarity"]) * self._weight["similarity_weight"] + float(response_completion["response_completeness"]) * self._weight["response_completeness_weight"]
        
        result[f"{base_key}"] = score_value
        result[f"{base_key}_similarity"] = similarity["similarity"]
        result[f"{base_key}_response_completeness"] = response_completion["response_completeness"]
        result[f"{base_key}_threshold"] = self._threshold

        result_key = f"{AzureBotEvaluator.RESULT_KEY}_result"
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