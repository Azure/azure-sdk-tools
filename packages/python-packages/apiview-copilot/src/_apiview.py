import sys

import requests
from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosHttpResponseError
from src._credential import get_credential

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


def get_apiview_comments(review_id: str, environment: str = "production", use_api: bool = False) -> dict:
    """
    Retrieves comments for a specific APIView review and returns them grouped by element ID and
    sorted by CreatedOn time. Omits resolved and deleted comments, and removes unnecessary fields.
    """
    comments = []
    try:
        if use_api:
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
        else:
            container = get_apiview_cosmos_client(container_name="Comments", environment=environment)
            result = container.query_items(
                # pylint: disable=line-too-long
                query=f"SELECT {', '.join(APIVIEW_COMMENT_SELECT_FIELDS)} FROM c WHERE c.ReviewId = @review_id AND c.IsResolved = false AND c.IsDeleted = false",
                parameters=[{"name": "@review_id", "value": review_id}],
            )
            comments = list(result)
    except Exception as e:
        print(f"Error retrieving comments for review {review_id}: {e}")
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
    Lists active APIView reviews in the specified environment during the specified period.
    """
    reviews = []
    try:
        container = get_apiview_cosmos_client(container_name="Reviews", environment=environment)
        result = container.query_items(
            query=f"SELECT {APIVIEW_COMMENT_SELECT_FIELDS} FROM c WHERE c.IsClosed = false AND c.CreatedOn >= @start_date AND c.CreatedOn <= @end_date AND c.Language = @language",
            parameters=[
                {"name": "@start_date", "value": start_date},
                {"name": "@end_date", "value": end_date},
                {"name": "@language", "value": language},
            ],
            enable_cross_partition_query=True,
        )
        reviews = list(result)
    except Exception as e:
        print(f"Error retrieving active reviews: {e}")
        return []
    return reviews
