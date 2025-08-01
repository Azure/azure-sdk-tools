import asyncio
from collections import OrderedDict
import colorama
from colorama import Fore, Style
from datetime import datetime
import json
from knack import CLI, ArgumentsContext, CLICommandsLoader
from knack.commands import CommandGroup
from knack.help_files import helps
import os
import pathlib
import prompty
import prompty.azure
import requests
import sys
import time
from typing import Optional

from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosHttpResponseError

from src.agent._agent import get_main_agent, invoke_agent
from src._credential import get_credential
from src._search_manager import SearchManager
from src._database_manager import get_database_manager, ContainerNames
from src._mention import handle_mention_request
from src._models import APIViewComment
from src._utils import get_language_pretty_name, get_prompt_path

colorama.init(autoreset=True)

BLUE = Fore.BLUE
GREEN = Fore.GREEN
RESET = Style.RESET_ALL
BOLD = Style.BRIGHT

# Bold and color for prompts
BOLD_GREEN = BOLD + GREEN
BOLD_BLUE = BOLD + BLUE


helps[
    "review"
] = """
    type: group
    short-summary: Commands for creating APIView reviews.
"""

helps[
    "agent"
] = """
    type: group
    short-summary: Commands for interacting with the agent.
"""

helps[
    "apiview"
] = """
    type: group
    short-summary: Commands for interacting with APIView.
"""

helps[
    "eval"
] = """
    type: group
    short-summary: Commands for APIView Copilot evaluations.
"""

helps[
    "app"
] = """
    type: group
    short-summary: Commands for the Flask app deployment.
"""

helps[
    "search"
] = """
    type: group
    short-summary: Commands for searching the knowledge base.
"""

helps[
    "db"
] = """
    type: group
    short-summary: Commands for managing the database.
"""

helps[
    "metrics"
] = """
    type: group
    short-summary: Commands for reporting metrics.
"""

# COMMANDS


def _local_review(
    language: str,
    target: str,
    base: str = None,
    outline: str = None,
    existing_comments: str = None,
    debug_log: bool = False,
):
    """
    Generates a review using the locally installed code.
    """
    from src._apiview_reviewer import ApiViewReview

    if base is None:
        filename = os.path.splitext(os.path.basename(target))[0]
    else:
        target_name = os.path.splitext(os.path.basename(target))[0]
        base_name = os.path.splitext(os.path.basename(base))[0]
        # find the common prefix
        common_prefix = os.path.commonprefix([target_name, base_name])
        # strip the common prefix from both names
        target_name = target_name[len(common_prefix) :]
        base_name = base_name[len(common_prefix) :]
        filename = f"{common_prefix}_{base_name}_{target_name}"

    with open(target, "r", encoding="utf-8") as f:
        target_apiview = f.read()
    if base:
        with open(base, "r", encoding="utf-8") as f:
            base_apiview = f.read()
    else:
        base_apiview = None

    outline_text = None
    if outline:
        with open(outline, "r", encoding="utf-8") as f:
            outline_text = f.read()

    comments_obj = None
    if existing_comments:
        with open(existing_comments, "r", encoding="utf-8") as f:
            comments_obj = json.load(f)

    reviewer = ApiViewReview(
        target=target_apiview,
        base=base_apiview,
        language=language,
        outline=outline_text,
        comments=comments_obj,
        debug_log=debug_log,
    )
    review = reviewer.run()
    reviewer.close()
    output_path = os.path.join("scratch", "output", language)
    os.makedirs(output_path, exist_ok=True)
    output_file = os.path.join(output_path, f"{filename}.json")

    with open(output_file, "w", encoding="utf-8") as f:
        f.write(review.model_dump_json(indent=4))

    print(f"Review written to {output_file}")


