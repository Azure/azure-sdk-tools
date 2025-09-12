# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=too-many-lines

"""
Command line interface for APIView Copilot.
"""

import asyncio
import json
import os
import pathlib
import sys
import time
from collections import OrderedDict
from typing import Optional

import colorama
import requests
from colorama import Fore, Style
from knack import CLI, ArgumentsContext, CLICommandsLoader
from knack.commands import CommandGroup
from knack.help_files import helps
from src._apiview import get_active_reviews as _get_active_reviews
from src._apiview import get_apiview_comments as _get_apiview_comments
from src._apiview_reviewer import SUPPORTED_LANGUAGES, ApiViewReview
from src._database_manager import ContainerNames, get_database_manager
from src._garbage_collector import GarbageCollector
from src._mention import handle_mention_request
from src._metrics import get_metrics_report
from src._search_manager import SearchManager
from src._settings import SettingsManager
from src._thread_resolution import handle_thread_resolution_request
from src._utils import get_language_pretty_name
from src.agent._agent import get_main_agent, invoke_agent

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

helps[
    "permissions"
] = """
    type: group
    short-summary: Commands for managing permissions.
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
    target_path = pathlib.Path(target)
    if base is None:
        filename = target_path.stem
    else:
        target_name = target_path.stem
        base_name = pathlib.Path(base).stem
        # find the common prefix
        common_prefix = os.path.commonprefix([target_name, base_name])
        # strip the common prefix from both names
        target_name = target_name[len(common_prefix) :]
        base_name = base_name[len(common_prefix) :]
        filename = f"{common_prefix}_{base_name}_{target_name}"

    with target_path.open("r", encoding="utf-8") as f:
        target_apiview = f.read()
    if base:
        with pathlib.Path(base).open("r", encoding="utf-8") as f:
            base_apiview = f.read()
    else:
        base_apiview = None

    outline_text = None
    if outline:
        with pathlib.Path(outline).open("r", encoding="utf-8") as f:
            outline_text = f.read()

    comments_obj = None
    if existing_comments:
        with pathlib.Path(existing_comments).open("r", encoding="utf-8") as f:
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
    output_path = pathlib.Path("scratch") / "output" / language
    output_path.mkdir(parents=True, exist_ok=True)
    output_file = output_path / f"{filename}.json"

    with output_file.open("w", encoding="utf-8") as f:
        f.write(review.model_dump_json(indent=4))

    print(f"Review written to {output_file}")


def run_test_case(language: str, test_file: str, num_runs: int = 3):
    """
    Runs one or all eval test cases.
    """
    from evals._runner import EvalRunner

    runner = EvalRunner(language=language, test_path=test_file, num_runs=num_runs)
    runner.run()


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
    with open(apiview_path, "r", encoding="utf-8") as f:
        apiview_contents = f.read()

    with open(expected_path, "r", encoding="utf-8") as f:
        expected_contents = json.loads(f.read())

    guidelines_path = pathlib.Path(__file__).parent / "guidelines" / language
    guidelines = []
    for file in guidelines_path.glob("*.json"):
        with open(file, "r", encoding="utf-8") as f:
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
            with open(test_file, "r", encoding="utf-8") as f:
                existing_test_cases = [json.loads(line) for line in f if line.strip()]
            for existing_test_case in existing_test_cases:
                if existing_test_case["testcase"] == test_case["testcase"]:
                    existing_test_cases.remove(existing_test_case)
                    break
            existing_test_cases.append(test_case)
            with open(test_file, "w", encoding="utf-8") as f:
                for existing_test_case in existing_test_cases:
                    f.write(json.dumps(existing_test_case) + "\n")
        else:
            with open(test_file, "a", encoding="utf-8") as f:
                f.write("\n")
                json.dump(test_case, f)
    else:
        with open(test_file, "w", encoding="utf-8") as f:
            json.dump(test_case, f)


def deconstruct_test_case(language: str, test_case: str, test_file: str):
    """
    Deconstructs a test case into its component APIView test and expected results file.
    """
    test_cases = {}
    with open(test_file, "r", encoding="utf-8") as f:
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
    with open(deconstructed_apiview, "w", encoding="utf-8") as f:
        f.write(apiview)

    with open(deconstructed_expected, "w", encoding="utf-8") as f:
        # sort comments by line number
        expected = json.loads(expected)
        expected["comments"] = sorted(expected["comments"], key=lambda x: x["line_no"])
        f.write(json.dumps(expected, indent=4))

    print(f"Deconstructed test case '{test_case}' into {deconstructed_apiview} and {deconstructed_expected}.")


def deploy_flask_app():
    """Command to deploy the Flask app."""
    # pylint: disable=import-outside-toplevel
    from scripts.deploy_app import deploy_app_to_azure

    deploy_app_to_azure()


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

    settings = SettingsManager()
    base_url = settings.get("WEBAPP_ENDPOINT")
    api_endpoint = f"{base_url}/api-review/start"

    resp = requests.post(api_endpoint, json=payload, timeout=60)
    if resp.status_code == 202:
        return resp.json()
    else:
        print(f"Error: {resp.status_code} {resp.text}")


def review_job_get(job_id: str):
    """Get the status/result of an API review job."""
    settings = SettingsManager()
    base_url = settings.get("WEBAPP_ENDPOINT")
    api_endpoint = f"{base_url}/api-review"
    url = f"{api_endpoint.rstrip('/')}/{job_id}"
    resp = requests.get(url, timeout=10)
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
        with open(path, "r", encoding="utf-8") as f:
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
    containers = containers or ContainerNames.data_containers()
    return SearchManager.run_indexers(container_names=containers)


def review_summarize(language: str, target: str, base: str = None):
    """
    Summarize an API or a diff of two APIs using the deployed API review service.
    """
    payload = {"language": language, "target": target}
    if base:
        payload["base"] = base
    settings = SettingsManager()
    base_url = settings.get("WEBAPP_ENDPOINT")
    api_endpoint = f"{base_url}/api-review/summarize"
    response = requests.post(api_endpoint, json=payload, timeout=60)
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
            settings = SettingsManager()
            base_url = settings.get("WEBAPP_ENDPOINT")
            api_endpoint = f"{base_url}/agent/chat"
            session = requests.Session()
            while True:
                try:
                    user_input = await async_input(f"{BOLD_GREEN}You:{RESET} ")
                except (EOFError, KeyboardInterrupt):
                    print(f"\n{BOLD}Exiting chat.{RESET}")
                    if current_thread_id:
                        print(
                            # pylint: disable=line-too-long
                            f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                        )
                    break
                if user_input.strip().lower() in {"exit", "quit"}:
                    print(f"\n{BOLD}Exiting chat.{RESET}")
                    if current_thread_id:
                        print(
                            # pylint: disable=line-too-long
                            f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                        )
                    break
                try:
                    payload = {"user_input": user_input}
                    if current_thread_id:
                        payload["thread_id"] = current_thread_id
                    resp = session.post(api_endpoint, json=payload, timeout=60)
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
                                # pylint: disable=line-too-long
                                f"{BOLD}Supply `-t/--thread-id {current_thread_id}` to continue the discussion later.{RESET}"
                            )
                        break
                    if user_input.strip().lower() in {"exit", "quit"}:
                        print(f"\n{BOLD}Exiting chat.{RESET}")
                        if current_thread_id:
                            print(
                                # pylint: disable=line-too-long
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
        settings = SettingsManager()
        base_url = settings.get("WEBAPP_ENDPOINT")
        api_endpoint = f"{base_url}/api-review/mention"
        try:
            resp = requests.post(
                api_endpoint,
                json={"comments": comments, "language": language, "packageName": package_name, "code": code},
                timeout=60,
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


def handle_agent_thread_resolution(comments_path: str, remote: bool = False):
    """
    Handles requests to update the knowledge base when a conversation is resolved.
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
        settings = SettingsManager()
        base_url = settings.get("WEBAPP_ENDPOINT")
        api_endpoint = f"{base_url}/api-review/resolve"
        try:
            resp = requests.post(
                api_endpoint,
                json={"comments": comments, "language": language, "packageName": package_name, "code": code},
                timeout=60,
            )
            data = resp.json()
            if resp.status_code == 200:
                print(f"{BOLD_BLUE}Agent response:{RESET}\n{data.get('response', '')}\n")
            else:
                print(f"Error: {resp.status_code} - {data}")
        except Exception as e:
            print(f"Error: {e}")
    else:
        return handle_thread_resolution_request(
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


def db_delete(container_name: str, id: str):
    """Soft-delete an item from the database."""
    db = get_database_manager()
    container = db.get_container_client(container_name)
    try:
        container.delete_item(item=id, partition_key=id)
        print(f"Item {id} soft-deleted from container {container_name}.")
    except Exception as e:
        print(f"Error deleting item: {e}")


def db_purge(containers: Optional[list[str]] = None, run_indexer: bool = False):
    """Purge soft-deleted items from the database."""
    gc = GarbageCollector()
    containers = containers or ContainerNames.data_containers()
    for container_name in containers:
        try:
            start_count = gc.get_item_count(container_name)
            if run_indexer:
                gc.run_indexer_and_purge(container_name)
            else:
                gc.purge_items(container_name)
            final_count = gc.get_item_count(container_name)
            if start_count - final_count:
                print(
                    f"Soft-deleted items purged from container {container_name}. {start_count - final_count} items removed."
                )
            else:
                print(f"No soft-deleted items to purge from container {container_name}.")
        except Exception as e:
            print(f"Error purging container: {e}")


def get_apiview_comments(review_id: str, environment: str = "production") -> dict:
    """
    Retrieves comments for a specific APIView review and returns them grouped by element ID and
    sorted by CreatedOn time. Omits resolved and deleted comments, and removes unnecessary fields.
    """
    return _get_apiview_comments(review_id, environment)


def get_active_reviews(start_date: str, end_date: str, language: str, environment: str = "production") -> list:
    """
    Retrieves active APIView reviews in the specified environment during the specified period.
    """
    reviews = _get_active_reviews(start_date, end_date, environment)
    pretty_language = get_language_pretty_name(language).lower()

    filtered = [r for r in reviews if r.language.lower() == pretty_language]
    for r in filtered:
        del r.language
    print(f"Found {len(filtered)} reviews in {pretty_language} between {start_date} and {end_date}.")
    return filtered


def report_metrics(start_date: str, end_date: str, environment: str = "production", markdown: bool = False) -> dict:
    """Generate a report of APIView metrics between two dates."""
    return get_metrics_report(start_date, end_date, environment, markdown)


def grant_permissions(assignee_id: str = None):
    """
    Grants permissions for running AVC locally.
    """
    from src._permissions import (
        PrincipalType,
        assign_cosmosdb_roles,
        assign_keyvault_access,
        assign_rbac_roles,
        get_current_user_object_id,
    )

    if not assignee_id:
        assignee_id = get_current_user_object_id()

    if not assignee_id:
        raise ValueError("Error: Could not determine the current user ID. Provide `--assignee-id` or run `az login`.")

    subscription_id = "a18897a6-7e44-457d-9260-f2854c0aca42"

    # grant permissions for App Configuration Data Reader
    for rg_name in ["apiview-copilot", "apiview-copilot-staging"]:
        assign_rbac_roles(
            roles=["App Configuration Data Reader"],
            principal_id=assignee_id,
            principal_type=PrincipalType.USER,
            subscription_id=subscription_id,
            rg_name=rg_name,
        )

        # grant permissions for Search Index Data Reader
        assign_rbac_roles(
            roles=["Search Index Data Reader"],
            principal_id=assignee_id,
            principal_type=PrincipalType.USER,
            subscription_id=subscription_id,
            rg_name=rg_name,
        )

        # grant CosmosDB permissions
        cosmos_name = "avc-cosmos" if rg_name == "apiview-copilot" else "avc-cosmos-staging"
        assign_cosmosdb_roles(
            principal_id=assignee_id,
            principal_type=PrincipalType.USER,
            subscription_id=subscription_id,
            rg_name=rg_name,
            role_kind="readOnly",
            cosmos_account_name=cosmos_name,
        )

        # grant KeyVault access
        keyvault_name = "avc-vault" if rg_name == "apiview-copilot" else "avc-vault-staging"
        assign_keyvault_access(
            principal_id=assignee_id,
            subscription_id=subscription_id,
            rg_name=rg_name,
            vault_name=keyvault_name,
            tenant_id="72f988bf-86f1-41af-91ab-2d7cd011db47",
        )

    rg_name = "azsdk-engsys-ai"
    # grant permissions for Cognitive Services OpenAI User on the OpenAI resource
    assign_rbac_roles(
        roles=["Cognitive Services OpenAI User"],
        principal_id=assignee_id,
        principal_type=PrincipalType.USER,
        subscription_id=subscription_id,
        rg_name=rg_name,
        scope=f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}/providers/Microsoft.CognitiveServices/accounts/azsdk-engsys-openai",
    )

    # grant permissions for Azure AI User on the Foundry resource
    assign_rbac_roles(
        roles=["Azure AI User"],
        principal_id=assignee_id,
        principal_type=PrincipalType.USER,
        subscription_id=subscription_id,
        rg_name=rg_name,
        scope=f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}/providers/Microsoft.CognitiveServices/accounts/azsdk-engsys-ai",
    )
    print("✅ Permissions granted. Please run `az logout` and then `az login` to refresh your access.")


