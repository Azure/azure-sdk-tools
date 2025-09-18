import os
import pathlib
import sys
from typing import List

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from azure.ai.evaluation import evaluate
from azure.identity import AzurePipelinesCredential
from evals._custom import EVALUATORS
from _config_loader import load_workflow_config, WorkflowConfigError
from src._settings import SettingsManager
from tabulate import tabulate

DEFAULT_NUM_RUNS: int = 1

class EvalRunner:
    """Class to run evals for APIView copilot."""

    def __init__(self, *, language: str, test_path: str, num_runs: int = DEFAULT_NUM_RUNS):
        self.language = language
        self.test_path = test_path
        self.num_runs = num_runs
        self.settings = SettingsManager()

        self._workflow_config = None
        self._is_workflow = False

        path_obj = pathlib.Path(test_path)
        
        try:
            self._workflow_config = load_workflow_config(path_obj)
        except WorkflowConfigError as e:
            raise ValueError(f"Invalid workflow config: {e}") from e
        self._is_workflow = True

        self._tests_directory = self._workflow_config.tests_path.parent
        self._test_files: List[pathlib.Path] = [self._workflow_config.tests_path]
        
        evaluation_kind = self._workflow_config.kind
        self._evaluator_class = EVALUATORS[evaluation_kind]

    def run(self):
        """Run the evaluation over the resolved test files."""
        custom_eval = self._evaluator_class()
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

        for file in self._test_files:
            if not file.exists():
                raise ValueError(f"Test file not found: {file}")
            file_run_results = []
            for run in range(self.num_runs):
                print(f"Running evals {run + 1}/{self.num_runs} for {file.name}...")
                result = evaluate(
                    data=str(file),
                    evaluators={"metrics": custom_eval},
                    evaluator_config={"metrics": custom_eval.evaluator_config},
                    target=custom_eval.target_function,
                    # FIXME: Should this be True? Probably?
                    fail_on_evaluator_errors=False,
                    # azure_ai_project=azure_ai_project,
                    **kwargs,
                )
                file_run_results.append({file.name: result})
            all_run_results.extend(file_run_results)

        if not all_run_results:
            raise ValueError("No results produced.")

        # Delegate all result processing to the evaluator
        processed_results = custom_eval.process_results(all_run_results, guideline_ids)
        custom_eval.show_results(processed_results)
        custom_eval.post_process(
            processed_results,
            self.language,
            str(self._tests_directory),
            guideline_ids,
        )

    def in_ci(self) -> bool:
        return bool(os.getenv("TF_BUILD"))