def create_test_case(
    language: str,
    test_case: str,
    apiview_path: str,
    expected_path: str,
    test_file: str,
    overwrite: bool = False,
):
    """
    Creates or updates a test case for the APIView reviewer.
    """
    with open(apiview_path, "r") as f:
        apiview_contents = f.read()

    with open(expected_path, "r") as f:
        expected_contents = json.loads(f.read())

    guidelines_path = pathlib.Path(__file__).parent / "guidelines" / language
    guidelines = []
    for file in guidelines_path.glob("*.json"):
        with open(file, "r") as f:
            guidelines.extend(json.loads(f.read()))

    context = ""
    for violation in expected_contents["comments"]:
        for rule_id in violation["rule_ids"]:
            for rule in guidelines:
                if rule["id"] == rule_id:
                    if rule["text"] not in context:
                        context += f"\n{rule['text']}"

    test_case = {
        "testcase": test_case,
        "query": apiview_contents.replace("\t", ""),
        "language": language,
        "context": context,
        "response": json.dumps(expected_contents),
    }

    if os.path.exists(test_file):
        if overwrite:
            with open(test_file, "r") as f:
                existing_test_cases = [json.loads(line) for line in f if line.strip()]
            for existing_test_case in existing_test_cases:
                if existing_test_case["testcase"] == test_case["testcase"]:
                    existing_test_cases.remove(existing_test_case)
                    break
            existing_test_cases.append(test_case)
            with open(test_file, "w") as f:
                for existing_test_case in existing_test_cases:
                    f.write(json.dumps(existing_test_case) + "\n")
        else:
            with open(test_file, "a") as f:
                f.write("\n")
                json.dump(test_case, f)
    else:
        with open(test_file, "w") as f:
            json.dump(test_case, f)


def deconstruct_test_case(language: str, test_case: str, test_file: str):
    """
    Deconstructs a test case into its component APIView test and expected results file.
    """
    test_cases = {}
    with open(test_file, "r") as f:
        for line in f:
            if line.strip():
                parsed = json.loads(line)
                if "testcase" in parsed:
                    test_cases[parsed["testcase"]] = parsed

    if test_case not in test_cases:
        raise ValueError(f"Test case '{test_case}' not found in the file.")

    apiview = test_cases[test_case].get("query", "")
    expected = test_cases[test_case].get("response", "")
    deconstructed_apiview = pathlib.Path(__file__).parent / "evals" / "tests" / language / f"{test_case}.txt"
    deconstructed_expected = pathlib.Path(__file__).parent / "evals" / "tests" / language / f"{test_case}.json"
    with open(deconstructed_apiview, "w") as f:
        f.write(apiview)

    with open(deconstructed_expected, "w") as f:
        # sort comments by line number
        expected = json.loads(expected)
        expected["comments"] = sorted(expected["comments"], key=lambda x: x["line_no"])
        f.write(json.dumps(expected, indent=4))

    print(f"Deconstructed test case '{test_case}' into {deconstructed_apiview} and {deconstructed_expected}.")


def deploy_flask_app(
    app_name: Optional[str] = None,
    resource_group: Optional[str] = None,
    subscription_id: Optional[str] = None,
):
    """Command to deploy the Flask app."""
    from scripts.deploy_app import deploy_app_to_azure

    deploy_app_to_azure(app_name, resource_group, subscription_id)


def generate_review(
    language: str,
    target: str,
    base: Optional[str] = None,
    outline: Optional[str] = None,
    existing_comments: Optional[str] = None,
    remote: bool = False,
    debug_log: bool = False,
):
    """
    Generates a review synchronously.
    """
    if remote:
        elapsed = 0
        job_info = review_job_start(
            language=language,
            target=target,
            base=base,
            outline=outline,
            existing_comments=existing_comments,
        )
        job_id = job_info.get("job_id") if job_info else None
        print(f"Started review job {job_id}...")
        if not job_id:
            print("Error: Could not extract job_id from review_job_start output.")
            print(job_info)
            return
        for _ in range(1800):  # up to 30 minutes
            status_info = review_job_get(job_id)
            status = status_info.get("status") if status_info else None
            if not status_info:
                print(f"Error: Could not get status for job {job_id}")
                return
            if status == "Success":
                print(json.dumps(status_info, indent=2))
                return
            elif status == "Error":
                print(f"Review job failed: {json.dumps(status_info, indent=2)}")
                return
            time.sleep(30)
            elapsed += 30
            print(f"  Status: {status}. Elapsed time: {elapsed / 60.0:.1f} min")
        print("Timed out waiting for review job to complete.")
    else:
        return _local_review(
            language=language,
            target=target,
            base=base,
            outline=outline,
            existing_comments=existing_comments,
            debug_log=debug_log,
        )