def revoke_permissions(assignee_id: str = None):
    """
    Revokes permissions for running AVC locally.
    """
    from azure.mgmt.resource import ManagementLockClient
    from src._credential import get_credential
    from src._permissions import (
        get_current_user_object_id,
        revoke_cosmosdb_roles,
        revoke_keyvault_access,
        revoke_rbac_roles,
    )

    if not assignee_id:
        assignee_id = get_current_user_object_id()

    if not assignee_id:
        raise ValueError("Error: Could not determine the current user ID. Provide `--assignee-id` or run `az login`.")

    subscription_id = "a18897a6-7e44-457d-9260-f2854c0aca42"
    credential = get_credential()
    lock_client = ManagementLockClient(credential, subscription_id)

    # temporarily delete the delete locks
    for rg_name in ["apiview-copilot", "apiview-copilot-staging", "azsdk-engsys-ai"]:
        locks = lock_client.management_locks.list_at_resource_group_level(rg_name)
        for lock in locks:
            if lock.level == "CanNotDelete":
                lock_client.management_locks.delete_at_resource_group_level(rg_name, lock.name)
                print(f"✅ Removed 'CanNotDelete' lock '{lock.name}' from resource group '{rg_name}'...")

    for rg_name in ["apiview-copilot", "apiview-copilot-staging"]:
        revoke_rbac_roles(
            roles=[
                "App Configuration Data Reader",
                "Search Index Data Reader",
                "DocumentDB Account Contributor",
            ],
            principal_id=assignee_id,
            subscription_id=subscription_id,
            rg_name=rg_name,
        )
        cosmos_name = "avc-cosmos" if rg_name == "apiview-copilot" else "avc-cosmos-staging"
        revoke_cosmosdb_roles(
            principal_id=assignee_id,
            subscription_id=subscription_id,
            rg_name=rg_name,
            cosmos_account_name=cosmos_name,
        )

        keyvault_name = "avc-vault" if rg_name == "apiview-copilot" else "avc-vault-staging"
        revoke_keyvault_access(
            principal_id=assignee_id, subscription_id=subscription_id, rg_name=rg_name, vault_name=keyvault_name
        )

    rg_name = "azsdk-engsys-ai"
    revoke_rbac_roles(
        roles=["Cognitive Services OpenAI User"],
        principal_id=assignee_id,
        subscription_id=subscription_id,
        rg_name=rg_name,
        scope=f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}/providers/Microsoft.CognitiveServices/accounts/azsdk-engsys-openai",
    )
    revoke_rbac_roles(
        roles=["Azure AI User"],
        principal_id=assignee_id,
        subscription_id=subscription_id,
        rg_name=rg_name,
        scope=f"/subscriptions/{subscription_id}/resourceGroups/{rg_name}/providers/Microsoft.CognitiveServices/accounts/azsdk-engsys-ai",
    )

    # recreate the deleted locks
    for rg_name in ["apiview-copilot", "apiview-copilot-staging", "azsdk-engsys-ai"]:
        lock_name = f"lock-{rg_name}"
        lock_client.management_locks.create_or_update_at_resource_group_level(
            rg_name,
            lock_name=lock_name,
            parameters={
                "level": "CanNotDelete",
            },
        )
        print(f"✅ Re-created 'CanNotDelete' lock for resource group '{rg_name}'...")


