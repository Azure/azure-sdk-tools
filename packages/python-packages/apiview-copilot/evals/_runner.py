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

    def create_temporary_jsonl_file(self, testcases: list[dict], workflow_name: str) -> Path:
        """
        Create a temporary JSONL file from a list of test cases.

        Args:
            testcases: List of test case dictionaries to write to the file.
            workflow_name: Name used for the output filename (e.g., "my_workflow" -> "my_workflow.jsonl").

        Returns:
            Path to the created JSONL file. The file is tracked and will be cleaned up
            automatically when cleanup() is called on this ExecutionContext.

        Note:
            Each test case is serialized as a compact JSON object on a single line.
            The temporary file is thread-safe and managed by the context's cleanup system.
        """
        tmp_dir = Path(tempfile.mkdtemp(prefix="evals_executor_"))
        output_name = f"{workflow_name}.jsonl"
        out_path = tmp_dir / output_name

        lines = [
            json.dumps(test_case, separators=(",", ":"), ensure_ascii=False)
            for test_case in testcases
        ]
        content = "\n".join(lines) + "\n"

        out_path.write_text(content, encoding="utf-8")

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

    def __init__(self, *, num_runs: int = DEFAULT_NUM_RUNS, use_recording: bool = False):
        self.num_runs = num_runs
        self._context: ExecutionContext | None = None
        self._results_lock = threading.Lock()
        self._use_recording = use_recording

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

            print(f"🚀 Executing {len(discovery_result.targets)} evaluation targets...")
            print(f"📊 {discovery_result.summary}")
            print()

            return self._run_parallel(discovery_result)
        finally:
            self.cleanup()

    def _run_parallel(self, discovery_result: DiscoveryResult) -> list[EvaluationResult]:
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

            def load_single_file(test_file: Path) -> tuple[Path, dict]:
                return test_file, self._context._load_test_file(test_file)

            with ThreadPoolExecutor(max_workers=4) as executor:
                futures = [executor.submit(load_single_file, test_file) for test_file in target.test_files]
                for future in as_completed(futures):
                    test_file, test_case = future.result()
                    test_file_to_case[test_file] = test_case

            testcase_ids = []
            test_file_paths = []

            for test_file, test_case in test_file_to_case.items():
                testcase_id = test_case.get("testcase")
                if testcase_id:
                    testcase_ids.append(testcase_id)
                    test_file_paths.append(test_file)

            # Resolve recordings strategy
            recordings_lookup = load_recordings(testcase_ids, test_file_paths) if self._use_recording else {}

            # Partition test data based on recordings
            recorded_azure_rows = []
            fresh_testcases = []
            fresh_test_file_paths = []

            for test_file, test_case in test_file_to_case.items():
                testcase_id = test_case.get("testcase")

                if testcase_id and testcase_id in recordings_lookup:
                    recorded_azure_rows.append(recordings_lookup[testcase_id])
                else:
                    fresh_testcases.append(test_case)
                    fresh_test_file_paths.append(test_file)

            # Execute fresh testcases
            fresh_results = self._run_azure_evaluation(fresh_testcases, target)

            if self._use_recording:
                fresh_testcase_ids = [
                    test_file_to_case[tf].get("testcase") 
                    for tf in fresh_test_file_paths
                ]
                save_recordings(fresh_testcase_ids, fresh_test_file_paths, fresh_results)

            # Combine all results
            recorded_rows = [row for row in recorded_azure_rows]
            fresh_rows = [row for result in fresh_results for row in result.get("rows", [])]
            all_recorded_rows = recorded_rows + fresh_rows
            combined_result = {"rows": all_recorded_rows, "metrics": {}, "studio_url": None}

            all_passed = all(row.get("outputs.metrics.success", False) for row in all_recorded_rows)

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

        # Create temporary JSONL file to feed to evaluator
        fresh_jsonl = self._context.create_temporary_jsonl_file(testcases, f"fresh_{target.workflow_name}")

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

        return results

    def show_results(self, results: list[EvaluationResult]):
        """Display detailed results from all evaluations."""
        print("=" * 60)
        print("📈 EVALUATION RESULTS")
        print("=" * 60)
        print()

        successful = [r for r in results if r.success and r.raw_results]
        failed = [r for r in results if not r.success and r.raw_results and r.error is None]
        errored = [r for r in results if r.error is not None]

        if successful:
            for result in successful:
                print(f"  ✅ {result.workflow_name}")
                raw_results = result.raw_results[0]
                for filename, eval_result in raw_results.items():
                    print(f"    == {filename} ==")
                    for res in eval_result["rows"]:
                        success = res["outputs.metrics.success"]
                        testcase_name = res["inputs.testcase"]
                        score = res["outputs.metrics.score"]
                        print(f"      -  {'✅' if success else '❌'} {score} - {testcase_name}")
                    print()

        if failed:
            for result in failed:
                print(f"  ❌ {result.workflow_name}")
                raw_results = result.raw_results[0]
                for filename, eval_result in raw_results.items():
                    print(f"    == {filename} ==")
                    for res in eval_result["rows"]:
                        success = res["outputs.metrics.success"]
                        testcase_name = res["inputs.testcase"]
                        score = res["outputs.metrics.score"]
                        print(f"      -  {'✅' if success else '❌'} {score} - {testcase_name}")
            print()

        if errored:
            print("💥 ERRORED EVALUATIONS:")
            for result in errored:
                print(f"  💥 {result.workflow_name}: {result.error}")
            print()

        if not successful and not failed and not errored:
            print("No evaluation results to display.")
            print()

    def show_summary(self, results: list[EvaluationResult]):
        """Display aggregated results from all evaluations."""
        successful = [r for r in results if r.success and r.raw_results]
        failed = [r for r in results if not r.success and r.raw_results and r.error is None]
        errored = [r for r in results if r.error is not None]

        print("=" * 60)
        print("📈 EVALUATION SUMMARY")
        print("=" * 60)
        print(f"Total targets: {len(results)}")
        print(f"✅ Successful: {len(successful)}")
        if len(failed) > 0:
            print(f"❌ Failed: {len(failed)}")
        if len(errored) > 0:
            print(f"💥 Errored: {len(errored)}")
        print()

        if errored:
            print("💥 ERRORED EVALUATIONS:")
            for result in errored:
                print(f"  • {result.workflow_name}: {result.error}")
            print()

        if failed:
            print("❌ FAILED EVALUATIONS:")
            for result in failed:
                print(f"  • {result.workflow_name}")
            print()

        if successful:
            print("✅ SUCCESSFUL EVALUATIONS:")
            for result in successful:
                print(f"  • {result.workflow_name}")
            print()

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
