from datetime import datetime
from typing import Optional

import prompty
import prompty.azure
from src._apiview import APIVIEW_COMMENT_SELECT_FIELDS, get_apiview_cosmos_client
from src._models import APIViewComment
from src._utils import get_prompt_path


def _calculate_language_adoption(start_date: str, end_date: str, environment: str = "production") -> dict:
    """
    Calculates the adoption rate of AI review comments by language.
    Looks at distinct ReviewIds that had new revisions created during the time period
    and calculates what percentage of those ReviewIds have AI comments.
    Returns a dictionary with languages as keys and adoption percentages as values.
    """
    # Get comments container client
    comments_client = get_apiview_cosmos_client(container_name="Comments", environment=environment)
    reviews_client = get_apiview_cosmos_client(container_name="Reviews", environment=environment)

    iso_start = datetime.strptime(start_date, "%Y-%m-%d").strftime("%Y-%m-%dT00:00:00Z")
    iso_end = datetime.strptime(end_date, "%Y-%m-%d").strftime("%Y-%m-%dT23:59:59.999999Z")

    # Query all comments in the date range to get active ReviewIds
    comments_query = """
    SELECT c.ReviewId, c.CreatedBy FROM c 
    WHERE c.CreatedOn >= @start_date AND c.CreatedOn <= @end_date
    """

    raw_comments = list(
        comments_client.query_items(
            query=comments_query,
            parameters=[{"name": "@start_date", "value": iso_start}, {"name": "@end_date", "value": iso_end}],
            enable_cross_partition_query=True,
        )
    )

    # Build set of active ReviewIds from comments
    active_reviews = set()
    for comment in raw_comments:
        review_id = comment.get("ReviewId")
        if review_id:
            active_reviews.add(review_id)

    # Find ReviewIds with AI comments
    ai_reviews = {
        comment["ReviewId"]
        for comment in raw_comments
        if comment.get("CreatedBy") == "azure-sdk" and comment.get("ReviewId")
    }

    # If no comments, try to get all reviews in the date range
    if not active_reviews:
        # Query all reviews in the date range
        reviews_query = """
        SELECT r.id, r.Language FROM r WHERE r.CreatedOn >= @start_date AND r.CreatedOn <= @end_date
        """
        batch_reviews = list(
            reviews_client.query_items(
                query=reviews_query,
                parameters=[{"name": "@start_date", "value": iso_start}, {"name": "@end_date", "value": iso_end}],
                enable_cross_partition_query=True,
            )
        )
        review_to_language = {}
        language_reviews = {}
        for review in batch_reviews:
            review_id = review.get("id")
            language = review.get("Language", "").lower()
            if language and review_id:
                review_to_language[review_id] = language
                if language not in language_reviews:
                    language_reviews[language] = set()
                language_reviews[language].add(review_id)
    else:
        # Query all reviews for active ReviewIds and get their languages
        review_to_language = {}
        language_reviews = {}
        batch_size = 100
        review_ids = list(active_reviews)
        for i in range(0, len(review_ids), batch_size):
            batch_ids = review_ids[i : i + batch_size]
            reviews_query = """
            SELECT r.id, r.Language FROM r WHERE ARRAY_CONTAINS(@review_ids, r.id)
            """
            batch_reviews = list(
                reviews_client.query_items(
                    query=reviews_query,
                    parameters=[{"name": "@review_ids", "value": batch_ids}],
                    enable_cross_partition_query=True,
                )
            )
            for review in batch_reviews:
                review_id = review.get("id")
                language = review.get("Language", "").lower()
                if language and review_id:
                    review_to_language[review_id] = language
                    if language not in language_reviews:
                        language_reviews[language] = set()
                    language_reviews[language].add(review_id)

    # Calculate adoption rate and counts per language
    adoption_stats = {}
    for language, review_ids in language_reviews.items():
        total_reviews = len(review_ids)
        reviews_with_ai_comments = sum(1 for review_id in review_ids if review_id in ai_reviews)
        adoption_rate = reviews_with_ai_comments / total_reviews if total_reviews > 0 else 0.0
        adoption_stats[language] = {
            "adoption_rate": f"{adoption_rate:.2f}",
            "active_reviews": total_reviews,
            "active_copilot_reviews": reviews_with_ai_comments,
        }

    return adoption_stats


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
    iso_start = None
    iso_end = None
    for date_str, label in zip([start_date, end_date], ["start_date", "end_date"]):
        try:
            dt = datetime.strptime(date_str, "%Y-%m-%d")
            if label == "start_date":
                # Start of day
                iso_start = dt.strftime("%Y-%m-%dT00:00:00Z")
            else:
                # End of day (max time)
                iso_end = dt.strftime("%Y-%m-%dT23:59:59.999999Z")
        except ValueError:
            bad_dates.append(date_str)
    if bad_dates:
        print(f"ValueError: Dates must be in YYYY-MM-DD format. Invalid date(s) found: {', '.join(bad_dates)}")
        return

    comments_client = get_apiview_cosmos_client(container_name="Comments", environment=environment)
    query = f"""
    SELECT {', '.join(APIVIEW_COMMENT_SELECT_FIELDS)} FROM c
    WHERE c.CreatedOn >= @start_date AND c.CreatedOn <= @end_date
    """
    # retrieve comments created between start_date and end_date (ISO 8601)
    raw_comments = list(
        comments_client.query_items(
            query=query,
            parameters=[{"name": "@start_date", "value": iso_start}, {"name": "@end_date", "value": iso_end}],
            enable_cross_partition_query=True,
        )
    )
    comments = [APIViewComment(**d) for d in raw_comments]

    # Calculate language adoption
    language_adoption = _calculate_language_adoption(start_date, end_date, environment=environment)

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
