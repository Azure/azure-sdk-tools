import json
import os
import sys
import tempfile
import threading
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from typing import Any, List

import yaml
from azure.ai.evaluation import evaluate
from azure.identity import AzurePipelinesCredential

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

import evals._custom
from evals._config_loader import get_evaluator_class
from evals._discovery import DiscoveryResult, EvaluationTarget
from src._settings import SettingsManager
from evals._util import (
    load_cache_lookup,
    append_results_to_cache,
    get_cache_file_path,
    construct_fake_azure_result
)

DEFAULT_NUM_RUNS: int = 1


class ExecutionContext:
    """Shared context for evaluation execution."""

    def __init__(self):
        self.settings = SettingsManager()
        self._azure_ai_project = {
            "subscription_id": self.settings.get("EVALS_SUBSCRIPTION"),
            "resource_group_name": self.settings.get("EVALS_RG"),
            "project_name": self.settings.get("EVALS_PROJECT_NAME"),
        }
        self._credential_kwargs = self._create_credential_kwargs()
        self._temp_files: list[Path] = []
        self._temp_files_lock = threading.Lock()

    def _create_credential_kwargs(self) -> dict[str, Any]:
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
        return {}

    def in_ci(self) -> bool:
        return bool(os.getenv("TF_BUILD"))

    def _load_test_file(self, test_file: Path) -> dict:
        """Load test file - supports both JSON and YAML formats."""
        try:
            with test_file.open("r", encoding="utf-8") as f:
                if test_file.suffix == ".json":
                    return json.load(f)
                elif test_file.suffix in [".yaml", ".yml"]:
                    return yaml.safe_load(f)
                else:
                    raise ValueError(f"Unsupported file format: {test_file.suffix}")
        except Exception as e:
            raise ValueError(f"Failed to read/parse test file {test_file}: {e}") from e

    def create_temporary_jsonl_file(self, target: EvaluationTarget) -> Path:
        """
        Collect all test files with the *.json extension in the given directory
        and store them as a single JSONL file in a temporary directory.

        Each source JSON file is parsed and written as one compact JSON object per line.
        Returns the Path to the created JSONL file.
        """
        tmp_dir = Path(tempfile.mkdtemp(prefix="evals_executor_"))
        output_name = f"{target.workflow_name}.jsonl"
        out_path = tmp_dir / output_name

        with out_path.open("w", encoding="utf-8") as out_f:
            for test_file in target.test_files:
                obj = self._load_test_file(test_file)
                out_f.write(json.dumps(obj, separators=(",", ":"), ensure_ascii=False))
                out_f.write("\n")

        with self._temp_files_lock:
            self._temp_files.append(out_path)
        return out_path

    def cleanup(self):
        """Clean up temporary files."""
        for temp_file in self._temp_files:
            try:
                if temp_file.exists():
                    temp_file.unlink()
                    # Also try to remove the parent directory if empty
                    try:
                        temp_file.parent.rmdir()
                    except OSError:
                        pass  # Directory not empty or other issue
            except Exception:
                pass


class EvaluationResult:
    """Result of evaluating a single evaluation target."""

    def __init__(self, target: EvaluationTarget, raw_results: list[dict], success: bool, error: str | None = None):
        self.target = target
        self.raw_results = raw_results
        self.success = success
        self.error = error

    @property
    def workflow_name(self) -> str:
        return self.target.workflow_name

    @property
    def num_test_files(self) -> int:
        return len(self.target.test_files)


