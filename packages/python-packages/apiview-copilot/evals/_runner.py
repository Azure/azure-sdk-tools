import os
import tempfile
import json
import pathlib
import sys
from typing import List

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from azure.ai.evaluation import evaluate
from azure.identity import AzurePipelinesCredential
from src._settings import SettingsManager
from tabulate import tabulate

from evals._config_loader import load_workflow_config, WorkflowConfigError, get_evaluator_class
import evals._custom

DEFAULT_NUM_RUNS: int = 1

class EvalRunner:
    """Class to run evals for APIView copilot."""

    def __init__(self, *, language: str, test_path: str, testcase: list[str] = None, num_runs: int = DEFAULT_NUM_RUNS):
        self.language = language
        self.test_path = test_path
        self.testcase = testcase
        self.num_runs = num_runs
        self.settings = SettingsManager()

        self._workflow_config = None
        self._is_workflow = False

        path_obj = pathlib.Path(test_path)
        
        try:
            self._workflow_config = load_workflow_config(path_obj)
        except WorkflowConfigError as e:
            raise ValueError(f"Invalid workflow config: {e}") from e

        self._tests_directory = self._workflow_config.tests_path.parent
        self._test_files: List[pathlib.Path] = [self._workflow_config.tests_path]
        
        evaluation_kind = self._workflow_config.kind
        self._evaluator_class = get_evaluator_class(evaluation_kind)

    def run(self):
        """Run the evaluation over the resolved test files."""
        evaluator = self._evaluator_class(self._workflow_config)
        guideline_ids = set()
        all_run_results = []
        temp_files = []

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

        try:
            for file in self._test_files:
                if not file.exists():
                    raise ValueError(f"Test file not found: {file}")

                filtered_file_path = self._filter_jsonl_data(str(file))
                if filtered_file_path != str(file):
                    temp_files.append(filtered_file_path)
                
                file_run_results = []
                for run in range(self.num_runs):
                    print(f"Running evals {run + 1}/{self.num_runs} for {file.name}...")
                    if self.testcase:
                        print(f"  (Filtered to testcases: {[testcase for testcase in self.testcase]})")
                    result = evaluate(
                        data=filtered_file_path,
                        evaluators={"metrics": evaluator},
                        evaluator_config={"metrics": evaluator.evaluator_config},
                        target=evaluator.target_function,
                        # FIXME: Should this be True? Probably?
                        fail_on_evaluator_errors=False,
                        # azure_ai_project=azure_ai_project,
                        **kwargs
                    )
                    file_run_results.append({file.name: result})
                all_run_results.extend(file_run_results)
        finally:
            for temp_file in temp_files:
                try:
                    os.unlink(temp_file)
                except Exception as e:
                    print(f"Error removing temporary file {temp_file}: {e}")

        if not all_run_results:
            raise ValueError("No results produced.")

        # Delegate all result processing to the evaluator
        processed_results = evaluator.process_results(all_run_results, guideline_ids)
        evaluator.show_results(processed_results)
        evaluator.post_process(
            processed_results,
            self.language,
            str(self._tests_directory),
            guideline_ids,
        )

    def in_ci(self) -> bool:
        return bool(os.getenv("TF_BUILD"))
    
    def _filter_jsonl_data(self, file_path: str) -> str:
        """Filter JSONL data by testcase if specified, return path to filtered temp file."""
        if not self.testcase:
            return file_path
        
        original_path = pathlib.Path(file_path)
        filtered_lines = []
        found_testcase = False
        
        try:
            with open(original_path, 'r', encoding='utf-8') as f:
                for line_num, line in enumerate(f, 1):
                    line = line.strip()
                    if not line:
                        continue
                        
                    try:
                        data = json.loads(line)
                        if data.get('testcase') in self.testcase:
                            filtered_lines.append(line)
                            found_testcase = True
                    except json.JSONDecodeError as e:
                        raise ValueError(f"Invalid JSON on line {line_num} in {file_path}: {e}")
            
            if not found_testcase:
                raise ValueError(f"Testcase '{self.testcase}' not found in {file_path}")
                
            # Create temporary file with filtered data
            temp_file = tempfile.NamedTemporaryFile(
                mode='w', 
                suffix='.jsonl', 
                delete=False,
                encoding='utf-8'
            )
            
            for line in filtered_lines:
                temp_file.write(line + '\n')
            temp_file.close()
            
            return temp_file.name
            
        except Exception as e:
            raise ValueError(f"Error filtering testcase data: {e}")
