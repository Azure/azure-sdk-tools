from dataclasses import asdict, dataclass, field
from datetime import date
from typing import Dict, Optional

from src._apiview import (
    ActiveReviewMetadata,
    ActiveRevisionMetadata,
    get_active_reviews,
    get_comments_in_date_range,
)
from src._database_manager import get_database_manager
from src._models import APIViewComment
from src._utils import run_prompty


@dataclass
class MetricsSegment:
    """
    Represents one time-window ("segment") of Copilot metrics
    for a specific dimension (e.g., language) or for all (dimension = {}).
    """

    start_date: date
    end_date: date
    segment_days: Optional[int] = None

    # Core review counts
    active_review_count: Optional[int] = None
    active_copilot_review_count: Optional[int] = None

    # Human comment counts
    human_comment_count_with_ai: Optional[int] = None
    human_comment_count_without_ai: Optional[int] = None

    # AI comment classifications
    upvoted_ai_comment_count: Optional[int] = None
    neutral_ai_comment_count: Optional[int] = None
    downvoted_ai_comment_count: Optional[int] = None

    # Dimension (e.g., {"language": "python"}) or {} for "All"
    dimension: Dict[str, str] = field(default_factory=dict)

    # Cosmos metadata
    type: str = field(default="metrics_segment", init=False)
    id: Optional[str] = None
    pk: Optional[str] = None

    def compute_ids(self):
        """Generate deterministic CosmosDB ID and partition key, lowercasing dimension values and normalizing language."""
        start = self.start_date
        end = self.end_date

        def normalize_dim_item(k, v):
            k = k.lower()
            v = v.lower()
            if k == "language" and v == "c#":
                v = "csharp"
            return f"{k}={v}"

        if not self.dimension:
            dim_key = "dim:all"
        else:
            dim_key = "dim:" + ";".join(normalize_dim_item(k, v) for k, v in sorted(self.dimension.items()))
        # Use ISO strings for stable IDs
        self.id = f"{start.isoformat()}|{end.isoformat()}|{dim_key}"
        self.pk = start.strftime("%Y_%m")

    def to_dict(self) -> Dict:
        """Return JSON-serializable dict for CosmosDB upsert."""
        if not self.id or not self.pk:
            self.compute_ids()
        d = asdict(self)
        d["start_date"] = self.start_date.isoformat()
        d["end_date"] = self.end_date.isoformat()
        return d

    def __post_init__(self):
        """Normalize dates, compute segment_days, and always set id/pk."""
        # Normalize to date objects if strings were passed
        if isinstance(self.start_date, str):
            self.start_date = date.fromisoformat(self.start_date)
        if isinstance(self.end_date, str):
            self.end_date = date.fromisoformat(self.end_date)

        # Compute inclusive segment days (at least 1)
        delta_days = (self.end_date - self.start_date).days + 1
        self.segment_days = max(1, delta_days)


def get_metrics_report(
    start_date: str, end_date: str, environment: str, markdown: bool = False, save: bool = False
) -> Optional[dict]:
    data = _build_metrics_data(start_date=start_date, end_date=end_date, environment=environment)
    if not data:
        raise ValueError("No data found for metrics report")
    if save:
        db_manager = get_database_manager()
        cosmos_client = db_manager.get_container_client("metrics")
        for doc in data.values():
            # do not save language-agnostic overall metrics to CosmosDB. PowerBI will calculate these.
            if not doc.dimension.get("language", None):
                continue
            try:
                cosmos_client.upsert(doc.id, data=doc.to_dict())
            except Exception as e:
                print(f"Error upserting document {doc.id}: {e}")
    report = _build_metrics_report(data)
    if markdown:
        inputs = {"data": report}
        summary = run_prompty(folder="other", filename="summarize_metrics", inputs=inputs)
        print(summary)
    else:
        return report


def _build_metrics_segment(
    *,
    start_date: str,
    end_date: str,
    reviews: list[ActiveReviewMetadata],
    comments: list[APIViewComment],
    language: Optional[str] = None,
) -> MetricsSegment:
    metrics = MetricsSegment(start_date=start_date, end_date=end_date)
    metrics.type = "metrics_segment"

    if language:
        metrics.dimension = {"language": language}
    else:
        metrics.dimension = {}
    # need language set to compute IDs
    metrics.compute_ids()

    # Extract approved revision metadata from get_active_reviews results
    # Get only revisions that were approved during the period
    approved_revisions = [rev for r in reviews for rev in r.revisions if rev.approval is not None]

    metrics.active_review_count = len(approved_revisions)
    metrics.active_copilot_review_count = sum(1 for rev in approved_revisions if rev.has_copilot_review)

    # Build a mapping of revision_id -> has_copilot for all approved revisions
    revision_has_copilot = {}
    for rev in approved_revisions:
        for revision_id in rev.revision_ids:
            revision_has_copilot[revision_id] = rev.has_copilot_review

    approved_revision_ids = set(revision_has_copilot.keys())

    # Filter comments to only those belonging to approved revisions, excluding Diagnostic
    approved_revision_comments = [
        c for c in comments if c.api_revision_id in approved_revision_ids and c.comment_source != "Diagnostic"
    ]

    # Categorize comments based on whether the revision has Copilot
    ai_comments_with_copilot = []
    human_comments_with_copilot = []
    human_comments_without_copilot = []

    for comment in approved_revision_comments:
        has_copilot = revision_has_copilot.get(comment.api_revision_id, False)

        if has_copilot:
            if comment.comment_source == "AIGenerated":
                ai_comments_with_copilot.append(comment)
            else:
                human_comments_with_copilot.append(comment)
        else:
            # For revisions without Copilot, all comments should be human
            if comment.comment_source != "AIGenerated":
                human_comments_without_copilot.append(comment)

    metrics.human_comment_count_with_ai = len(human_comments_with_copilot)
    metrics.human_comment_count_without_ai = len(human_comments_without_copilot)

    # Count AI comments only from approved revisions with Copilot (excluding deleted)
    ai_comments_to_count = [c for c in ai_comments_with_copilot if not c.is_deleted]

    # Categorize AI comments by vote status
    upvoted_ai_comments = [c for c in ai_comments_to_count if c.upvotes]
    downvoted_ai_comments = [c for c in ai_comments_to_count if c.downvotes]
    neutral_ai_comments = [c for c in ai_comments_to_count if not c.upvotes and not c.downvotes]

    metrics.upvoted_ai_comment_count = len(upvoted_ai_comments)
    metrics.neutral_ai_comment_count = len(neutral_ai_comments)
    metrics.downvoted_ai_comment_count = len(downvoted_ai_comments)

    return metrics


