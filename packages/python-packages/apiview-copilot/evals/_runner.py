import os
import tempfile
import json
import sys
from typing import Optional, List
from pathlib import Path

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from azure.ai.evaluation import evaluate
from azure.identity import AzurePipelinesCredential
from src._settings import SettingsManager
from tabulate import tabulate

import evals._custom # Unused but needed to instantiate custom evaluators
from evals._config_loader import (
    WorkflowConfig, 
    WorkflowConfigError, 
    load_workflow_config, 
    load_workflow_directory, 
    get_evaluator_class
)

DEFAULT_NUM_RUNS: int = 1

class EvalRunner:
    """Evaluation runner for APIView copilot workflows.
    
    Supports both single-workflow execution and batch processing of all
    workflows for a given language. Handles test case filtering, multiple
    runs, and result aggregation.
    
    Args:
        language: Programming language to evaluate (e.g., 'python', 'java')
        test_path: Path to specific workflow YAML file (optional)
        test_cases: List of test case names to filter (requires test_path)
        num_runs: Number of evaluation runs to perform per test file
        
    Raises:
        ValueError: When inputs are invalid or incompatible
    """

    def __init__(self, *, language: str, test_path: Optional[str], test_cases: Optional[List[str]] = None, num_runs: int = DEFAULT_NUM_RUNS):
        self.language = language
        self.test_path = Path(test_path) if test_path else None
        self.test_cases = test_cases
        self.num_runs = num_runs
        self.settings = SettingsManager()

        self._workflow_configs: List[WorkflowConfig] = []

        if self.test_path is None:
            # No test file specified - discover all workflows for specified language
            self._workflow_configs = self._discover_all_workflows()
        else:
            try:
                workflow_config = load_workflow_config(self.test_path)
                self._workflow_configs = [workflow_config]
            except WorkflowConfigError as e:
                raise ValueError(f"Invalid workflow config: {e}") from e

        if not self._workflow_configs:
            raise ValueError(f"No valid workflows found for language '{language}'")

    def run(self):
        """Orchestrate the evaluation process."""
        credentials = self._setup_credentials()
        all_workflow_results = self._execute_workflows(credentials)
        self._present_results(all_workflow_results)

    def _setup_credentials(self) -> dict:
        """Setup Azure credentials and evaluation configuration."""
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
            return {
                "credential": AzurePipelinesCredential(
                    service_connection_id=service_connection_id,
                    client_id=client_id,
                    tenant_id=tenant_id,
                    system_access_token=system_access_token,
                )
            }
        else:
            return {}

    def _execute_workflows(self, credentials: dict) -> dict:
        """Execute all configured workflows and collect results."""
        all_workflow_results = {}
        
        for workflow_idx, workflow_config in enumerate(self._workflow_configs):
            print(f"\n=== Running Workflow {workflow_idx + 1}/{len(self._workflow_configs)}: {workflow_config.name} ===")
            
            workflow_result = self._execute_single_workflow(workflow_config, credentials)
            all_workflow_results[workflow_config.name] = workflow_result
            
        return all_workflow_results

    def _execute_single_workflow(self, workflow_config: WorkflowConfig, credentials: dict) -> dict:
        """Execute a single workflow and return its results."""
        evaluator_class = get_evaluator_class(workflow_config.kind)
        evaluator = evaluator_class(workflow_config)
        
        workflow_run_results = []
        temp_files = []
        
        try:
            test_files = [workflow_config.tests_path]
            
            for file in test_files:
                if not file.exists():
                    raise ValueError(f"Test file not found: {file}")

                # Filter JSONL to temporary file with specified testcases or return original file if no filter
                filtered_file_path = self._filter_jsonl_data(file)
                if filtered_file_path != file:
                    temp_files.append(filtered_file_path)
                
                file_run_results = self._execute_test_file_runs(file, filtered_file_path, evaluator, credentials)
                workflow_run_results.extend(file_run_results)
        finally:
            self._cleanup_temp_files(temp_files)

        if not workflow_run_results:
            raise ValueError("No results produced.")

        guideline_ids = set()
        processed_results = evaluator.process_results(workflow_run_results, guideline_ids)

        return {
            'processed_results': processed_results,
            'evaluator': evaluator,
            'workflow_config': workflow_config,
            'guideline_ids': guideline_ids
        }

    def _execute_test_file_runs(self, file: Path, filtered_file_path: Path, evaluator, credentials: dict) -> list:
        """Execute multiple runs for a single test file."""
        file_run_results = []
        
        for run in range(self.num_runs):
            print(f"Running evals {run + 1}/{self.num_runs} for {file.name}...")
            if self.test_cases:
                print(f"  (Filtered to testcases: {[testcase for testcase in self.test_cases]})")
            
            result = evaluate(
                data=str(filtered_file_path),
                evaluators={"metrics": evaluator},
                evaluator_config={"metrics": evaluator.evaluator_config},
                target=evaluator.target_function,
                # FIXME: Should this be True? Probably?
                fail_on_evaluator_errors=False,
                # azure_ai_project=azure_ai_project,
                **credentials
            )
            file_run_results.append({file.name: result})
            
        return file_run_results

    def _cleanup_temp_files(self, temp_files: List[Path]) -> None:
        """Clean up temporary files created during execution."""
        for temp_file in temp_files:
            try:
                temp_file.unlink()
            except OSError as e:
                print(f"Error removing temporary file {temp_file}: {e}")

    def _present_results(self, all_workflow_results: dict) -> None:
        """Present evaluation results and run post-processing."""
        overall_guideline_ids = set()
        
        for workflow_data in all_workflow_results.values():
            overall_guideline_ids.update(workflow_data['guideline_ids'])
        
        self._show_aggregated_results(all_workflow_results)
        self._run_post_processing(all_workflow_results, overall_guideline_ids)

    def in_ci(self) -> bool:
        return bool(os.getenv("TF_BUILD"))
    
    def _discover_all_workflows(self) -> List[WorkflowConfig]:
        """Discover all workflow files for the specified language."""
        if not self.language:
            raise ValueError("Language (-l/--language) must be specified when a workflow file (-f/--test-file) is not provided.")
        if self.test_cases:
            raise ValueError(
                "Cannot specify test cases (-c/--test-cases) without specifying a workflow file (-f/--test-file). "
                "To run specific test cases, provide a workflow file."
            )

        workflows_dir = Path(__file__).parent / "workflows" / self.language
        
        if not workflows_dir.exists():
            raise ValueError(f"No workflows directory found for language '{self.language}' at {workflows_dir}")
        
        try:
            return load_workflow_directory(workflows_dir)
        except WorkflowConfigError as e:
            raise ValueError(f"Error loading workflows from {workflows_dir}: {e}") from e
    
    def _filter_jsonl_data(self, file_path: Path) -> Path:
        """Filter JSONL data by testcase if specified, return path to filtered temp file."""
        if not self.test_cases:
            return file_path
        
        filtered_lines = []
        found_testcases = set()
        
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                for line_num, line in enumerate(f, 1):
                    line = line.strip()
                    if not line:
                        continue
                        
                    try:
                        data = json.loads(line)
                        if data.get('testcase') in self.test_cases:
                            filtered_lines.append(line)
                            found_testcases.add(data.get('testcase'))
                    except json.JSONDecodeError as e:
                        raise ValueError(f"Invalid JSON on line {line_num} in {file_path}: {e}")
                    
            unfound_testcases = set(self.test_cases) - found_testcases
            if unfound_testcases:
                raise ValueError(f"Testcases '{unfound_testcases}' not found in {file_path}")

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
            
            return Path(temp_file.name)
            
        except Exception as e:
            raise ValueError(f"Error filtering testcase data: {e}")
        
    def _show_aggregated_results(self, all_workflow_results: dict) -> None:
        """Show results for all workflows in a unified manner."""
        print(f"\n{'='*80}")
        print(f"Evaluation results summary ({len(all_workflow_results)} workflows)")
        print(f"{'='*80}")
        
        for workflow_name, workflow_data in all_workflow_results.items():
            print(f"\n--- {workflow_name.upper()} ---")
            evaluator = workflow_data['evaluator']
            processed_results = workflow_data['processed_results']
            evaluator.show_results(processed_results)

    def _run_post_processing(self, all_workflow_results: dict, overall_guideline_ids: set) -> None:
        """Run post-processing for all workflows."""
        for workflow_name, workflow_data in all_workflow_results.items():
            evaluator = workflow_data['evaluator']
            processed_results = workflow_data['processed_results']
            workflow_config = workflow_data['workflow_config']
            guideline_ids = workflow_data['guideline_ids']
            
            tests_directory = workflow_config.tests_path.parent
            
            evaluator.post_process(
                processed_results,
                self.language,
                str(tests_directory),
                guideline_ids,
            )
