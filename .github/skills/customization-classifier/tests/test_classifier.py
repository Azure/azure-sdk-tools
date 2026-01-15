"""
Customization Classifier Skill Test Runner

This module runs the SKILL.md through Claude API with test cases to validate
classification behavior. No mocking - uses real API calls.

Requirements:
    pip install anthropic pytest azure-identity python-dotenv

Environment (loaded from .env at repo root):
    ANTHROPIC_ENDPOINT: Azure AI Services endpoint (uses identity auth)
    ANTHROPIC_MODEL: Model to use (e.g., claude-sonnet-4-5)
    ANTHROPIC_API_KEY: Optional - if not set, uses Azure CLI auth

Usage:
    pytest test_classifier.py -v
    python test_classifier.py  # Run directly for detailed output
"""

import json
import os
import re
from datetime import datetime
from pathlib import Path
from dataclasses import dataclass, asdict
from typing import Optional

import anthropic
import pytest
from dotenv import load_dotenv


# Output directory for test results
OUTPUT_DIR = Path(__file__).parent / "test_output"


def load_env():
    """Load environment variables from .env file at repo root."""
    # Find repo root (look for .env file going up)
    current = Path(__file__).resolve()
    for parent in current.parents:
        env_file = parent / ".env"
        if env_file.exists():
            load_dotenv(env_file)
            print(f"Loaded environment from: {env_file}")
            return env_file
    
    # Fallback: try repo root based on known structure
    repo_root = Path(__file__).resolve().parents[4]  # .github/skills/customization-classifier/tests -> repo root
    env_file = repo_root / ".env"
    if env_file.exists():
        load_dotenv(env_file)
        print(f"Loaded environment from: {env_file}")
        return env_file
    
    print("Warning: No .env file found")
    return None


def create_anthropic_client() -> anthropic.Anthropic:
    """
    Create Anthropic client with Azure AI endpoint and identity auth.
    
    Uses Azure CLI authentication (Entra ID) by default.
    Falls back to API key if ANTHROPIC_API_KEY is set.
    """
    endpoint = os.environ.get("ANTHROPIC_ENDPOINT")
    api_key = os.environ.get("ANTHROPIC_API_KEY")
    
    if endpoint:
        # Azure AI Services endpoint with identity auth
        if api_key:
            # Use API key if provided
            return anthropic.Anthropic(
                base_url=endpoint,
                api_key=api_key,
            )
        else:
            # Use Azure CLI / Entra ID authentication
            try:
                from azure.identity import DefaultAzureCredential, get_bearer_token_provider
                
                credential = DefaultAzureCredential()
                token_provider = get_bearer_token_provider(
                    credential,
                    "https://cognitiveservices.azure.com/.default"
                )
                
                return anthropic.Anthropic(
                    base_url=endpoint,
                    api_key="placeholder",  # Required but not used with auth header
                    default_headers={
                        "Authorization": f"Bearer {token_provider()}"
                    },
                )
            except ImportError:
                raise RuntimeError(
                    "azure-identity package required for identity auth. "
                    "Install with: pip install azure-identity"
                )
    elif api_key:
        # Direct Anthropic API
        return anthropic.Anthropic(api_key=api_key)
    else:
        raise RuntimeError(
            "No Anthropic configuration found. Set either:\n"
            "  - ANTHROPIC_ENDPOINT (for Azure AI) in .env\n"
            "  - ANTHROPIC_API_KEY (for direct Anthropic API) in .env"
        )


# Import reasoning evaluator
try:
    from reasoning_evaluator import evaluate_reasoning
    REASONING_EVAL_AVAILABLE = True
except ImportError:
    REASONING_EVAL_AVAILABLE = False
    print("Warning: reasoning_evaluator not available, skipping grounding checks")


@dataclass
class TestCase:
    id: str
    description: str
    service: str
    language: str
    request: str
    expected_classification: str


@dataclass
class ClassificationResult:
    classification: Optional[str]
    reason: Optional[str]
    iteration: Optional[int]
    next_action: Optional[str]
    raw_response: str