def review_job_start(
    language: str,
    target: str,
    base: Optional[str] = None,
    outline: Optional[str] = None,
    existing_comments: Optional[str] = None,
):
    """Start an API review job."""

    with open(target, "r", encoding="utf-8") as f:
        target_content = f.read()
    if base:
        with open(base, "r", encoding="utf-8") as f:
            base_content = f.read()
    else:
        base_content = None

    outline_text = None
    if outline:
        with open(outline, "r", encoding="utf-8") as f:
            outline_text = f.read()

    comments_obj = None
    if existing_comments:
        with open(existing_comments, "r", encoding="utf-8") as f:
            comments_obj = json.load(f)

    payload = {
        "language": language,
        "target": target_content,
    }
    if base_content is not None:
        payload["base"] = base_content
    if outline_text is not None:
        payload["outline"] = outline_text
    if comments_obj is not None:
        payload["comments"] = comments_obj

    APP_NAME = os.getenv("AZURE_APP_NAME")
    if not APP_NAME:
        raise ValueError("AZURE_APP_NAME environment variable is not set.")
    api_endpoint = f"https://{APP_NAME}.azurewebsites.net/api-review/start"

    resp = requests.post(api_endpoint, json=payload)
    if resp.status_code == 202:
        return resp.json()
    else:
        print(f"Error: {resp.status_code} {resp.text}")


def review_job_get(job_id: str):
    """Get the status/result of an API review job."""
    APP_NAME = os.getenv("AZURE_APP_NAME")
    if not APP_NAME:
        raise ValueError("AZURE_APP_NAME environment variable is not set.")
    api_endpoint = f"https://{APP_NAME}.azurewebsites.net/api-review"
    url = f"{api_endpoint.rstrip('/')}/{job_id}"
    resp = requests.get(url)
    if resp.status_code == 200:
        return resp.json()
    else:
        print(f"Error: {resp.status_code} {resp.text}")


def search_knowledge_base(
    language: str,
    text: Optional[str] = None,
    path: Optional[str] = None,
    markdown: bool = False,
):
    """
    Queries the Search indexes and returns the resulting Cosmos DB
    objects, resolving all links between objects. This result represents
    what the AI reviewer would receive as context in RAG mode.
    """
    if (path and text) or (not path and not text):
        raise ValueError("Provide one of `--path` or `--text`.")
    search = SearchManager(language=language)
    query = text
    if path:
        with open(path, "r") as f:
            query = f.read()
    results = search.search_all(query=query)
    context = search.build_context(results.results)
    if markdown:
        md = context.to_markdown()
        print(md)
    else:
        print(json.dumps(context, indent=2, cls=CustomJSONEncoder))


def reindex_search(containers: Optional[list[str]] = None):
    """
    Trigger a reindex of the Azure Search index for the ArchAgent Knowledge Base.
    If no container is specified, reindex all containers.
    """
    return SearchManager.run_indexers(container_names=containers)


def review_summarize(language: str, target: str, base: str = None):
    """
    Summarize an API or a diff of two APIs using the deployed API review service.
    """
    payload = {"language": language, "target": target}
    if base:
        payload["base"] = base
    APP_NAME = os.getenv("AZURE_APP_NAME")
    api_endpoint = f"https://{APP_NAME}.azurewebsites.net/api-review/summarize"
    response = requests.post(api_endpoint, json=payload)
    if response.status_code == 200:
        summary = response.json().get("summary")
        print(summary)
    else:
        print(f"Error: {response.status_code} - {response.text}")


