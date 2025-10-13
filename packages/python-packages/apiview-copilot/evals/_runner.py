import json
import os
import pathlib
import sys
import tempfile
from typing import List

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

import evals._custom
from azure.ai.evaluation import evaluate
from azure.identity import AzurePipelinesCredential
from evals._config_loader import (
    WorkflowConfigError,
    get_evaluator_class,
    load_workflow_config,
)
from src._settings import SettingsManager
from tabulate import tabulate

DEFAULT_NUM_RUNS: int = 1


class EvalRunner:
    """Class to run evals for APIView copilot."""

    def __init__(self, *, test_path: str, num_runs: int = DEFAULT_NUM_RUNS):
        self.test_path = test_path
        self.num_runs = num_runs
        self.settings = SettingsManager()
        self._tests_directory = pathlib.Path(test_path)
        try:
            self.config = load_workflow_config(self._tests_directory)
        except WorkflowConfigError as e:
            raise ValueError(f"Invalid workflow config: {e}") from e
        self._jsonl_file = self.create_temporary_jsonl_file(self._tests_directory)

    def run(self):
        """Run the evaluation over the resolved test files."""
        evaluator_class = get_evaluator_class(self.config.kind)
        evaluator = evaluator_class(self.config)
        guideline_ids = set()
        all_run_results = []

        azure_ai_project = {
            "subscription_id": self.settings.get("EVALS_SUBSCRIPTION"),
            "resource_group_name": self.settings.get("EVALS_RG"),
            "project_name": self.settings.get("EVALS_PROJECT_NAME"),
        }

        if self.in_ci():
            service_connection_id = os.environ.get("AZURESUBSCRIPTION_SERVICE_CONNECTION_ID")
            client_id = os.environ.get("AZURESUBSCRIPTION_CLIENT_ID")
            tenant_id = os.environ.get("AZURESUBSCRIPTION_TENANT_ID")
            system_access_token = os.environ.get("SYSTEM_ACCESSTOKEN")
            kwargs = {
                "credential": AzurePipelinesCredential(
                    service_connection_id=service_connection_id,
                    client_id=client_id,
                    tenant_id=tenant_id,
                    system_access_token=system_access_token,
                )
            }
        else:
            kwargs = {}

        evaluator._jsonl_file = self._jsonl_file
        if not self._jsonl_file.exists():
            raise ValueError(f"Test file not found: {self._jsonl_file}")

        file_run_results = []
        for run in range(self.num_runs):
            print(f"Running evals {run + 1}/{self.num_runs} for {self._jsonl_file.name}...")
            result = evaluate(
                data=str(self._jsonl_file),
                evaluators={"metrics": evaluator},
                evaluator_config={"metrics": evaluator.evaluator_config},
                target=evaluator.target_function,
                fail_on_evaluator_errors=False,
                # FIXME: Enable this?
                # azure_ai_project=azure_ai_project,
                **kwargs,
            )
            file_run_results.append({self._jsonl_file.name: result})
        all_run_results.extend(file_run_results)

        if not all_run_results:
            raise ValueError("No results produced.")

        # Delegate all result processing to the evaluator
        processed_results = evaluator.process_results(all_run_results, guideline_ids)
        evaluator.show_results(processed_results)
        evaluator.post_process(
            processed_results,
            str(self._tests_directory),
            guideline_ids,
        )

    def create_temporary_jsonl_file(self, directory: pathlib.Path) -> pathlib.Path:
        """
        Collect all test files with the *.json extension in the given directory
        and store them as a single JSONL file in a temporary directory.

        Each source JSON file is parsed and written as one compact JSON object per line.
        Returns the Path to the created JSONL file.
        """
        if not directory.exists() or not directory.is_dir():
            raise ValueError(f"Test directory not found: {directory}")

        test_files = sorted(directory.glob("*.json"))
        if not test_files:
            raise ValueError(f"No test files found in: {directory}")

        tmp_dir = pathlib.Path(tempfile.mkdtemp(prefix="evals_jsonl_"))
        out_path = tmp_dir / f"{directory.name}.jsonl"

        with out_path.open("w", encoding="utf-8") as out_f:
            for src in test_files:
                try:
                    with src.open("r", encoding="utf-8") as f:
                        obj = json.load(f)
                except Exception as e:
                    raise ValueError(f"Failed to read/parse test file {src}: {e}") from e
                out_f.write(json.dumps(obj, separators=(",", ":"), ensure_ascii=False))
                out_f.write("\n")

        return out_path

    def in_ci(self) -> bool:
        return bool(os.getenv("TF_BUILD"))