class EvaluationRunner:
    """Executes evaluations targets with shared context"""

    def __init__(self, *, num_runs: int = DEFAULT_NUM_RUNS, use_cache: bool = False):
        self.num_runs = num_runs
        self._context: ExecutionContext | None = None
        self._results_lock = threading.Lock()
        self._use_cache = use_cache

    def _ensure_context(self):
        if self._context is None:
            self._context = ExecutionContext()

    def run(self, discovery_result: DiscoveryResult) -> List[EvaluationResult]:
        """Execute all targets in the discovery result.

        Args:
            discovery_result: Result from EvaluationDiscovery containing targets to execute.

        Returns:
            List of EvaluationResult objects.
        """
        try:
            self._ensure_context()

            print(f"🚀 Executing {len(discovery_result.targets)} evaluation targets...")
            print(f"📊 {discovery_result.summary}")
            print()

            return self._run_parallel(discovery_result)
        finally:
            self.cleanup()

    def _run_parallel(self, discovery_result: DiscoveryResult) -> List[EvaluationResult]:
        """Parallel execution using ThreadPoolExecutor."""
        cpu_count = os.cpu_count() or 4
        max_workers = min(cpu_count * 2, len(discovery_result.targets))
        results = []
        completed_count = 0
        total_targets = len(discovery_result.targets)

        with ThreadPoolExecutor(max_workers=max_workers) as executor:
            # Submit all tasks
            future_to_target = {
                executor.submit(self._execute_target_with_progress, target, i + 1, total_targets): target
                for i, target in enumerate(discovery_result.targets)
            }

            # Collect results as they complete
            for future in as_completed(future_to_target):
                target = future_to_target[future]

                try:
                    result = future.result()
                    results.append(result)

                    with self._results_lock:
                        completed_count += 1

                    if result.success:
                        print(f"✅ {target.workflow_name} completed successfully ({completed_count}/{total_targets})")
                    else:
                        print(f"❌ {target.workflow_name} failed: {result.error} ({completed_count}/{total_targets})")

                except Exception as e:
                    with self._results_lock:
                        completed_count += 1

                    print(f"💥 {target.workflow_name} crashed: {e} ({completed_count}/{total_targets})")
                    results.append(EvaluationResult(target=target, raw_results=[], success=False, error=str(e)))

                print()  # Spacing

        # Sort results to match original target order
        target_order = {target.workflow_name: i for i, target in enumerate(discovery_result.targets)}
        results.sort(key=lambda r: target_order.get(r.workflow_name, 999))

        return results

    def _execute_target_with_progress(self, target: EvaluationTarget, index: int, total: int) -> EvaluationResult:
        print(f"[{index}/{total}] Started {target.workflow_name}...")
        return self._execute_target(target)

    def _execute_target(self, target: EvaluationTarget) -> EvaluationResult:
        try:
            # Load each test file once and reuse parsed data
            test_file_to_case = {}
            testcase_ids = []
            test_file_paths = []
            
            for test_file in target.test_files:
                test_case = self._context._load_test_file(test_file)
                test_file_to_case[test_file] = test_case
                testcase_id = test_case.get("testcase")
                if testcase_id:
                    testcase_ids.append(testcase_id)
                    test_file_paths.append(test_file)
            
            # Resolve cache strategy
            if self._use_cache:
                cache_lookup = load_cache_lookup(testcase_ids, test_file_paths)
            else:
                cache_lookup = {}
            
            # Partition test data based on cache
            cached_azure_rows = []
            fresh_testcases = []
            fresh_test_file_paths = []
            
            for test_file in target.test_files:
                test_case = test_file_to_case[test_file]
                testcase_id = test_case.get("testcase")
                
                if testcase_id and testcase_id in cache_lookup:
                    cached_azure_rows.append(cache_lookup[testcase_id])
                else:
                    fresh_testcases.append(test_case)
                    fresh_test_file_paths.append(test_file)
            
            # Execute fresh testcases if needed
            fresh_results = []
            if fresh_testcases:
                fresh_results = self._run_azure_evaluation(fresh_testcases, target)
                
                if self._use_cache: 
                    append_results_to_cache(fresh_test_file_paths, fresh_results)
            
            # Combine all results
            cached_rows = [row for row in cached_azure_rows]
            fresh_rows = [row for result in fresh_results for row in result.get("rows", [])]
            all_cached_rows = cached_rows + fresh_rows
            combined_result = construct_fake_azure_result(all_cached_rows)
            
            return EvaluationResult(
                target=target,
                raw_results=[{f"{target.workflow_name}.jsonl": combined_result}],
                success=True,
            )
            
        except Exception as e:
            return EvaluationResult(target=target, raw_results=[], success=False, error=str(e))

    def _run_azure_evaluation(self, testcases: list[dict], target: EvaluationTarget) -> list[dict]:
        """Run Azure AI evaluation on a list of testcases."""
        if not testcases:
            return []
        
        # Create temporary file for testcases
        tmp_dir = Path(tempfile.mkdtemp(prefix="evals_fresh_"))
        fresh_jsonl = tmp_dir / f"fresh_{target.workflow_name}.jsonl"
        
        with fresh_jsonl.open("w", encoding="utf-8") as f:
            for test_case in testcases:
                f.write(json.dumps(test_case, separators=(",", ":"), ensure_ascii=False) + "\n")
        
        # Execute evaluation
        evaluator_class = get_evaluator_class(target.config.kind)
        evaluator = evaluator_class(target.config, jsonl_file=fresh_jsonl)
        
        results = []
        for run in range(self.num_runs):
            if self.num_runs > 1:
                print(f"  📋 Run {run + 1}/{self.num_runs}...")
            
            result = evaluate(
                data=str(fresh_jsonl),
                evaluators={"metrics": evaluator},
                evaluator_config={"metrics": evaluator.evaluator_config},
                target=evaluator.target_function,
                fail_on_evaluator_errors=False,
                **self._context._credential_kwargs,
            )
            results.append(result)
        
        # Cleanup
        try:
            fresh_jsonl.unlink()
            tmp_dir.rmdir()
        except OSError:
            pass
        
        return results

    def show_results(self, results: List[EvaluationResult]):
        """Display detailed results from all evaluations."""
        print("=" * 60)
        print("📈 EVALUATION RESULTS")
        print("=" * 60)
        print()

        successful = [r for r in results if r.success and r.raw_results]
        failed = [r for r in results if not r.success]

        if successful:
            for result in successful:
                print(f"✅ {result.workflow_name}")
                raw_results = result.raw_results[0]
                for filename, eval_result in raw_results.items():
                    print(f"  == {filename} ==")
                    for res in eval_result["rows"]:
                        success = res["outputs.metrics.correct_action"]
                        testcase_name = res["inputs.testcase"]
                        score = res["outputs.metrics.score"]
                        print(f"    -  {'✅' if success else '❌'} {score} - {testcase_name}")
                print()

        if failed:
            print("❌ FAILED EVALUATIONS:")
            for result in failed:
                print(f"  • {result.workflow_name}: {result.error}")
            print()

        if not successful and not failed:
            print("No evaluation results to display.")
            print()

    def show_summary(self, results: List[EvaluationResult]):
        """Display aggregated results from all evaluations."""
        successful = [r for r in results if r.success]
        failed = [r for r in results if not r.success]

        print("=" * 60)
        print("📈 EVALUATION SUMMARY")
        print("=" * 60)
        print(f"Total targets: {len(results)}")
        print(f"✅ Successful: {len(successful)}")
        print(f"❌ Failed: {len(failed)}")
        print()

        if failed:
            print("❌ FAILED EVALUATIONS:")
            for result in failed:
                print(f"  • {result.workflow_name}: {result.error}")
            print()

        if successful:
            print("✅ SUCCESSFUL EVALUATIONS:")
            total_test_files = sum(r.num_test_files for r in successful)
            print(f"  • Processed {total_test_files} test files across {len(successful)} workflows")

            # Group by workflow type
            by_type = {}
            for result in successful:
                workflow_type = result.target.config.kind
                by_type.setdefault(workflow_type, []).append(result)

            for workflow_type, type_results in by_type.items():
                print(f"  • {workflow_type}: {len(type_results)} workflows")

    def cleanup(self):
        """Clean up resources."""
        if self._context:
            self._context.cleanup()


__all__ = [
    "ExecutionContext",
    "EvaluationResult",
    "EvaluationExecutor",
]
