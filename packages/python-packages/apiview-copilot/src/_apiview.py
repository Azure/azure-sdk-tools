import asyncio
import sys
from dataclasses import dataclass
from typing import Optional

import httpx
from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosHttpResponseError
from src._credential import get_credential
from src._utils import get_language_pretty_name, to_iso8601

_APIVIEW_COMMENT_SELECT_FIELDS = [
    "id",
    "CreatedOn",
    "CreatedBy",
    "CommentText",
    "IsResolved",
    "IsDeleted",
    "ElementId",
    "ReviewId",
    "APIRevisionId",
    "Upvotes",
    "Downvotes",
    "CommentType",
    "CommentSource",
]
APIVIEW_COMMENT_SELECT_FIELDS = [f"c.{field}" for field in _APIVIEW_COMMENT_SELECT_FIELDS]


@dataclass
class ActiveReviewMetadata:
    review_id: str
    name: Optional[str]
    language: str

    def __post_init__(self):
        # Ensure pretty language name normalization
        self.language = get_language_pretty_name(self.language)


selection_type = {
    "Latest": 1,
    "LatestApproved": 2,
    "LatestAutomatic": 3,
    "LatestManual": 4,
}


class ApiViewClient:
    """Client for interacting with the API View service."""

    def __init__(self, environment: str = "production"):
        self.environment = environment

    async def get_revision_text(
        self, *, revision_id: Optional[str] = None, review_id: Optional[str] = None, label: Optional[str] = None
    ) -> str:
        """
        Get the text of an API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve.
            review_id (str): The ID of the API review to retrieve.
            label (str): Used in conjunction with review_id to specify which revision to get. Defaults to "Latest".
        """
        endpoint = "/api/apirevisions/getRevisionContent?"

        if revision_id:
            if review_id or label:
                raise ValueError("revision_id cannot be used with review_id or label.")
            endpoint += f"apiRevisionId={revision_id}"
        elif review_id:
            if not label:
                label = "Latest"
            if label not in selection_type:
                raise ValueError(f"Invalid label '{label}'. Must be one of {list(selection_type.keys())}.")
            endpoint += f"apiReviewId={review_id}&label={selection_type[label]}"
        else:
            raise ValueError("Either revision_id or review_id must be provided.")
        return await self.send_request(endpoint)

    async def get_revision_outline(self, revision_id: str) -> str:
        """
        Get the outline for a given API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve.
        """
        return await self.send_request(f"/api/apirevisions/{revision_id}/outline")

    async def get_review_comments(self, revision_id: str) -> str:
        """
        Get the comments visible for a given API review.
        Args:
            revision_id (str): The ID of the API revision to retrieve comments for. Comments that are "visible"
                               from that revision will be returned.
        """
        endpoint = f"/api/comments/getRevisionComments?apiRevisionId={revision_id}"
        return await self.send_request(endpoint)

    async def send_request(self, endpoint: str):
        # strip leading /
        if endpoint.startswith("/"):
            endpoint = endpoint[1:]

        apiview_endpoints = {
            "production": "https://apiview.dev",
            "staging": "https://apiviewstagingtest.com",
        }
        endpoint_root = apiview_endpoints.get(self.environment)
        endpoint = f"{endpoint_root}/{endpoint}"
        apiview_scopes = {
            "production": "api://apiview/.default",
            "staging": "api://apiviewstaging/.default",
        }
        credential = get_credential()
        scope = apiview_scopes.get(self.environment)
        token = await asyncio.to_thread(credential.get_token, scope)

        async with httpx.AsyncClient(timeout=30) as client:
            resp = await client.get(endpoint, headers={"Authorization": f"Bearer {token.token}"})
            resp.raise_for_status()
            return resp.json()


def get_apiview_cosmos_client(container_name: str, environment: str = "production", db_name: str = "APIViewV2"):
    """
    Returns the Cosmos DB container client for the specified container and environment.
    """
    apiview_account_names = {
        "production": "apiview-cosmos",
        "staging": "apiviewstaging",
    }
    try:
        cosmos_acc = apiview_account_names.get(environment)
        cosmos_db = db_name
        if not cosmos_acc:
            raise ValueError(
                # pylint: disable=line-too-long
                f"Unrecognized environment: {environment}. Valid options are: {', '.join(apiview_account_names.keys())}."
            )
        cosmos_url = f"https://{cosmos_acc}.documents.azure.com:443/"
        client = CosmosClient(url=cosmos_url, credential=get_credential())
        database = client.get_database_client(cosmos_db)
        container = database.get_container_client(container_name)
        return container
    except CosmosHttpResponseError as e:
        if e.status_code == 403:
            print(
                # pylint: disable=line-too-long
                "Error: You do not have permission to access Cosmos DB.\nTo grant yourself access, run: python scripts\\apiview_permissions.py"
            )
        sys.exit(1)


