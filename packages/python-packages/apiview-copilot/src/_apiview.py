import sys
from datetime import datetime, timezone

import requests
from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosHttpResponseError
from src._credential import get_credential
from src._utils import get_language_pretty_name

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


def get_active_reviews(start_date: str, end_date: str, language: str, environment: str = "production") -> list:
    """
    Lists distinct active APIView review IDs in the specified environment during the specified period.

    Returns:
        list[str] - ordered list of unique ReviewId values for comments that match the query window/language.
    """
    try:
        container = get_apiview_cosmos_client(container_name="Comments", environment=environment)
        start_epoch = _to_epoch_seconds(start_date)
        end_epoch = _to_epoch_seconds(end_date)
        result = container.query_items(
            query=f"SELECT {', '.join(APIVIEW_COMMENT_SELECT_FIELDS)} FROM c WHERE c._ts >= @start_date AND c._ts <= @end_date",
            parameters=[
                {"name": "@start_date", "value": start_epoch},
                {"name": "@end_date", "value": end_epoch},
            ],
            enable_cross_partition_query=True,
        )
        comments = list(result)
    except Exception as e:
        print(f"Error retrieving active reviews: {e}")
        return []

    # Extract distinct ReviewId values preserving first-seen order
    review_ids: list = []
    seen: set = set()
    for comment in comments:
        review_id = comment.get("ReviewId")
        if not review_id:
            continue
        if review_id not in seen:
            seen.add(review_id)
            review_ids.append(review_id)

    if not review_ids:
        return []

    # Now extract the review names for those IDs from the "Reviews" container
    try:
        reviews_container = get_apiview_cosmos_client(container_name="Reviews", environment=environment)

        # Build a parameterized OR query to fetch matching reviews and filter by language.
        clauses = []
        for rid in review_ids:
            clauses.append(f'c.id = "{rid}"')

        language = get_language_pretty_name(language)
        query = f"SELECT c.id, c.PackageName FROM c WHERE ({' OR '.join(clauses)}) AND c.Language = \"{language}\""

        # Execute and materialize results
        results = list(reviews_container.query_items(query=query, enable_cross_partition_query=True))
    except Exception as e:
        print(f"Error retrieving review names from Reviews container: {e}")
        return []
    return results


def _to_epoch_seconds(date_str: str, *, end_of_day: bool = False) -> int:
    """
    Convert a date string to epoch seconds (UTC).

    Accepted inputs:
      - "YYYY-MM-DD"                -> treated as midnight UTC (or end of day if end_of_day=True)
      - full ISO-8601 datetime e.g. "2025-08-01T12:34:56Z" or "2025-08-01T12:34:56+00:00"

    Returns integer seconds since the epoch (UTC).

    Raises:
      ValueError if the input format cannot be parsed.
    """
    # Fast path for simple YYYY-MM-DD
    if len(date_str) == 10 and date_str.count("-") == 2:
        try:
            year, month, day = map(int, date_str.split("-"))
        except Exception as exc:
            raise ValueError(f"Invalid date: {date_str}") from exc
        if end_of_day:
            dt = datetime(year, month, day, 23, 59, 59, 999999, tzinfo=timezone.utc)
        else:
            dt = datetime(year, month, day, 0, 0, 0, 0, tzinfo=timezone.utc)
        return int(dt.timestamp())

    # Otherwise try ISO parsing
    try:
        # datetime.fromisoformat handles offsets like +00:00 but not trailing 'Z' in some versions.
        ds = date_str
        if ds.endswith("Z"):
            ds = ds[:-1] + "+00:00"
        dt = datetime.fromisoformat(ds)
        if dt.tzinfo is None:
            dt = dt.replace(tzinfo=timezone.utc)
        else:
            dt = dt.astimezone(timezone.utc)
        return int(dt.timestamp())
    except Exception as exc:
        raise ValueError(f"Unrecognized date format: {date_str}") from exc