def handle_agent_chat(thread_id: Optional[str] = None, remote: bool = False):
    """
    Start or continue an interactive chat session with the agent.
    """

    async def async_input(prompt: str) -> str:
        # Run input() in a thread to avoid blocking the event loop
        return await asyncio.to_thread(input, prompt)

    async def chat():
        print(f"{BOLD}Interactive API Review Agent Chat. Type 'exit' to quit.\n{RESET}")
        messages = []
        current_thread_id = thread_id
        if remote:
            APP_NAME = os.getenv("AZURE_APP_NAME")
            if not APP_NAME:
                print(f"{BOLD}AZURE_APP_NAME environment variable is not set.{RESET}")
                return
            api_endpoint = f"https://{APP_NAME}.azurewebsites.net/agent/chat"
            session = requests.Session()
            while True:
                try:
                    user_input = await async_input(f"{BOLD_GREEN}You:{RESET} ")
                except (EOFError, KeyboardInterrupt):
                    print(f"\n{BOLD}Exiting chat.{RESET}")
                    if current_thread_id:
                        print(
                            f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                        )
                    break
                if user_input.strip().lower() in {"exit", "quit"}:
                    print(f"\n{BOLD}Exiting chat.{RESET}")
                    if current_thread_id:
                        print(
                            f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                        )
                    break
                try:
                    payload = {"user_input": user_input}
                    if current_thread_id:
                        payload["thread_id"] = current_thread_id
                    resp = session.post(api_endpoint, json=payload)
                    if resp.status_code == 200:
                        data = resp.json()
                        response = data.get("response", "")
                        thread_id_out = data.get("thread_id", current_thread_id)
                        print(f"{BOLD_BLUE}Agent:{RESET} {response}\n")
                        current_thread_id = thread_id_out
                    else:
                        print(f"Error: {resp.status_code} - {resp.text}")
                except Exception as e:
                    print(f"Error: {e}")
        else:
            # Local mode: use async agent as before
            async with get_main_agent() as agent:
                while True:
                    try:
                        user_input = await async_input(f"{BOLD_GREEN}You:{RESET} ")
                    except (EOFError, KeyboardInterrupt):
                        print(f"\n{BOLD}Exiting chat.{RESET}")
                        if current_thread_id:
                            print(
                                f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                            )
                        break
                    if user_input.strip().lower() in {"exit", "quit"}:
                        print(f"\n{BOLD}Exiting chat.{RESET}")
                        if current_thread_id:
                            print(
                                f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                            )
                        break
                    try:
                        response, thread_id_out, messages = await invoke_agent(
                            agent=agent, user_input=user_input, thread_id=current_thread_id, messages=messages
                        )
                        print(f"{BOLD_BLUE}Agent:{RESET} {response}\n")
                        current_thread_id = thread_id_out
                    except Exception as e:
                        print(f"Error: {e}")

    asyncio.run(chat())


def handle_agent_mention(comments_path: str, remote: bool = False):
    """
    Handles @mention requests from the agent.
    This function is a placeholder for the actual implementation.
    """
    # load comments from the comments_path
    comments = []
    if os.path.exists(comments_path):
        with open(comments_path, "r", encoding="utf-8") as f:
            data = json.load(f)
    else:
        print(f"Comments file {comments_path} does not exist.")
        return
    comments = data.get("comments", [])
    language = data.get("language", None)
    package_name = data.get("package_name", None)
    code = data.get("code", None)
    if language not in SUPPORTED_LANGUAGES:
        print(f"Unsupported language `{language}`")
        return
    pretty_language = get_language_pretty_name(language)

    if remote:
        APP_NAME = os.getenv("AZURE_APP_NAME")
        if not APP_NAME:
            print("AZURE_APP_NAME environment variable is not set.")
            return
        api_endpoint = f"https://{APP_NAME}.azurewebsites.net/api-review/mention"
        try:
            resp = requests.post(
                api_endpoint,
                json={"comments": comments, "language": language, "packageName": package_name, "code": code},
            )
            data = resp.json()
            if resp.status_code == 200:
                print(f"{BOLD_BLUE}Agent response:{RESET}\n{data.get('response', '')}\n")
            else:
                print(f"Error: {resp.status_code} - {data}")
        except Exception as e:
            print(f"Error: {e}")
    else:
        return handle_mention_request(
            comments=comments,
            language=pretty_language,
            package_name=package_name,
            code=code,
        )