def _calculate_adoption_rate(segment: MetricsSegment) -> float:
    """Calculate the adoption rate of Copilot in a given segment."""
    if segment.active_review_count == 0:
        return 0.0
    return segment.active_copilot_review_count / segment.active_review_count


def _calculate_ai_comment_count(segment: MetricsSegment) -> int:
    """Calculate the total number of AI comments."""
    return segment.upvoted_ai_comment_count + segment.downvoted_ai_comment_count + segment.neutral_ai_comment_count


def _calculate_good_comment_rate(segment: MetricsSegment) -> float:
    """Calculate the rate of good AI comments."""
    total = _calculate_ai_comment_count(segment)
    if total == 0:
        return 0.0
    return segment.upvoted_ai_comment_count / total


def _calculate_bad_comment_rate(segment: MetricsSegment) -> float:
    """Calculate the rate of bad AI comments."""
    total = _calculate_ai_comment_count(segment)
    if total == 0:
        return 0.0
    return segment.downvoted_ai_comment_count / total


def _calculate_neutral_comment_rate(segment: MetricsSegment) -> float:
    """Calculate the rate of neutral AI comments."""
    total = _calculate_ai_comment_count(segment)
    if total == 0:
        return 0.0
    return segment.neutral_ai_comment_count / total


def _calculate_ai_comment_rate(segment: MetricsSegment) -> float:
    """Calculate the rate of AI comments."""
    ai_count = _calculate_ai_comment_count(segment)
    total = ai_count + segment.human_comment_count_with_ai
    if total == 0:
        return 0.0
    return ai_count / total


def _build_metrics_report(data: Dict[str, MetricsSegment]) -> dict:
    """Build a metrics report from the raw data."""
    start_date = data["overall"].start_date
    end_date = data["overall"].end_date

    report = {"start_date": start_date, "end_date": end_date, "metrics": {}}

    def fmt(val):
        return round(val, 2) if isinstance(val, float) else val

    for language, segment in data.items():
        report["metrics"][language] = {
            "adoption": {
                "active_review_count": segment.active_review_count,
                "copilot_review_count": segment.active_copilot_review_count,
                "adoption_rate": fmt(_calculate_adoption_rate(segment)),
            },
            "comment_quality": {
                "ai_comment_count": _calculate_ai_comment_count(segment),
                "good": fmt(_calculate_good_comment_rate(segment)),
                "bad": fmt(_calculate_bad_comment_rate(segment)),
                "neutral": fmt(_calculate_neutral_comment_rate(segment)),
            },
            "comment_makeup": {
                "human_comment_count_without_copilot": segment.human_comment_count_without_ai,
                "human_comment_count_with_ai": segment.human_comment_count_with_ai,
                "ai_comment_count": _calculate_ai_comment_count(segment),
                "ai_comment_rate": fmt(_calculate_ai_comment_rate(segment)),
            },
        }
    return report


def _build_metrics_data(start_date: str, end_date: str, environment: str) -> Optional[Dict[str, MetricsSegment]]:
    """Package metrics data for a report or publishing."""
    # filter out C and C++ since they are not supported by Copilot
    # See: https://github.com/Azure/azure-sdk-tools/issues/10465
    pretty_languages_to_omit = ["c++", "c", "typespec", "swagger", "xml"]
    active_reviews = get_active_reviews(
        start_date, end_date, environment=environment, omit_languages=pretty_languages_to_omit
    )
    raw_comments = get_comments_in_date_range(start_date, end_date, environment=environment)
    all_comments = [APIViewComment(**d) for d in raw_comments]
    results = {}
    results["overall"] = _build_metrics_segment(
        start_date=start_date, end_date=end_date, reviews=active_reviews, comments=all_comments
    )

    languages_to_package = {r.language for r in active_reviews}
    for language in languages_to_package:
        filtered_reviews = [r for r in active_reviews if r.language == language]
        results[language] = _build_metrics_segment(
            start_date=start_date, end_date=end_date, reviews=filtered_reviews, comments=all_comments, language=language
        )

    return results
