import uuid
from dataclasses import asdict, dataclass, field
from datetime import date
from typing import Dict, Optional

import prompty
import prompty.azure
from src._apiview import (
    get_active_reviews,
    get_comments_in_date_range,
)
from src._database_manager import get_database_manager
from src._models import APIViewComment, CosmosMetricDocument
from src._utils import get_prompt_path, to_epoch_seconds


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
        """Generate deterministic CosmosDB ID and partition key."""
        start = self.start_date
        end = self.end_date

        dim_key = (
            "dim:all"
            if not self.dimension
            else "dim:" + ";".join(f"{k}={v}" for k, v in sorted(self.dimension.items()))
        )
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
        """Normalize dates and compute segment_days automatically."""
        # Normalize to date objects if strings were passed
        if isinstance(self.start_date, str):
            self.start_date = date.fromisoformat(self.start_date)
        if isinstance(self.end_date, str):
            self.end_date = date.fromisoformat(self.end_date)

        # Compute inclusive segment days (at least 1)
        delta_days = (self.end_date - self.start_date).days + 1
        self.segment_days = max(1, delta_days)


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


def _generate_cosmosdb_documents(data: dict) -> list[dict]:
    """Convert the report into CosmosDB metrics documents."""
    documents = []
    try:
        start_date = to_epoch_seconds(data["start_date"])
        end_date = to_epoch_seconds(data["end_date"], end_of_day=True)
        metrics = data["metrics"]
        for metric_name, metric_data in metrics.items():
            if metric_name == "language_adoption":
                # language_adoption is a dict of dicts, so we need to create a document for each language
                for language, lang_data in metric_data.items():
                    language = "csharp" if language == "c#" else language
                    doc = {
                        "id": f"{metric_name}_{language}-{start_date}-{end_date}",
                        "pk": f"{metric_name}_{language}",
                        "metric_name": f"{metric_name}_{language}",
                        "dimensions": {"language": language},
                        "period": {
                            "anchor": start_date,
                            "index": 0,  # triweek index since anchor (0 for the first window)
                            "start_epoch_s": start_date,
                            "end_epoch_s": end_date,
                            "label": f"{start_date}_to_{end_date}",
                        },
                        "label": f"{start_date}_to_{end_date}",
                        "values": lang_data,
                        "updated_at_epoch_s": end_date,
                    }
                    # validate against Pydantic model
                    cosmos_doc = CosmosMetricDocument(**doc)
                    documents.append(cosmos_doc.model_dump())
            else:
                # For other metrics, create a single document
                doc = {
                    "id": f"{metric_name}-{start_date}-{end_date}",
                    "pk": metric_name,
                    "metric_name": metric_name,
                    "dimensions": {},
                    "period": {
                        "anchor": start_date,
                        "index": 0,  # triweek index since anchor (0 for the first window)
                        "start_epoch_s": start_date,
                        "end_epoch_s": end_date,
                        "label": f"{start_date}_to_{end_date}",
                    },
                    "label": f"{start_date}_to_{end_date}",
                    "values": {metric_name: metric_data},
                    "updated_at_epoch_s": end_date,
                }
                # validate against Pydantic model
                cosmos_doc = CosmosMetricDocument(**doc)
                documents.append(cosmos_doc.model_dump())
    except Exception as e:
        print(f"Error generating CosmosDB documents: {e}")
    return documents


def get_metrics_report(
    start_date: str, end_date: str, environment: str, markdown: bool = False, save: bool = False
) -> Optional[Dict[str, MetricsSegment]]:
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

    overall_metrics = MetricsSegment(start_date=start_date, end_date=end_date)

    active_reviews = get_active_reviews(start_date, end_date, environment=environment)
    active_review_ids = {r.review_id for r in active_reviews}
    ai_review_ids = set()
    comments = get_comments_in_date_range(start_date, end_date, environment=environment)

    raise NotImplementedError("Not finished!")

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

    if save:
        # documents = _generate_cosmosdb_documents(report)
        # TODO: Revert. Just to see how queryable the data is in untransformed form.
        report["id"] = str(uuid.uuid4())
        documents = [report]
        if documents:
            db_manager = get_database_manager()
            cosmos_client = db_manager.get_container_client("metrics")
            for doc in documents:
                try:
                    cosmos_client.upsert(doc["id"], data=doc)
                except Exception as e:
                    # FIXME: debug issues here...
                    print(f"Error upserting document {doc['id']}: {e}")

    if markdown:
        prompt_path = get_prompt_path(folder="other", filename="summarize_metrics")
        inputs = {"data": report}
        summary = prompty.execute(prompt_path, inputs=inputs)
        print(summary)
    else:
        return report
