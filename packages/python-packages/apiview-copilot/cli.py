from collections import OrderedDict
import json
import os
from pprint import pprint
import sys
import pathlib

from knack import CLI, ArgumentsContext, CLICommandsLoader
from knack.commands import CommandGroup
from knack.help_files import helps
from typing import Literal, Optional

helps[
    "review"
] = """
    type: group
    short-summary: Commands related to APIView reviews.
"""

helps[
    "eval"
] = """
    type: group
    short-summary: Commands related to APIView copilot evals.
"""

helps[
    "app"
] = """
    type: group
    short-summary: Commands related to the Flask app deployment.
"""


def generate_review(
    language: str,
    path: str,
    model: Literal["gpt-4o-mini", "o3-mini"],
    chunk_input: bool = False,
    log_prompts: bool = False,
    use_rag: bool = False,
):
    """
    Generate a review for an APIView
    """
    from src._apiview_reviewer import ApiViewReview

    rg = ApiViewReview(language=language, model=model, log_prompts=log_prompts)
    filename = os.path.splitext(os.path.basename(path))[0]

    with open(path, "r") as f:
        apiview = f.read()
    review = rg.get_response(apiview, chunk_input=chunk_input, use_rag=use_rag)
    output_path = os.path.join("scratch", "output", language)
    if not os.path.exists(output_path):
        os.makedirs(output_path)
    with open(os.path.join(output_path, f"{filename}.json"), "w") as f:
        f.write(review.model_dump_json(indent=4))
    print(f"Review saved to {os.path.join(output_path, f'{filename}.json')}")
    print(f"Found {len(review.violations or [])} violations")


def create_test_case(
    language: str,
    test_case: str,
    apiview_path: str,
    expected_path: str,
    test_file: str,
    overwrite: bool = False,
):
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
    for violation in expected_contents["violations"]:
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
    deconstructed_apiview = (
        pathlib.Path(__file__).parent
        / "evals"
        / "tests"
        / language
        / f"{test_case}.txt"
    )
    deconstructed_expected = (
        pathlib.Path(__file__).parent
        / "evals"
        / "tests"
        / language
        / f"{test_case}.json"
    )
    with open(deconstructed_apiview, "w") as f:
        f.write(apiview)

    with open(deconstructed_expected, "w") as f:
        f.write(expected)

    print(
        f"Deconstructed test case '{test_case}' into {deconstructed_apiview} and {deconstructed_expected}."
    )


def deploy_flask_app(
    app_name: Optional[str] = None,
    resource_group: Optional[str] = None,
    subscription_id: Optional[str] = None,
):
    """Command to deploy the Flask app."""
    from scripts.deploy_solution import deploy_to_azure

    deploy_to_azure(app_name, resource_group, subscription_id)


class CliCommandsLoader(CLICommandsLoader):
    def load_command_table(self, args):
        with CommandGroup(self, "review", "__main__#{}") as g:
            g.command("generate", "generate_review")
        with CommandGroup(self, "eval", "__main__#{}") as g:
            g.command("create", "create_test_case")
            g.command("deconstruct", "deconstruct_test_case")
        with CommandGroup(self, "app", "__main__#{}") as g:
            g.command("deploy", "deploy_flask_app")
        return OrderedDict(self.command_table)

    def load_arguments(self, command):
        with ArgumentsContext(self, "") as ac:
            ac.argument(
                "language",
                type=str,
                help="The language of the APIView file",
                options_list=("--language", "-l"),
            )
        with ArgumentsContext(self, "review") as ac:
            ac.argument("path", type=str, help="The path to the APIView file")
            ac.argument(
                "log_prompts",
                action="store_true",
                help="Log each prompt in ascending order in the `scratch/propmts` folder.",
            )
            ac.argument(
                "model",
                type=str,
                help="The model to use for the review",
                options_list=("--model", "-m"),
                choices=["gpt-4o-mini", "o3-mini"],
            )
            ac.argument(
                "chunk_input",
                action="store_true",
                help="Chunk the input into smaller sections (currently, by class).",
            )
            ac.argument(
                "use_rag",
                action="store_true",
                help="Use RAG pattern to generate the review.",
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
            ac.argument(
                "test_case", type=str, help="The specific test case to deconstruct."
            )
            ac.argument(
                "test_file", type=str, help="The full path to the JSONL test file."
            )
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
        super(CliCommandsLoader, self).load_arguments(command)


def run_cli():
    cli = CLI(cli_name="apiviewcopilot", commands_loader_cls=CliCommandsLoader)
    exit_code = cli.invoke(sys.argv[1:])
    sys.exit(exit_code)


if __name__ == "__main__":
    run_cli()
