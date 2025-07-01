import asyncio
from collections import OrderedDict
import json
import os
from pprint import pprint
import requests
import sys
import pathlib

from src._search_manager import SearchManager

from knack import CLI, ArgumentsContext, CLICommandsLoader
from knack.commands import CommandGroup
from knack.help_files import helps
from typing import Optional

helps[
    "review"
] = """
    type: group
    short-summary: Commands for creating APIView reviews.
"""

helps[
    "review job"
] = """
    type: group
    short-summary: Commands for managing API review jobs.
"""

helps[
    "agent"
] = """
    type: group
    short-summary: Commands for interacting with the agent.
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


def local_review(
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


def generate_review_from_app(
    language: str,
    target: str,
    base: Optional[str] = None,
    outline: Optional[str] = None,
    existing_comments: Optional[str] = None,
):
    """Generates a review using the deployed Flask app."""
    from scripts.remote_review import generate_remote_review

    # Read the file content
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

    response = asyncio.run(
        generate_remote_review(
            target=target_content,
            base=base_content,
            language=language,
            outline=outline_text,
            existing_comments=comments_obj,
        )
    )

    # response is already a dict, no need to parse it
    if isinstance(response, dict):
        pprint(response, indent=2)
    else:
        # Handle error responses which are strings
        print(response)


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
        pprint(resp.json(), indent=2)
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
        pprint(resp.json(), indent=2)
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
    context = search.build_context(results)
    if markdown:
        md = context.to_markdown()
        print(md)
    else:
        print(json.dumps(context, indent=2, cls=CustomJSONEncoder))


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


def ask_agent():
    """
    Start an interactive session with the agent.
    This function is a placeholder for the actual implementation.
    """
    # Here you would implement the logic to interact with the agent
    # For example, using an AI service or a custom agent implementation
    # Simulate a response
    print("Simulating agent response...")
    return "Agent response"


def handle_agent_mention(comments_path: str):
    """
    Handles @mention requests from the agent.
    This function is a placeholder for the actual implementation.
    """
    # load comments from the comments_path
    comments = []
    if os.path.exists(comments_path):
        with open(comments_path, "r", encoding="utf-8") as f:
            comments = json.load(f)
    else:
        print(f"Comments file {comments_path} does not exist.")
        return
    print("Handling agent mention...")


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
    def load_command_table(self, args):
        with CommandGroup(self, "review", "__main__#{}") as g:
            g.command("local", "local_review")
            g.command("remote", "generate_review_from_app")
            g.command("summarize", "review_summarize")
        with CommandGroup(self, "agent", "__main__#{}") as g:
            g.command("mention", "handle_agent_mention")
            g.command("ask", "ask_agent")
        with CommandGroup(self, "eval", "__main__#{}") as g:
            g.command("create", "create_test_case")
            g.command("deconstruct", "deconstruct_test_case")
        with CommandGroup(self, "app", "__main__#{}") as g:
            g.command("deploy", "deploy_flask_app")
        with CommandGroup(self, "search", "__main__#{}") as g:
            g.command("kb", "search_knowledge_base")
        with CommandGroup(self, "review job", "__main__#{}") as g:
            g.command("start", "review_job_start")
            g.command("get", "review_job_get")
        return OrderedDict(self.command_table)

    def load_arguments(self, command):
        with ArgumentsContext(self, "") as ac:
            ac.argument(
                "language",
                type=str,
                help="The language of the APIView file",
                options_list=("--language", "-l"),
                choices=SUPPORTED_LANGUAGES,
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
        with ArgumentsContext(self, "review job start") as ac:
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

        with ArgumentsContext(self, "review job get") as ac:
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
