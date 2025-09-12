import sys
from datetime import datetime, timezone
from typing import Optional

import requests
from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosHttpResponseError
from src._credential import get_credential
from src._utils import get_language_pretty_name, to_epoch_seconds

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
]
APIVIEW_COMMENT_SELECT_FIELDS = [f"c.{field}" for field in _APIVIEW_COMMENT_SELECT_FIELDS]


class ActiveReviewMetadata:
    def __init__(self, review_id: str, name: Optional[str], language: str):
        self.review_id = review_id
        self.name = name
        self.language = get_language_pretty_name(language)


def get_apiview_cosmos_client(container_name: str, environment: str = "production"):
    """
    Returns the Cosmos DB container client for the specified container and environment.
    """
    apiview_account_names = {
        "production": "apiview-cosmos",
        "staging": "apiviewstaging",
    }
    try:
        cosmos_acc = apiview_account_names.get(environment)
        cosmos_db = "APIViewV2"
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


def get_apiview_comments(review_id: str, environment: str = "production") -> dict:
    """
    Retrieves comments for a specific APIView review and returns them grouped by element ID and
    sorted by CreatedOn time. Omits resolved and deleted comments, and removes unnecessary fields.
    """
    comments = []
    if not review_id:
        raise ValueError("When using the API, `--review-id` must be provided.")
    apiview_endpoints = {
        "production": "https://apiview.dev",
        "staging": "https://apiviewstagingtest.com",
    }
    endpoint_root = apiview_endpoints.get(environment)
    endpoint = f"{endpoint_root}/api/Comments/{review_id}?commentType=APIRevision&isDeleted=false"
    apiview_scopes = {
        "production": "api://apiview/.default",
        "staging": "api://apiviewstaging/.default",
    }
    credential = get_credential()
    scope = apiview_scopes.get(environment)
    token = credential.get_token(scope)
    response = requests.get(
        endpoint,
        headers={"Content-Type": "application/json", "Authorization": f"Bearer {token.token}"},
        timeout=30,
    )

    if response.status_code != 200:
        print(f"Error retrieving comments: {response.status_code} - {response.text}")
        return {}
    try:
        comments = response.json()
    except Exception as json_exc:
        content = response.content.decode("utf-8")
        if "Please login using your GitHub account" in content:
            print("Error: API is still requesting authentication via Github.")
            return {}
        else:
            print(f"Error parsing comments JSON: {json_exc}")
            return {}

    conversations = {}
    if comments:
        for comment in comments:
            element_id = comment.get("ElementId")
            if element_id in conversations:
                conversations[element_id].append(comment)
            else:
                conversations[element_id] = [comment]
    for element_id, comments in conversations.items():
        # sort comments by created_on time
        comments.sort(key=lambda x: x.get("CreatedOn", 0))
    return conversations


def get_active_reviews(start_date: str, end_date: str, environment: str = "production") -> list[ActiveReviewMetadata]:
    """
    Lists distinct active APIView review IDs in the specified environment during the specified period.
    The definition of "active" is any review that has comments created during the time period.

    Returns:
        list[str] - list of unique ReviewId values considered "active" during the query window.
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
        language = get_language_pretty_name(result.get("Language", "Unknown"))
        review_id = result["id"]
        review_name = result.get("PackageName")
        metadata.append(ActiveReviewMetadata(review_id=review_id, name=review_name, language=language))
    return metadata


def get_comments_in_date_range(start_date: str, end_date: str, environment: str = "production") -> list:
    """
    Retrieves all comments created within the specified date range in the given environment.
    """
    comments_client = get_apiview_cosmos_client(container_name="Comments", environment=environment)
    start_epoch = to_epoch_seconds(start_date)
    end_epoch = to_epoch_seconds(end_date, end_of_day=True)
    result = comments_client.query_items(
        query=f"SELECT {', '.join(APIVIEW_COMMENT_SELECT_FIELDS)} FROM c WHERE c._ts >= @start_date AND c._ts <= @end_date",
        parameters=[
            {"name": "@start_date", "value": start_epoch},
            {"name": "@end_date", "value": end_epoch},
        ],
        enable_cross_partition_query=True,
    )
    return list(result)


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
