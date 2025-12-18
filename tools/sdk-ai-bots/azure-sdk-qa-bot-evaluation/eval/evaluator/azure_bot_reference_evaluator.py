from typing import Any, Set
from .constants import EVALUATION_PASS_FAIL_MAPPING

class AzureBotReferenceEvaluator:
    RESULT_KEY = "reference_match"
    def __init__(self, threshold: float = 1.0, higher_is_better: bool = True):
        self._threshold = threshold
        self._higher_is_better = higher_is_better

    def _normalize_url(self, url: str) -> str:
        """Normalize URL for comparison by removing fragments, anchors, and common variations."""
        from urllib.parse import urlparse, urlunparse

        parsed = urlparse(url)
        # Normalize scheme and netloc to lowercase
        scheme = parsed.scheme.lower()
        netloc = parsed.netloc.lower()

        # Remove 'www.' prefix if present
        if netloc.startswith("www."):
            netloc = netloc[4:]

        # Remove trailing slash from path
        path = parsed.path.rstrip("/")
        if not path:
            path = "/"

        # Remove fragment (anchor) - this removes #section, #line-numbers, etc.
        # Remove query parameters that might indicate line numbers or sections
        query = parsed.query
        if query:
            # Filter out common line/section indicators from query params
            filtered_params = []
            for param in query.split("&"):
                if "=" in param:
                    key, value = param.split("=", 1)
                    # Skip parameters that typically indicate line numbers or sections
                    if key.lower() not in ["line", "lines", "section", "anchor", "highlight"]:
                        filtered_params.append(param)
                else:
                    # Keep parameters without values if they're not line indicators
                    if param.lower() not in ["line", "lines", "section", "anchor", "highlight"]:
                        filtered_params.append(param)
            query = "&".join(filtered_params) if filtered_params else ""

        # Reconstruct without fragments and filtered query
        normalized = urlunparse((scheme, netloc, path, parsed.params, query, ""))
        return normalized

    def _get_reference_matches(self, expected: list[str], actual: list[str]) -> tuple[Set, Set, Set, float]:
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
    
    def __call__(self, reference_urls: list[str], expected_reference_urls: list[str] | None = None):
        # Calculate reference matching if expected references are provided
        reference_match_score = 1.0  # Default to perfect match if no expected references

        result: dict[str, Any] = {}
        base_key = f"{AzureBotReferenceEvaluator.RESULT_KEY}"
        if expected_reference_urls:
            exact_matches, unexpected_refs, missing_refs, match_percentage = self._get_reference_matches(
                expected_reference_urls, reference_urls
            )
            reference_match_score = match_percentage
            result[f"{base_key}"] = match_percentage
            result[f"{base_key}_exact_matches"] = list(exact_matches)
            result[f"{base_key}_unexpected_refs"] = list(unexpected_refs)
            result[f"{base_key}_missing_refs"] = list(missing_refs)
        else:
            result[f"{base_key}"] = reference_match_score
        
        result_key = f"{base_key}_result"
        if self._higher_is_better:
            if float(reference_match_score) >= self._threshold:
                result[result_key] = EVALUATION_PASS_FAIL_MAPPING[True]
            else:
                result[result_key] = EVALUATION_PASS_FAIL_MAPPING[False]
        else:
            if float(reference_match_score) <= self._threshold:
                result[result_key] = EVALUATION_PASS_FAIL_MAPPING[True]
            else:
                result[result_key] = EVALUATION_PASS_FAIL_MAPPING[False]
        return result