def db_get(container_name: str, id: str):
    """Retrieve an item from the database."""
    db = get_database_manager()
    container = db.get_container_client(container_name)
    try:
        item = container.get(id)
        print(json.dumps(item, indent=2))
    except Exception as e:
        print(f"Error retrieving item: {e}")


def _get_apiview_cosmos_client(*, environment: str = "production"):
    """
    Returns the Cosmos DB container client for APIView comments.
    """
    apiview_account_names = {
        "production": "apiview-cosmos",
        "staging": "apiviewstaging",
    }


def _get_apiview_cosmos_client(container_name: str, environment: str = "production"):
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
                f"Error: You do not have permission to access Cosmos DB.\nTo grant yourself access, run: python scripts\\apiview_permissions.py"
            )
        sys.exit(1)


def _get_apiview_reviews_client(environment: str = "production"):
    """
    Returns the Cosmos DB container client for APIView reviews.
    """
    try:
        cosmos_acc = os.getenv("APIVIEW_COSMOS_ACC_NAME")
        cosmos_db = "APIViewV2"
        container_name = "Reviews"
        if not cosmos_acc:
            raise ValueError("APIVIEW_COSMOS_ACC_NAME environment variable is not set.")
        cosmos_url = f"https://{cosmos_acc}.documents.azure.com:443/"
        client = CosmosClient(url=cosmos_url, credential=get_credential())
        database = client.get_database_client(cosmos_db)
        container = database.get_container_client(container_name)
        return container
    except CosmosHttpResponseError as e:
        if e.status_code == 403:
            print(
                f"Error: You do not have permission to access Cosmos DB.\nTo grant yourself access, run: python scripts\\apiview_permissions.py"
            )
        sys.exit(1)


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


