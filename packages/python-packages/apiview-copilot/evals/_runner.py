import json
import os
import sys
import tempfile
import threading
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import yaml
from azure.ai.evaluation import evaluate
from azure.identity import AzurePipelinesCredential

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from evals._config_loader import get_evaluator_class
from evals._discovery import DiscoveryResult, EvaluationTarget
from evals._util import (
    save_recordings,
    load_recordings,
)
from src._settings import SettingsManager

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

    def __init__(self, *, num_runs: int = DEFAULT_NUM_RUNS, use_recording: bool = False, verbose: bool = False):
        self.num_runs = num_runs
        self._context: ExecutionContext | None = None
        self._results_lock = threading.Lock()
        self._use_recording = use_recording
        self._verbose = verbose

    def _ensure_context(self):
        if self._context is None:
            self._context = ExecutionContext()

    def run(self, discovery_result: DiscoveryResult) -> list[EvaluationResult]:
        """Execute all targets in the discovery result.

        Args:
            discovery_result: Result from EvaluationDiscovery containing targets to execute.

        Returns:
            List of EvaluationResult objects.
        """
        try:
            self._ensure_context()
            return self._run(discovery_result)
        finally:
            self.cleanup()

    def _run(self, discovery_result: DiscoveryResult) -> list[EvaluationResult]:
        """Run tests in parallel with progress tracking."""
        workflow_count = len(discovery_result.targets)
        cpu_count = os.cpu_count() or 4
        max_workers = min(cpu_count * 2, workflow_count)
        results = []
        total_targets = len(discovery_result.targets)

        # Session header
        BOLD = "\033[1m"
        END = "\033[0m"
        print(f"{'='*60}")
        print("test session starts")
        print(f"{'='*60}")
        print(f"{BOLD}collected {discovery_result.total_test_files}{END} across {BOLD}{workflow_count} workflows {END}")
        print()

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

                except Exception as e:
                    with self._results_lock:
                        completed_count += 1

                    print('\033[91mE\033[0m', end='', flush=True)
                    results.append(EvaluationResult(target=target, raw_results=[], success=False, error=str(e)))

        # Spacing
        print()

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
            if self._use_recording:
                cache_lookup = load_recordings(testcase_ids, test_file_paths)
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

                if self._use_recording:
                    save_recordings(fresh_test_file_paths, fresh_results)

            # Combine all results
            cached_rows = [row for row in cached_azure_rows]
            fresh_rows = [row for result in fresh_results for row in result.get("rows", [])]
            all_cached_rows = cached_rows + fresh_rows
            combined_result = {"rows": all_cached_rows, "metrics": {}, "studio_url": None}

            all_passed = all(row.get("outputs.metrics.success", False) for row in all_cached_rows)

            return EvaluationResult(
                target=target,
                raw_results=[{f"{target.workflow_name}.jsonl": combined_result}],
                success=all_passed,
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

    def show_results(self, results: list[EvaluationResult]):
        """Display detailed evaluation results with colored output showing failures, errors, and summary statistics."""
        
        # color codes
        RED = '\033[91m'
        GREEN = '\033[92m'
        YELLOW = '\033[93m'
        RESET = '\033[0m'
        BOLD = '\033[1m'
        

        workflow_stats = []
        for result in results:
            if result.error:
                workflow_stats.append({
                    'result': result,
                    'status': 'errored',
                    'failed_testcases': [],
                    'passed_testcases': [],
                    'partial_testcases': [],
                    'total_testcases': 0
                })
            elif result.raw_results:
                failed_tests = []
                passed_tests = []
                partial_tests = []
                raw = result.raw_results[0]
                for filename, eval_result in raw.items():
                    for res in eval_result["rows"]:
                        testcase = res["inputs.testcase"]
                        score = res["outputs.metrics.score"]
                        success = res["outputs.metrics.success"]
                        
                        if success:
                            if score < 100:
                                partial_tests.append((testcase, score))
                            else:
                                passed_tests.append((testcase, score))
                        else:
                            failed_tests.append((testcase, score))
                
                workflow_stats.append({
                    'result': result,
                    'status': 'failed' if failed_tests else 'passed',
                    'failed_testcases': failed_tests,
                    'passed_testcases': passed_tests,
                    'partial_testcases': partial_tests,
                    'total_testcases': len(failed_tests) + len(passed_tests) + len(partial_tests)
                })
        

        errored = [s for s in workflow_stats if s['status'] == 'errored']
        failed = [s for s in workflow_stats if s['status'] == 'failed']

        has_partial = any(len(s['partial_testcases']) > 0 for s in workflow_stats)
        
        # failures section
        if failed or errored:
            print(f"{RED}{BOLD}=" * 60)
            print("FAILURES")
            print("=" * 60 + RESET)
            
            for stat in errored:
                result = stat['result']
                print(f"{RED}_________________________ {result.workflow_name} _________________________{RESET}")
                print(f"{RED}ERROR: {result.error}{RESET}")
                print()
            
            for stat in failed:
                result = stat['result']
                print(f"{RED}_________________________ {result.workflow_name} _________________________{RESET}")
                for testcase, score in stat['failed_testcases']:
                    print(f"{RED}FAILED{RESET} {result.workflow_name}::{testcase}")
                    print(f"  score: {score}")
                    print()

        if has_partial:
            print(f"{YELLOW}{BOLD}{'=' * 60}")
            print("PARTIAL PASSES")
            print("=" * 60 + RESET)
            
            for stat in workflow_stats:
                if stat['partial_testcases']:
                    result = stat['result']
                    print(f"{YELLOW}_________________________ {result.workflow_name} _________________________{RESET}")
                    for testcase, score in stat['partial_testcases']:
                        print(f"{YELLOW}PARTIAL{RESET} {result.workflow_name}::{testcase}")
                        print(f"  score: {score}")
                        print()

        if self._verbose and any(len(s['passed_testcases']) > 0 for s in workflow_stats):
            print(f"{GREEN}{BOLD}{'=' * 60}")
            print("PASSED")
            print("=" * 60 + RESET)
            
            for stat in workflow_stats:
                if stat['passed_testcases']:
                    result = stat['result']
                    print(f"{GREEN}_________________________ {result.workflow_name} _________________________{RESET}")
                    for testcase, score in stat['passed_testcases']:
                        print(f"{GREEN}✓{RESET} {result.workflow_name}::{testcase}")
                        print(f"  score: {score}")
                        print()
        
        # short test summary info
        if failed or errored or has_partial:
            print(f"{YELLOW}{'=' * 60}")
            print("test summary")
            print("=" * 60 + RESET)
            
            for stat in errored:
                result = stat['result']
                print(f"{RED}ERROR{RESET} {result.workflow_name} - {result.error}")
            
            for stat in failed:
                result = stat['result']
                for testcase, _ in stat['failed_testcases']:
                    print(f"{RED}FAILED{RESET} {result.workflow_name}::{testcase}")

            for stat in workflow_stats:
                if stat['partial_testcases']:
                    result = stat['result']
                    for testcase, score in stat['partial_testcases']:
                        print(f"{YELLOW}PARTIAL{RESET} {result.workflow_name}::{testcase} - score: {score}")

            print()
        
        # final summary with precomputed totals
        print("=" * 60)
        parts = []
        
        total_failed_testcases = sum(len(s['failed_testcases']) for s in workflow_stats)
        total_passed_testcases = sum(len(s['passed_testcases']) for s in workflow_stats)
        total_partial_testcases = sum(len(s['partial_testcases']) for s in workflow_stats)
        total_testcases = sum(s['total_testcases'] for s in workflow_stats)
        
        if errored:
            parts.append(f"{RED}{len(errored)} errored{RESET}")
        if total_failed_testcases > 0:
            parts.append(f"{RED}{total_failed_testcases} failed{RESET}")
        if total_passed_testcases > 0:
            parts.append(f"{GREEN}{total_passed_testcases} passed{RESET}")
        if total_partial_testcases > 0:
            parts.append(f"{YELLOW}{total_partial_testcases} partial{RESET}")
        parts.append(f"{total_testcases} total")
        
        print(", ".join(parts))
        print("=" * 60)

    def cleanup(self):
        """Clean up resources."""
        if self._context:
            self._context.cleanup()

    def generate_report(self, results: list[EvaluationResult]) -> list:
        """Generate a flat list of eval_case dicts per test, matching the required schema."""
        eval_timestamp = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
        eval_date = eval_timestamp[:10]
        cases = []
        for result in results:
            workflow_name = result.workflow_name
            raw_results = list(result.raw_results[0].values())[0]["rows"] if result.raw_results else []
            for row in raw_results:
                testcase = row.get("inputs.testcase")
                status = "pass" if row.get("outputs.metrics.success") == True else "fail"
                score = row.get("outputs.metrics.score")
                case_id = f"{eval_timestamp}|{workflow_name}|{testcase}"
                pk = eval_date.replace("-", "_")
                cases.append(
                    {
                        "type": "eval_case",
                        "id": case_id,
                        "pk": pk,
                        "eval_timestamp": eval_timestamp,
                        "eval_date": eval_date,
                        "workflow_name": workflow_name,
                        "testcase": testcase,
                        "status": status,
                        "score": score,
                    }
                )
        return cases


__all__ = [
    "ExecutionContext",
    "EvaluationResult",
]