def get_active_reviews(
    start_date: str,
    end_date: str,
    *,
    environment: str = "production",
    omit_languages: Optional[list[str]] = None,
) -> list[ActiveReviewMetadata]:
    """
    Lists distinct active APIView review IDs in the specified environment during the specified period.
    The definition of "active" is any review that has comments created during the time period.

    Returns:
        list[ActiveReviewMetadata] - list of metadata objects considered "active" during the query window.
    """
    metadata: list[ActiveReviewMetadata] = []
    active_review_ids = get_active_review_ids(start_date, end_date, environment=environment)
    reviews_container = get_apiview_cosmos_client(container_name="Reviews", environment=environment)

    # Build a parameterized OR query to fetch matching reviews and filter by language.
    params = []
    clauses = []
    for i, rid in enumerate(active_review_ids):
        param_name = f"@id_{i}"
        clauses.append(f"c.id = {param_name}")
        params.append({"name": param_name, "value": rid})

    # Compose query with parameterized OR clauses
    query = f"SELECT c.id, c.PackageName, c.Language FROM c WHERE ({' OR '.join(clauses)})"
    results = list(reviews_container.query_items(query=query, parameters=params, enable_cross_partition_query=True))

    for result in results:
        review_id = result["id"]
        review_name = result.get("PackageName")
        language = get_language_pretty_name(result.get("Language", "Unknown"))
        if language == "Java" and review_name.startswith("com.azure.android:"):
            # APIView does not distinguish between Java and Android at the review level, but we need to
            language = "Android"
        metadata.append(ActiveReviewMetadata(review_id=review_id, name=review_name, language=language))

    # Filter out omitted languages if specified
    if omit_languages:
        omit_lower = {l.lower() for l in omit_languages}
        metadata = [r for r in metadata if r.language.lower() not in omit_lower]

    return metadata


def get_active_review_ids(start_date: str, end_date: str, environment: str = "production") -> list:
    """
    Lists distinct active APIView review IDs in the specified environment during the specified period.
    The definition of "active" is any review that has comments created during the time period.

    Returns:
        list[str] - list of unique ReviewId values considered "active" during the query window.
    """
    try:
        comments = get_comments_in_date_range(start_date, end_date, environment=environment)
    except Exception as e:
        print(f"Error retrieving active reviews: {e}")
        return []

    review_ids = set()
    for comment in comments:
        review_id = comment.get("ReviewId")
        if review_id:
            review_ids.add(review_id)

    return list(review_ids)


def get_comments_in_date_range(start_date: str, end_date: str, environment: str = "production") -> list:
    """
    Retrieves all comments created within the specified date range in the given environment.
    Applies ISO8601 midnight/end-of-day formatting to start_date and end_date.
    """
    start_iso = to_iso8601(start_date)
    end_iso = to_iso8601(end_date, end_of_day=True)

    comments_client = get_apiview_cosmos_client(container_name="Comments", environment=environment)
    result = comments_client.query_items(
        query=f"SELECT {', '.join(APIVIEW_COMMENT_SELECT_FIELDS)} FROM c WHERE c.CreatedOn >= @start_date AND c.CreatedOn <= @end_date",
        parameters=[
            {"name": "@start_date", "value": start_iso},
            {"name": "@end_date", "value": end_iso},
        ],
        enable_cross_partition_query=True,
    )
    return list(result)


def get_approvers(*, language: str = None, environment: str = "production") -> set[str]:
    """
    Retrieves the set of profile ids for approvers based on ApprovedLanguages.
    If language is specified, returns profile ids where ApprovedLanguages contains the language.
    If no language is specified, returns all profile ids with non-empty ApprovedLanguages.
    """
    profiles_client = get_apiview_cosmos_client(container_name="Profiles", environment=environment, db_name="APIView")
    query = "SELECT c.id, c.Preferences FROM c"
    parameters = []
    result = profiles_client.query_items(
        query=query,
        parameters=parameters,
        enable_cross_partition_query=True,
    )

    approver_ids = set()
    for item in result:
        preferences = item.get("Preferences", {})
        approved_languages = preferences.get("ApprovedLanguages", [])
        if not approved_languages:
            continue
        if language:
            if language in approved_languages:
                approver_ids.add(item.get("id"))
        else:
            approver_ids.add(item.get("id"))

    return approver_ids
