from typing import Optional

import prompty
import prompty.azure
from src._apiview import (
    get_active_reviews,
    get_comments_in_date_range,
)
from src._models import APIViewComment
from src._utils import get_prompt_path, to_epoch_seconds


class AdoptionMetric:
    """Helper class to track adoption metrics."""

    def __init__(self):
        self.total_count = 0
        self.copilot_count = 0

    @property
    def adoption_rate(self) -> float:
        if self.total_count == 0:
            return 0.0
        return self.copilot_count / self.total_count

    def as_dict(self) -> dict:
        return {
            "adoption_rate": f"{self.adoption_rate:.2f}",
            "active_reviews": self.total_count,
            "active_copilot_reviews": self.copilot_count,
        }


def _calculate_language_adoption(start_date: str, end_date: str, environment: str = "production") -> dict:
    """
    Calculates the adoption rate of AI review comments by language.
    Looks at distinct ReviewIds that had new revisions created during the time period
    and calculates what percentage of those ReviewIds have AI comments.
    Returns a dictionary with languages as keys and adoption percentages as values.
    """

    active_reviews = get_active_reviews(start_date, end_date, environment=environment)
    active_review_ids = {r.review_id for r in active_reviews}
    ai_review_ids = set()
    comments = get_comments_in_date_range(start_date, end_date, environment=environment)

    # identify which active reviews have AI comments
    for comment in comments:
        review_id = comment.get("ReviewId")
        created_by = comment.get("CreatedBy")
        if review_id and created_by == "azure-sdk":
            ai_review_ids.add(review_id)

    # convert from set to list
    ai_review_ids = list(ai_review_ids)

    # validate that ai_review_ids is a subset of active_review_ids
    assert set(ai_review_ids).issubset(active_review_ids), "AI review IDs must be a subset of active review IDs"

    # filter out C and C++ since they are not supported by Copilot
    # See: https://github.com/Azure/azure-sdk-tools/issues/10465
    pretty_languages_to_omit = ["c++", "c", "typespec", "swagger", "xml"]
    active_reviews = [r for r in active_reviews if r.language.lower() not in pretty_languages_to_omit]

    # Calculate adoption rate and counts per language
    adoption_stats: dict[str, AdoptionMetric] = {}
    for item in active_reviews:
        has_ai_comments = item.review_id in ai_review_ids
        if item.language not in adoption_stats:
            adoption_stats[item.language] = AdoptionMetric()
        if has_ai_comments:
            adoption_stats[item.language].copilot_count += 1
        adoption_stats[item.language].total_count += 1

    # convert to dict form for easier serialization
    output = {}
    for language, stats in list(adoption_stats.items()):
        output[language.lower()] = stats.as_dict()
    return output


def _calculate_ai_vs_manual_comment_ratio(comments: list[APIViewComment]) -> float:
    """
    Calculates the ratio of AI-generated comments to manual comments.
    """
    ai_count = 0
    manual_count = 0
    for comment in comments:
        if comment.created_by == "azure-sdk":
            ai_count += 1
        else:
            manual_count += 1
    return ai_count / manual_count if manual_count > 0 else float("inf") if ai_count > 0 else 0.0


def _calculate_good_vs_bad_comment_ratio(comments: list[APIViewComment]) -> float:
    """
    Calculates the ratio of AI-generated comments with a thumbs-up compared to comments with a thumbs-down.
    """
    good_count = 0
    neutral_count = 0
    bad_count = 0
    ai_comments = [c for c in comments if c.created_by == "azure-sdk"]
    for comment in ai_comments:
        good_count += len(comment.upvotes)
        bad_count += len(comment.downvotes)
        if not comment.upvotes and not comment.downvotes:
            neutral_count += 1
    return good_count / bad_count if bad_count > 0 else float("inf") if good_count > 0 else 0.0


def get_metrics_report(start_date: str, end_date: str, environment: str, markdown: bool = False) -> Optional[dict]:
    """Generate a metrics report for a date range."""
    # validate that start_date and end_date are in YYYY-MM-DD format
    bad_dates = []
    for date_str in [start_date, end_date]:
        try:
            to_epoch_seconds(date_str)
        except ValueError:
            bad_dates.append(date_str)
    if bad_dates:
        print(f"ValueError: Dates must be in YYYY-MM-DD format. Invalid date(s) found: {', '.join(bad_dates)}")
        return

    # Calculate language adoption
    language_adoption = _calculate_language_adoption(start_date, end_date, environment=environment)

    raw_comments = get_comments_in_date_range(start_date, end_date, environment=environment)
    comments = [APIViewComment(**d) for d in raw_comments]
    report = {
        "start_date": start_date,
        "end_date": end_date,
        "metrics": {
            "ai_vs_manual_comment_ratio": _calculate_ai_vs_manual_comment_ratio(comments),
            "good_vs_bad_comment_ratio": _calculate_good_vs_bad_comment_ratio(comments),
            "language_adoption": language_adoption,
        },
    }
    if markdown:
        prompt_path = get_prompt_path(folder="other", filename="summarize_metrics")
        inputs = {"data": report}
        summary = prompty.execute(prompt_path, inputs=inputs)
        print(summary)
    else:
        return report
