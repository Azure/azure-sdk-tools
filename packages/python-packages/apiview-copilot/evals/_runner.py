import json
import os
import pathlib
import sys
from typing import Any, Set

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from azure.ai.evaluation import evaluate
from azure.identity import AzurePipelinesCredential
from evals._custom import EVALUATORS
from src._settings import SettingsManager
from tabulate import tabulate

DEFAULT_NUM_RUNS: int = 1

class EvalRunner:
    """Class to run evals for APIView copilot."""

    def __init__(self, *, language: str, test_path: str, evaluator_type: str = "apiview", num_runs: int = DEFAULT_NUM_RUNS):
        self.language = language
        self.test_path = test_path
        self.evaluator_type = evaluator_type
        self.num_runs = num_runs
        self.settings = SettingsManager()

        if evaluator_type not in EVALUATORS:
            raise ValueError(f"Unknown evaluator type: {evaluator_type}. Available: {list(EVALUATORS.keys())}")

        self._tests_directory = pathlib.Path(__file__).parent / "tests" / self.language
        self._test_file = pathlib.Path(test_path).name

    def run(self):
        """Run the evaluation."""
        evaluator_class = EVALUATORS[self.evaluator_type]
        custom_eval = evaluator_class()
        guideline_ids = set()

        all_run_results = []
        for file in self._tests_directory.glob("*.jsonl"):
            if self._test_file != "all" and file.name != self._test_file:
                continue

            azure_ai_project = {
                "subscription_id": self.settings.get("EVALS_SUBSCRIPTION"),
                "resource_group_name": self.settings.get("EVALS_RG"),
                "project_name": self.settings.get("EVALS_PROJECT_NAME"),
            }
            if self.in_ci():
                service_connection_id = os.environ["AZURESUBSCRIPTION_SERVICE_CONNECTION_ID"]
                client_id = os.environ["AZURESUBSCRIPTION_CLIENT_ID"]
                tenant_id = os.environ["AZURESUBSCRIPTION_TENANT_ID"]
                system_access_token = os.environ["SYSTEM_ACCESSTOKEN"]
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

            # TODO: Multiple runs are running in series and also in series for multiple files. They could be made to run in parallel to speed processing.
            file_run_results = []
            for run in range(self.num_runs):
                print(f"Running evals {run + 1}/{self.num_runs} for {file.name}...")
                result = evaluate(
                    data=str(file),
                    # FIXME: Need to quickly ensure that the metrics submission is pickleable. If not, fail fast
                    evaluators={
                        "metrics": custom_eval,
                    },
                    # Use the evaluator's own configuration
                    evaluator_config={
                        "metrics": custom_eval.evaluator_config,
                    },
                    target=custom_eval.target_function,
                    # FIXME: Should this be True? Probably?
                    fail_on_evaluator_errors=False,
                    azure_ai_project=azure_ai_project,
                    **kwargs,
                )
                file_run_results.append({file.name: result})

            all_run_results.extend(file_run_results)

        if not all_run_results:
            raise ValueError(f"No tests found in: {self._test_file}")

        # Delegate all result processing to the evaluator
        processed_results = custom_eval.process_results(all_run_results, guideline_ids)
        custom_eval.show_results(processed_results)
        custom_eval.post_process(processed_results, self.language, str(self._tests_directory), self._test_file, guideline_ids)

    def in_ci(self) -> bool:
        return bool(os.getenv("TF_BUILD"))