class CliCommandsLoader(CLICommandsLoader):
    """Loader for CLI commands related to APIView and review management."""

    # COMMAND REGISTRATION

    def load_command_table(self, args):
        with CommandGroup(self, "apiview", "__main__#{}") as g:
            g.command("get-comments", "get_apiview_comments")
            g.command("get-active-reviews", "get_active_reviews")
        with CommandGroup(self, "review", "__main__#{}") as g:
            g.command("generate", "generate_review")
            g.command("start-job", "review_job_start")
            g.command("get-job", "review_job_get")
            g.command("summarize", "review_summarize")
        with CommandGroup(self, "agent", "__main__#{}") as g:
            g.command("mention", "handle_agent_mention")
            g.command("chat", "handle_agent_chat")
            g.command("resolve-thread", "handle_agent_thread_resolution")
        with CommandGroup(self, "eval", "__main__#{}") as g:
            g.command("run", "run_test_case")
            g.command("create", "create_test_case")
            g.command("deconstruct", "deconstruct_test_case")
        with CommandGroup(self, "app", "__main__#{}") as g:
            g.command("deploy", "deploy_flask_app")
        with CommandGroup(self, "search", "__main__#{}") as g:
            g.command("kb", "search_knowledge_base")
            g.command("reindex", "reindex_search")
        with CommandGroup(self, "db", "__main__#{}") as g:
            g.command("get", "db_get")
            g.command("delete", "db_delete")
            g.command("purge", "db_purge")
        with CommandGroup(self, "metrics", "__main__#{}") as g:
            g.command("report", "report_metrics")
        with CommandGroup(self, "permissions", "__main__#{}") as g:
            g.command("grant", "grant_permissions")
            g.command("revoke", "revoke_permissions")
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
            ac.argument(
                "start_date",
                type=str,
                help="The start date (YYYY-MM-DD).",
                options_list=["--start-date", "-s"],
            )
            ac.argument(
                "end_date",
                type=str,
                help="The end date (YYYY-MM-DD).",
                options_list=["--end-date", "-e"],
            )
            ac.argument(
                "environment",
                type=str,
                help="The APIView environment. Defaults to 'production'.",
                options_list=["--environment"],
                default="production",
                choices=["production", "staging"],
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
                # pylint: disable=line-too-long
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
        with ArgumentsContext(self, "eval") as ac:
            ac.argument(
                "test_case",
                type=str,
                help="The name of the test case.",
                options_list=["--test-case", "-c"],
            )
            ac.argument(
                "language",
                type=str,
                help="The language of the test case.",
                options_list=["--language", "-l"],
                choices=SUPPORTED_LANGUAGES,
            )
            ac.argument(
                "test_file",
                type=str,
                options_list=["--test-file", "-f"],
                help="The full path to the JSONL test file.",
            )
        with ArgumentsContext(self, "eval run") as ac:
            ac.argument(
                "num_runs", type=int, options_list=["--num-runs", "-n"], help="Number of times to run the test case."
            )
        with ArgumentsContext(self, "eval create") as ac:
            ac.argument("test_case", type=str, help="The name of the test case")
            ac.argument(
                "apiview_path",
                type=str,
                help="The full path to the txt file containing the APIView text",
            )
            ac.argument(
                "expected_path",
                type=str,
                help="The full path to the expected JSON output from the AI reviewer.",
            )
            ac.argument(
                "test_file",
                type=str,
                options_list=["--test-file", "-f"],
                help="The full path to the JSONL test file. Can be an existing test file, or will create a new one.",
            )
            ac.argument(
                "overwrite",
                action="store_true",
                help="Overwrite the test case if it already exists.",
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
                choices=ContainerNames.data_containers(),
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
        with ArgumentsContext(self, "agent") as ac:
            ac.argument(
                "thread_id",
                type=str,
                help="The thread ID to continue the discussion. If not provided, a new thread will be created.",
                options_list=["--thread-id", "-t"],
            )
            ac.argument(
                "comments_path",
                type=str,
                help="Path to the JSON file containing comments for the agent to process.",
                options_list=["--comments-path", "-c"],
            )
        with ArgumentsContext(self, "db") as ac:
            ac.argument(
                "container_name",
                type=str,
                help="The name of the Cosmos DB container",
                choices=ContainerNames.values(),
                options_list=["--container-name", "-c"],
            )
            ac.argument(
                "id",
                type=str,
                help="The id of the item.",
                options_list=["--id", "-i"],
            )
        with ArgumentsContext(self, "db purge") as ac:
            ac.argument(
                "containers",
                type=str,
                nargs="*",
                help="The names of the containers to purge. If not provided, all containers will be purged.",
                options_list=["--containers", "-c"],
                choices=ContainerNames.data_containers(),
            )
            ac.argument(
                "run_indexer",
                help="Whether to run the search indexer before purging.",
                options_list=["--run-indexer"],
                action="store_true",
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
        with ArgumentsContext(self, "metrics report") as ac:
            ac.argument("start_date", help="The start date for the metrics report (YYYY-MM-DD).")
            ac.argument("end_date", help="The end date for the metrics report (YYYY-MM-DD).")
            ac.argument(
                "environment",
                help="The APIView environment from which to calculate the metrics report. Defaults to 'production'.",
            )
        with ArgumentsContext(self, "permissions") as ac:
            ac.argument(
                "assignee_id",
                type=str,
                help="The user ID of the assignee. If not provided, defaults to the current user.",
                options_list=["--assignee-id", "-a"],
                default=None,
            )
        super(CliCommandsLoader, self).load_arguments(command)


def run_cli():
    """Run the CLI application."""
    cli = CLI(cli_name="avc", commands_loader_cls=CliCommandsLoader)
    exit_code = cli.invoke(sys.argv[1:])
    sys.exit(exit_code)


class CustomJSONEncoder(json.JSONEncoder):
    """Custom JSON encoder to handle objects with `to_dict` or `__dict__` methods."""

    def default(self, o):
        # If the object has a `to_dict` method, use it
        if hasattr(o, "to_dict"):
            return o.to_dict()
        # If the object has a `__dict__` attribute, use it
        elif hasattr(o, "__dict__"):
            return o.__dict__
        # Otherwise, use the default serialization
        return super().default(o)


if __name__ == "__main__":
    run_cli()
