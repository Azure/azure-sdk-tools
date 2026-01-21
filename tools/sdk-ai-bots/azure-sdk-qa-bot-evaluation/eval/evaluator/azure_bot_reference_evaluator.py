from dataclasses import dataclass
from typing import Any, Dict, Set
from .constants import EVALUATION_PASS_FAIL_MAPPING

@dataclass
class AzureBotReference:
    def __init__(self, title: str, url: str):
        self._title = title
        self._url = url
    
    @property
    def title(self) -> str:
        """Getter for title"""
        return self._title

    @title.setter
    def title(self, value: str) -> None:
        """Setter for title"""
        self._title = value

    @property
    def url(self) -> str:
        """Getter for url"""
        return self._url

    @title.setter
    def url(self, value: str) -> None:
        """Setter for url"""
        self._url = value

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
    def _get_reference_matches(self, expected: list[Dict[str, Any]], actual: list[Dict[str, Any]]) -> tuple[Set, Set, Set, float]:
        """Compare reference between expected and actual lists."""
        # expected_in_actual_map = {ref: False for ref in expected}
        # actual_in_expected_map = {ref: False for ref in actual}

        # for expected_ref in expected:
        #     for actual_ref in actual:
        #         if (expected_ref.title == actual_ref.title and self._normalize_url(expected_ref.url) == self._normalize_url(actual_ref.url)):
        #             expected_in_actual_map[expected_ref] = True
        #             actual_in_expected_map[actual_ref] = True
        
        expected_in_actual = []
        actual_in_expected = []

        for expected_ref in expected:
            for actual_ref in actual:
                if (expected_ref["title"] == actual_ref["title"] and self._normalize_url(expected_ref["url"]) == self._normalize_url(actual_ref["url"])):
                    expected_in_actual.add(expected_ref)
                    actual_in_expected.add(actual_ref)

        exact_matches = expected_in_actual
        #missing_refs = expected - exact_matches
        missing_refs = [ref for ref in expected if ref not in exact_matches]
        # unexpected_refs = actual - actual_in_expected
        unexpected_refs = [ref for ref in actual if ref not in actual_in_expected]

        # Calculate match percentage based on expected URLs
        if len(expected) == 0:
            match_percentage = 1.0  # 100% if no references expected
        else:
            match_percentage = len(exact_matches) / len(expected)

        return exact_matches, unexpected_refs, missing_refs, match_percentage

    def _get_reference_matches2(self, expected: list[str], actual: list[str]) -> tuple[Set, Set, Set, float]:
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
    
    def __call__(self, references: list[str], expected_references: list[str] | None = None):
        # Calculate reference matching if expected references are provided
        reference_match_score = 1.0  # Default to perfect match if no expected references

        result: dict[str, Any] = {}
        base_key = f"{AzureBotReferenceEvaluator.RESULT_KEY}"
        if expected_references:
            exact_matches, unexpected_refs, missing_refs, match_percentage = self._get_reference_matches(
                expected_references, references
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
