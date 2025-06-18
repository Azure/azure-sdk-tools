import json
from semantic_kernel.functions import kernel_function
from src._apiview_reviewer import ApiViewReview, ApiViewReviewMode


class ApiReviewPlugin:

    @kernel_function(description="Perform an API review on a single API.")
    async def review_api(self, *, language: str, target: str):
        """
        Perform an API review on a single API.
        Args:
            language (str): The programming language of the APIs.
            target (str): The target (new) API to review.
        """
        reviewer = ApiViewReview(target, None, language=language, mode=ApiViewReviewMode.FULL)
        results = reviewer.run()
        return json.dumps(results.model_dump(), indent=2)

    @kernel_function(description="Perform an API review on a diff between two APIs.")
    async def review_api_diff(self, *, language: str, target: str, base: str):
        """
        Perform an API review on a diff between two APIs.
        Args:
            language (str): The programming language of the APIs.
            target (str): The target (new) API to review.
            base (str): The base (old) API to compare against.
        """
        reviewer = ApiViewReview(target, base, language=language, mode=ApiViewReviewMode.DIFF)
        results = reviewer.run()
        return json.dumps(results.model_dump(), indent=2)