def get_apiview_comments(review_id: str, environment: str = "production", use_api: bool = False) -> dict:
    """
    Retrieves comments for a specific APIView review and returns them grouped by element ID and sorted by CreatedOn time.
    Omits resolved and deleted comments, and removes unnecessary fields.
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
                headers={"Content-Type": "application/json",
                        "Authorization": f"Bearer {token.token}"}
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
            container = _get_apiview_cosmos_client(container_name="Comments", environment=environment)
            result = container.query_items(
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


def _calculate_language_adoption(start_date: str, end_date: str, environment: str = "production") -> dict:
    """
    Calculates the adoption rate of AI review comments by language.
    Looks at distinct ReviewIds that had new revisions created during the time period
    and calculates what percentage of those ReviewIds have AI comments.
    Returns a dictionary with languages as keys and adoption percentages as values.
    """
    # Get comments container client
    comments_client = _get_apiview_cosmos_client(container_name="Comments", environment=environment)
    reviews_client = _get_apiview_cosmos_client(container_name="Reviews", environment=environment)

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


def report_metrics(start_date: str, end_date: str, environment: str = "production", markdown: bool = False) -> dict:
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

    comments_client = _get_apiview_cosmos_client(container_name="Comments", environment=environment)
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


SUPPORTED_LANGUAGES = [
    "android",
    "clang",
    "cpp",
    "dotnet",
    "golang",
    "ios",
    "java",
    "python",
    "rust",
    "typescript",
]


class CliCommandsLoader(CLICommandsLoader):

    # COMMAND REGISTRATION

    def load_command_table(self, args):
        with CommandGroup(self, "apiview", "__main__#{}") as g:
            g.command("get-comments", "get_apiview_comments")
        with CommandGroup(self, "review", "__main__#{}") as g:
            g.command("generate", "generate_review")
            g.command("start-job", "review_job_start")
            g.command("get-job", "review_job_get")
            g.command("summarize", "review_summarize")
        with CommandGroup(self, "agent", "__main__#{}") as g:
            g.command("mention", "handle_agent_mention")
            g.command("chat", "handle_agent_chat")
        with CommandGroup(self, "eval", "__main__#{}") as g:
            g.command("create", "create_test_case")
            g.command("deconstruct", "deconstruct_test_case")
        with CommandGroup(self, "app", "__main__#{}") as g:
            g.command("deploy", "deploy_flask_app")
        with CommandGroup(self, "search", "__main__#{}") as g:
            g.command("kb", "search_knowledge_base")
            g.command("reindex", "reindex_search")
        with CommandGroup(self, "db", "__main__#{}") as g:
            g.command("get", "db_get")
        with CommandGroup(self, "metrics", "__main__#{}") as g:
            g.command("report", "report_metrics")
        return OrderedDict(self.command_table)

    # ARGUMENT REGISTRATION

    def load_arguments(self, command):
        with ArgumentsContext(self, "") as ac:
            ac.argument(
                "language",
                type=str,
                help="The language of the APIView file",
                options_list=("--language", "-l"),
                choices=SUPPORTED_LANGUAGES,
            )
            ac.argument(
                "remote",
                action="store_true",
                help="Use the remote API review service instead of local processing.",
            )
        with ArgumentsContext(self, "review") as ac:
            ac.argument("path", type=str, help="The path to the APIView file")
            ac.argument(
                "target",
                type=str,
                help="The path to the APIView file to review.",
                options_list=("--target", "-t"),
            )
            ac.argument(
                "base",
                type=str,
                help="The path to the base APIView file to compare against. If omitted, copilot will review the entire target APIView.",
                options_list=("--base", "-b"),
            )
            ac.argument(
                "outline",
                type=str,
                help="Path to a plain text file containing the outline text.",
                options_list=["--outline"],
                default=None,
            )
            ac.argument(
                "existing_comments",
                type=str,
                help="Path to a JSON file containing existing comments.",
                default=None,
            )
            ac.argument(
                "debug_log",
                options_list=["--debug-log"],
                action="store_true",
                help="Enable debug logging for the review process. Outputs to `scratch/logs/<LANG>` directory.",
            )
        with ArgumentsContext(self, "eval create") as ac:
            ac.argument("language", type=str, help="The language for the test case.")
            ac.argument("test_case", type=str, help="The name of the test case")
            ac.argument(
                "apiview_path",
                type=str,
                help="The full path to the txt file containing the APIview text",
            )
            ac.argument(
                "expected_path",
                type=str,
                help="The full path to the expected JSON output from the AI reviewer.",
            )
            ac.argument(
                "test_file",
                type=str,
                help="The full path to the JSONL test file. Can be an existing test file, or will create a new one.",
            )
            ac.argument(
                "overwrite",
                action="store_true",
                help="Overwrite the test case if it already exists.",
            )
        with ArgumentsContext(self, "eval deconstruct") as ac:
            ac.argument("language", type=str, help="The language for the test case.")
            ac.argument("test_case", type=str, help="The specific test case to deconstruct.")
            ac.argument("test_file", type=str, help="The full path to the JSONL test file.")
        with ArgumentsContext(self, "app deploy") as ac:
            ac.argument(
                "app_name",
                options_list=["--app-name"],
                help="The name of the Azure App Service. Env var: AZURE_APP_NAME",
            )
            ac.argument(
                "resource_group",
                options_list=["--resource-group"],
                help="The Azure resource group containing the App Service. Env var: AZURE_RESOURCE_GROUP",
            )
            ac.argument(
                "subscription_id",
                options_list=["--subscription-id"],
                help="The Azure subscription ID. Env var: AZURE_SUBSCRIPTION_ID",
            )
        with ArgumentsContext(self, "search") as ac:
            ac.argument(
                "path",
                type=str,
                help="The path to the file containing query text or code.",
                options_list=["--path"],
            )
            ac.argument(
                "text",
                type=str,
                help="The text query to search.",
            )
            ac.argument(
                "index",
                type=str,
                nargs="+",
                help="The indexes to search. Can be one or more of: examples, guidelines.",
                options_list=["--index"],
            )
            ac.argument(
                "markdown",
                help="Render output as markdown instead of JSON.",
            )
        with ArgumentsContext(self, "search reindex") as ac:
            ac.argument(
                "containers",
                type=str,
                nargs="*",
                help="The names of the containers to reindex. If not provided, all containers will be reindexed.",
                options_list=["--containers", "-c"],
                choices=[c.value for c in ContainerNames if c != "review-jobs"],
            )
        with ArgumentsContext(self, "review start-job") as ac:
            ac.argument(
                "language",
                type=str,
                help="The language of the APIView file",
                options_list=("--language", "-l"),
                choices=SUPPORTED_LANGUAGES,
            )
            ac.argument(
                "target",
                type=str,
                help="The path to the APIView file to review.",
                options_list=("--target", "-t"),
            )
            ac.argument(
                "base",
                type=str,
                help="The path to the base APIView file to compare against.",
                options_list=("--base", "-b"),
                default=None,
            )
            ac.argument(
                "outline",
                type=str,
                help="Path to a plain text file containing the outline text.",
                options_list=["--outline"],
                default=None,
            )
            ac.argument(
                "existing_comments",
                type=str,
                help="Path to a JSON file containing existing comments.",
                default=None,
            )

        with ArgumentsContext(self, "review get-job") as ac:
            ac.argument(
                "job_id",
                type=str,
                help="The job ID to poll.",
                options_list=["--job-id"],
            )
        with ArgumentsContext(self, "review summarize") as ac:
            ac.argument(
                "language",
                type=str,
                help="The language of the APIView file",
                options_list=["--language", "-l"],
                choices=SUPPORTED_LANGUAGES,
            )
            ac.argument(
                "target", type=str, help="The path to the APIView file to summarize.", options_list=["--target", "-t"]
            )
            ac.argument(
                "base",
                type=str,
                help="The path to the base APIView file for diff summarization.",
                options_list=["--base", "-b"],
            )
        with ArgumentsContext(self, "agent mention") as ac:
            ac.argument(
                "comments_path",
                type=str,
                help="Path to the JSON file containing comments for the agent to process.",
                options_list=["--comments-path", "-c"],
            )
        with ArgumentsContext(self, "agent") as ac:
            ac.argument(
                "thread_id",
                type=str,
                help="The thread ID to continue the discussion. If not provided, a new thread will be created.",
                options_list=["--thread-id", "-t"],
            )
        with ArgumentsContext(self, "db get") as ac:
            ac.argument(
                "container_name",
                type=str,
                help="The name of the Cosmos DB container",
                choices=[c.value for c in ContainerNames],
                options_list=["--container-name", "-c"],
            )
            ac.argument(
                "id",
                type=str,
                help="The id of the item to retrieve.",
                options_list=["--id", "-i"],
            )
        with ArgumentsContext(self, "apiview") as ac:
            ac.argument(
                "review_id",
                type=str,
                help="The review ID of the APIView to retrieve comments for.",
                options_list=["--review-id", "-r"],
            )
            ac.argument(
                "environment",
                type=str,
                help="The APIView environment from which to retrieve comments. Defaults to 'production'.",
                options_list=["--environment"],
                default="production",
                choices=["production", "staging"],
            )
            ac.argument(
                "use_api",
                action="store_true",
                help="Use the APIView API to retrieve comments instead of Cosmos DB.",
            )
        with ArgumentsContext(self, "metrics report") as ac:
            ac.argument(
                "start_date",
                type=str,
                help="The start date for the metrics report (YYYY-MM-DD).",
                options_list=["--start-date", "-s"],
            )
            ac.argument(
                "end_date",
                type=str,
                help="The end date for the metrics report (YYYY-MM-DD).",
                options_list=["--end-date", "-e"],
            )
            ac.argument(
                "environment",
                type=str,
                help="The APIView environment from which to calculate the metrics report. Defaults to 'production'.",
                options_list=["--environment"],
                default="production",
                choices=["production", "staging"],
            )
        super(CliCommandsLoader, self).load_arguments(command)


def run_cli():
    cli = CLI(cli_name="apiviewcopilot", commands_loader_cls=CliCommandsLoader)
    exit_code = cli.invoke(sys.argv[1:])
    sys.exit(exit_code)


class CustomJSONEncoder(json.JSONEncoder):
    def default(self, obj):
        # If the object has a `to_dict` method, use it
        if hasattr(obj, "to_dict"):
            return obj.to_dict()
        # If the object has a `__dict__` attribute, use it
        elif hasattr(obj, "__dict__"):
            return obj.__dict__
        # Otherwise, use the default serialization
        return super().default(obj)


if __name__ == "__main__":
    run_cli()