class SkillTestRunner:
    """Runs the customization classifier skill through Claude API."""
    
    def __init__(self, model: Optional[str] = None):
        load_env()
        self.client = create_anthropic_client()
        self.model = model or os.environ.get("ANTHROPIC_MODEL", "claude-sonnet-4-5")
        self.skill_content = self._load_skill()
        self.test_cases = self._load_test_cases()
        self.output_dir = OUTPUT_DIR
        self._clear_output_dir()
        print(f"Using model: {self.model}")
        print(f"Output directory: {self.output_dir}")
    
    def _clear_output_dir(self):
        """Clear and recreate the output directory."""
        import shutil
        if self.output_dir.exists():
            shutil.rmtree(self.output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
    
    def _load_skill(self) -> str:
        """Load SKILL.md content."""
        skill_path = Path(__file__).parent.parent / "SKILL.md"
        if not skill_path.exists():
            raise FileNotFoundError(f"SKILL.md not found at {skill_path}")
        return skill_path.read_text()
    
    def _load_test_cases(self) -> list[TestCase]:
        """Load test cases from JSON."""
        cases_path = Path(__file__).parent / "test_cases.json"
        if not cases_path.exists():
            raise FileNotFoundError(f"test_cases.json not found at {cases_path}")
        
        with open(cases_path) as f:
            data = json.load(f)
        
        return [
            TestCase(
                id=tc["id"],
                description=tc["description"],
                service=tc["input"]["service"],
                language=tc["input"]["language"],
                request=tc["input"]["request"],
                expected_classification=tc["expected"]["classification"],
            )
            for tc in data["test_cases"]
        ]
    
    def _build_prompt(self, test_case: TestCase) -> str:
        """Build the user prompt for a test case."""
        return f"""Classify this customization request:

Service: {test_case.service}
Language: {test_case.language}
Request: {test_case.request}

Assume the following repos are available in the workspace:
- /repos/azure-rest-api-specs (spec repo)
- /repos/azure-sdk-for-{test_case.language} (SDK repo)

Provide your classification in the exact format specified in your instructions."""
    
    def _parse_response(self, response_text: str) -> ClassificationResult:
        """Parse the classification response."""
        classification = None
        reason = None
        iteration = None
        next_action = None
        
        # Extract Classification
        class_match = re.search(
            r"Classification:\s*(PHASE_A|SUCCESS|FAILURE)", 
            response_text, 
            re.IGNORECASE
        )
        if class_match:
            classification = class_match.group(1).upper()
        
        # Extract Reason
        reason_match = re.search(
            r"Reason:\s*(.+?)(?=\n(?:Iteration|Next Action)|$)", 
            response_text, 
            re.IGNORECASE | re.DOTALL
        )
        if reason_match:
            reason = reason_match.group(1).strip()
        
        # Extract Iteration
        iter_match = re.search(r"Iteration:\s*(\d+)", response_text, re.IGNORECASE)
        if iter_match:
            iteration = int(iter_match.group(1))
        
        # Extract Next Action
        action_match = re.search(
            r"Next Action:\s*(.+?)(?=\n\n|$)", 
            response_text, 
            re.IGNORECASE | re.DOTALL
        )
        if action_match:
            next_action = action_match.group(1).strip()
        
        return ClassificationResult(
            classification=classification,
            reason=reason,
            iteration=iteration,
            next_action=next_action,
            raw_response=response_text,
        )
    
    def run_test(self, test_case: TestCase) -> tuple[bool, ClassificationResult, list[str]]:
        """
        Run a single test case through the skill.
        
        Returns:
            (passed, result, failures) tuple
        """
        prompt = self._build_prompt(test_case)
        
        # Call Claude API with skill as system prompt
        message = self.client.messages.create(
            model=self.model,
            max_tokens=1024,
            system=f"""You are using the following skill to classify customization requests.
Follow the instructions exactly and respond in the specified format.

{self.skill_content}""",
            messages=[{"role": "user", "content": prompt}],
        )
        
        response_text = message.content[0].text
        result = self._parse_response(response_text)
        
        # Validate results
        failures = []
        reasoning_eval = None
        
        # Check classification (primary validation)
        if result.classification != test_case.expected_classification:
            failures.append(
                f"Classification mismatch: expected {test_case.expected_classification}, "
                f"got {result.classification}"
            )
        
        # Check reasoning quality (secondary validation)
        if result.reason:
            if REASONING_EVAL_AVAILABLE:
                try:
                    reasoning_eval = evaluate_reasoning(
                        classification=result.classification,
                        reasoning=result.reason,
                        query=test_case.request,
                        next_action=result.next_action,
                    )
                    # For PHASE_A, reasoning must be grounded in reference doc
                    if result.classification == "PHASE_A" and not reasoning_eval.get("valid"):
                        failures.append(
                            f"PHASE_A reasoning not grounded: {reasoning_eval.get('details', 'No details')}"
                        )
                except Exception as e:
                    # Don't fail test on evaluation errors, just log
                    reasoning_eval = {"error": str(e), "valid": True}
        else:
            failures.append("No reason provided in response")
        
        passed = len(failures) == 0
        return passed, result, failures, reasoning_eval
    
    def run_all_tests(self) -> dict:
        """Run all test cases and return results summary."""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        results = {
            "timestamp": timestamp,
            "model": self.model,
            "passed": 0,
            "failed": 0,
            "total": len(self.test_cases),
            "details": [],
        }
        
        for test_case in self.test_cases:
            print(f"\nRunning: {test_case.id}")
            print(f"  {test_case.description}")
            
            try:
                passed, result, failures, reasoning_eval = self.run_test(test_case)
                
                if passed:
                    results["passed"] += 1
                    print(f"  ✅ PASSED - Classification: {result.classification}")
                    if reasoning_eval and result.classification == "PHASE_A":
                        decorators = reasoning_eval.get("decorator_check", {}).get("referenced_decorators", [])
                        if decorators:
                            print(f"     Grounded in: {', '.join(decorators)}")
                else:
                    results["failed"] += 1
                    print(f"  ❌ FAILED")
                    for failure in failures:
                        print(f"     - {failure}")
                
                test_detail = {
                    "id": test_case.id,
                    "description": test_case.description,
                    "passed": passed,
                    "expected_classification": test_case.expected_classification,
                    "actual_classification": result.classification,
                    "actual_reason": result.reason,
                    "reasoning_evaluation": reasoning_eval,
                    "iteration": result.iteration,
                    "next_action": result.next_action,
                    "failures": failures,
                    "input": {
                        "service": test_case.service,
                        "language": test_case.language,
                        "request": test_case.request,
                    },
                    "raw_response": result.raw_response,
                }
                results["details"].append(test_detail)
                
            except Exception as e:
                results["failed"] += 1
                print(f"  ❌ ERROR: {e}")
                error_detail = {
                    "id": test_case.id,
                    "passed": False,
                    "error": str(e),
                }
                results["details"].append(error_detail)
        
        # Write all output to a single file
        self._write_output(results, timestamp)
        
        return results
    
    def _write_output(self, results: dict, timestamp: str):
        """Write all test output to a single file with summary at top."""
        output_file = self.output_dir / f"{timestamp}_results.txt"
        
        with open(output_file, "w") as f:
            # Summary at top
            f.write("=" * 70 + "\n")
            f.write("CUSTOMIZATION CLASSIFIER SKILL TEST RESULTS\n")
            f.write("=" * 70 + "\n\n")
            f.write(f"Timestamp: {timestamp}\n")
            f.write(f"Model:     {results['model']}\n")
            f.write(f"Total:     {results['total']}\n")
            f.write(f"Passed:    {results['passed']} ✅\n")
            f.write(f"Failed:    {results['failed']} ❌\n")
            f.write(f"Rate:      {results['passed'] / results['total'] * 100:.1f}%\n\n")
            
            # Quick overview table
            f.write("-" * 70 + "\n")
            f.write("QUICK OVERVIEW\n")
            f.write("-" * 70 + "\n")
            for detail in results["details"]:
                status = "✅" if detail.get("passed") else "❌"
                expected = detail.get("expected_classification", "?")
                actual = detail.get("actual_classification", "?")
                match = "✓" if expected == actual else "✗"
                f.write(f"{status} {detail['id']:<45} {expected:<10} → {actual:<10} {match}\n")
            f.write("\n")
            
            # Detailed results
            f.write("=" * 70 + "\n")
            f.write("DETAILED RESULTS\n")
            f.write("=" * 70 + "\n")
            
            for detail in results["details"]:
                status = "✅ PASSED" if detail.get("passed") else "❌ FAILED"
                f.write(f"\n{'─' * 70}\n")
                f.write(f"{status}: {detail['id']}\n")
                f.write(f"{'─' * 70}\n")
                f.write(f"Description: {detail.get('description', 'N/A')}\n\n")
                
                if "error" in detail:
                    f.write(f"ERROR: {detail['error']}\n")
                else:
                    f.write(f"Input:\n")
                    f.write(f"  Service:  {detail['input']['service']}\n")
                    f.write(f"  Language: {detail['input']['language']}\n")
                    f.write(f"  Request:\n")
                    for line in detail['input']['request'].split('\n'):
                        f.write(f"    {line}\n")
                    f.write(f"\n")
                    
                    f.write(f"Expected Classification: {detail.get('expected_classification')}\n")
                    f.write(f"Actual Classification:   {detail.get('actual_classification')}\n")
                    f.write(f"Actual Reason:           {detail.get('actual_reason')}\n")
                    
                    # Reasoning evaluation details
                    reasoning_eval = detail.get('reasoning_evaluation')
                    if reasoning_eval:
                        f.write(f"\nReasoning Evaluation:\n")
                        f.write(f"  Type:    {reasoning_eval.get('evaluation_type', 'N/A')}\n")
                        f.write(f"  Valid:   {reasoning_eval.get('valid', 'N/A')}\n")
                        if reasoning_eval.get('groundedness_score'):
                            f.write(f"  Score:   {reasoning_eval.get('groundedness_score')}/5\n")
                        decorator_check = reasoning_eval.get('decorator_check', {})
                        if decorator_check.get('referenced_decorators'):
                            f.write(f"  Decorators: {', '.join(decorator_check['referenced_decorators'])}\n")
                        f.write(f"  Details: {reasoning_eval.get('details', 'N/A')}\n")
                    
                    if detail.get("failures"):
                        f.write(f"\nFailures:\n")
                        for failure in detail["failures"]:
                            f.write(f"  ✗ {failure}\n")
                    
                    f.write(f"\nRaw Response:\n")
                    f.write(f"  ┌{'─' * 66}┐\n")
                    for line in detail.get("raw_response", "").split("\n"):
                        f.write(f"  │ {line:<64} │\n")
                    f.write(f"  └{'─' * 66}┘\n")
        
        print(f"\nResults written to: {output_file}")


# Pytest integration
@pytest.fixture(scope="module")
def runner():
    """Create test runner instance."""
    return SkillTestRunner()


@pytest.fixture(scope="module")
def test_cases(runner):
    """Load test cases."""
    return runner.test_cases


def pytest_generate_tests(metafunc):
    """Generate individual test functions for each test case."""
    if "test_case_id" in metafunc.fixturenames:
        # Load test cases
        cases_path = Path(__file__).parent / "test_cases.json"
        with open(cases_path) as f:
            data = json.load(f)
        ids = [tc["id"] for tc in data["test_cases"]]
        metafunc.parametrize("test_case_id", ids)


def test_classification(runner, test_case_id):
    """Test individual classification case."""
    test_case = next(tc for tc in runner.test_cases if tc.id == test_case_id)
    passed, result, failures, reasoning_eval = runner.run_test(test_case)
    
    if not passed:
        failure_msg = "\n".join(failures)
        reasoning_info = ""
        if reasoning_eval:
            reasoning_info = f"\n\nReasoning evaluation: {reasoning_eval}"
        pytest.fail(
            f"Classification failed for {test_case_id}:\n{failure_msg}{reasoning_info}\n\n"
            f"Raw response:\n{result.raw_response}"
        )


# Direct execution
if __name__ == "__main__":
    print("=" * 60)
    print("Customization Classifier Skill Test Suite")
    print("=" * 60)
    
    try:
        runner = SkillTestRunner()
    except RuntimeError as e:
        print(f"\n❌ Configuration error: {e}")
        exit(1)
    
    results = runner.run_all_tests()
    
    print("\n" + "=" * 60)
    print("SUMMARY")
    print("=" * 60)
    print(f"Total:  {results['total']}")
    print(f"Passed: {results['passed']} ✅")
    print(f"Failed: {results['failed']} ❌")
    print(f"Rate:   {results['passed'] / results['total'] * 100:.1f}%")
    
    # Exit with error code if any tests failed
    exit(0 if results["failed"] == 0 else 1)
