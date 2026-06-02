"""Spector case processor — converts TypeSpec scenario files to markdown.

Processes .tsp files with @scenario annotations from the typespec and
typespec-azure http-specs directories, using Azure OpenAI to generate
meaningful titles and descriptions for each scenario.
"""

from __future__ import annotations

import asyncio
import logging
import os
import re
import time
from dataclasses import dataclass
from pathlib import Path

from openai import AsyncAzureOpenAI

logger = logging.getLogger(__name__)

MAX_RETRIES = 5
BASE_DELAY = 2.0  # seconds
MAX_DELAY = 60.0
MAX_CONCURRENT = 5
IGNORED_SPECS = ["special-words"]


@dataclass
class AnalysisResult:
    title: str
    scenarios: list[dict[str, str]]  # [{heading, description}]


class SpectorCaseProcessor:
    """Processes TypeSpec @scenario files into markdown documentation."""

    _client: AsyncAzureOpenAI | None = None

    @classmethod
    async def _init_client(cls) -> None:
        deployment = os.environ.get("AOAI_CHAT_REASONING_MODEL")
        api_key = os.environ.get("AOAI_CHAT_COMPLETIONS_API_KEY")
        endpoint = os.environ.get("AOAI_CHAT_COMPLETIONS_ENDPOINT")
        cls._client = AsyncAzureOpenAI(
            azure_endpoint=endpoint,
            api_key=api_key,
            api_version="2024-12-01-preview",
            azure_deployment=deployment,
        )

    @classmethod
    async def process_spector_cases(cls, docs_dir: str) -> None:
        """Process spector cases in both typespec and typespec-azure directories."""
        await cls._init_client()

        dirs = [
            (
                os.path.join(docs_dir, "typespec/packages/http-specs/specs"),
                os.path.join(docs_dir, "typespec/packages/http-specs/specs/generated"),
            ),
            (
                os.path.join(docs_dir, "typespec-azure/packages/azure-http-specs/specs"),
                os.path.join(docs_dir, "typespec-azure/packages/azure-http-specs/specs/generated"),
            ),
        ]

        for root, target_root in dirs:
            try:
                await cls._convert_cases_to_markdown(root, target_root)
            except Exception as e:
                logger.error("Error processing specs in %s: %s", root, e)

        logger.info("Spector case processing completed")

    @classmethod
    async def _convert_cases_to_markdown(cls, root: str, target_root: str) -> None:
        """Convert all specs in a directory to markdown."""
        if not os.path.isdir(root):
            logger.error("Spector specs directory not found: %s", root)
            return

        specs, paths = cls._get_specs(root)
        semaphore = asyncio.Semaphore(MAX_CONCURRENT)

        async def process(spec: str, spec_path: str) -> None:
            async with semaphore:
                await cls._process_spec_file(spec, spec_path, root, target_root)

        tasks = []
        for spec, spec_path in zip(specs, paths):
            if os.path.basename(spec_path).lower() != "main.tsp":
                continue
            tasks.append(process(spec, spec_path))

        await asyncio.gather(*tasks, return_exceptions=True)

    @classmethod
    async def _process_spec_file(
        cls, main_spec: str, spec_path: str, root: str, target_root: str
    ) -> None:
        """Process a single spec file."""
        try:
            dir_path = os.path.dirname(spec_path)
            relative_dir = os.path.relpath(dir_path, root)
            logger.info("Processing spec: %s", relative_dir)

            # Check for client.tsp
            client_tsp_path = os.path.join(dir_path, "client.tsp")
            client_tsp = None
            if os.path.isfile(client_tsp_path):
                with open(client_tsp_path, encoding="utf-8") as f:
                    client_tsp = f.read()

            scenarios = cls._get_scenarios("@scenario\n", main_spec)
            if not scenarios:
                logger.info("No scenarios in %s, skipping", relative_dir)
                return

            doc = await cls._create_markdown_doc(scenarios, main_spec, client_tsp)

            target_dir = os.path.join(target_root, relative_dir)
            os.makedirs(target_dir, exist_ok=True)
            target_path = os.path.join(
                target_dir,
                os.path.basename(spec_path).replace(".tsp", ".md"),
            )
            Path(target_path).write_text(doc, encoding="utf-8")
            logger.info("Saved markdown: %s", target_path)
        except Exception as e:
            logger.error("Error processing %s: %s", spec_path, e)

    @classmethod
    async def _create_markdown_doc(
        cls,
        scenarios: list[str],
        main_spec: str,
        client_tsp: str | None = None,
    ) -> str:
        """Create markdown from scenarios using LLM analysis."""
        combined = main_spec
        if client_tsp:
            combined = (
                f"// === MAIN SPEC (main.tsp) ===\n{main_spec}\n\n"
                f"// === CLIENT CUSTOMIZATION (client.tsp) ===\n{client_tsp}"
            )

        analysis = await cls._analyze_scenarios(scenarios, combined)

        doc = f"# Usages for {analysis.title}\n\n"
        for i, scenario in enumerate(scenarios):
            data = analysis.scenarios[i] if i < len(analysis.scenarios) else {"heading": f"Scenario {i+1}", "description": ""}
            cleaned = cls._remove_spector_content(scenario)
            desc = "" if data["description"] == data["heading"] else data["description"]
            doc += (
                f"## Scenario: {data['heading']}\n"
                f"{desc}\n"
                f"``` typespec\n{cleaned}\n```\n\n"
            )

        doc += "## Full Sample: \n// main.tsp\n``` typespec\n"
        doc += cls._remove_spector_content(main_spec) + "\n```\n"

        if client_tsp:
            doc += "// client.tsp\n``` typespec\n"
            doc += cls._remove_spector_content(client_tsp) + "\n```\n"

        return doc

    @classmethod
    async def _analyze_scenarios(
        cls, scenarios: list[str], spec: str
    ) -> AnalysisResult:
        """Analyze scenarios with a single LLM call."""
        scenarios_text = "\n".join(
            f"=== SCENARIO {i+1} ===\n{s}" for i, s in enumerate(scenarios)
        )

        prompt = f"""Analyze the following TypeSpec content and scenarios to extract structured information.

FULL SPEC CONTENT:
{spec}

SCENARIOS:
{scenarios_text}

Please provide a JSON response with the following structure:
{{
    "title": "A concise title from @scenarioService or @doc (one line only)",
    "scenarios": [
        {{
            "heading": "Title for scenario from @scenarioDoc or @doc (one line)",
            "description": "Description from @scenarioDoc or @doc (exclude 'expected' test results)"
        }}
    ]
}}

Requirements:
- Extract title from @scenarioService or @doc closest to @scenarioService only
- Headings should be one line suitable for markdown headers
- Descriptions should exclude 'expected' test results
- If description is same as heading, make description empty string
- Provide exactly {len(scenarios)} scenario objects
- Return only valid JSON"""

        response = await cls._get_chat_completion(prompt)
        # Parse JSON response
        clean = response.strip()
        if clean.startswith("```json"):
            clean = clean.removeprefix("```json").removesuffix("```").strip()
        elif clean.startswith("```"):
            clean = clean.removeprefix("```").removesuffix("```").strip()

        import json

        data = json.loads(clean)
        return AnalysisResult(
            title=data.get("title", "Unknown"),
            scenarios=data.get("scenarios", []),
        )

    @classmethod
    async def _get_chat_completion(cls, question: str) -> str:
        """Get chat completion with retry logic."""
        if not cls._client:
            raise RuntimeError("OpenAI client not initialized")

        deployment = os.environ.get("AOAI_CHAT_REASONING_MODEL", "")

        for attempt in range(MAX_RETRIES + 1):
            try:
                response = await cls._client.chat.completions.create(
                    messages=[
                        {
                            "role": "system",
                            "content": "You are a TypeSpec expert. Extract structured information and return only valid JSON.",
                        },
                        {"role": "user", "content": question},
                    ],
                    model=deployment,
                )
                if response.choices and response.choices[0].message.content:
                    return response.choices[0].message.content
                break
            except Exception as e:
                err_str = str(e)
                if ("429" in err_str or "Too Many Requests" in err_str) and attempt < MAX_RETRIES:
                    delay = min(BASE_DELAY * (2**attempt), MAX_DELAY)
                    logger.warning("Rate limit (attempt %d), retrying in %.1fs...", attempt + 1, delay)
                    await asyncio.sleep(delay)
                    continue
                raise

        raise RuntimeError(f"Failed to get response after {MAX_RETRIES + 1} attempts")

    # --- Text processing utilities ---

    @classmethod
    def _get_specs(cls, root: str) -> tuple[list[str], list[str]]:
        """Walk directory tree and collect .tsp file contents and paths."""
        specs: list[str] = []
        paths: list[str] = []

        for dirpath, _, filenames in os.walk(root):
            for fname in filenames:
                if not fname.endswith(".tsp"):
                    continue
                full_path = os.path.join(dirpath, fname)
                if any(ignored in full_path for ignored in IGNORED_SPECS):
                    continue
                try:
                    with open(full_path, encoding="utf-8") as f:
                        specs.append(f.read())
                    paths.append(full_path)
                except OSError as e:
                    logger.warning("Failed to read %s: %s", full_path, e)

        return specs, paths

    @classmethod
    def _get_scenarios(cls, search_str: str, spec: str) -> list[str]:
        """Extract scenario blocks from spec content."""
        indexes = cls._find_indexes(search_str, spec)
        scenarios = []
        for i, start in enumerate(indexes):
            end = indexes[i + 1] if i + 1 < len(indexes) else len(spec)
            scenarios.append(spec[start:end])
        return scenarios

    @classmethod
    def _find_indexes(cls, search_str: str, spec: str) -> list[int]:
        """Find starting indexes of each scenario block."""
        indexes: list[int] = []
        start = 0
        while True:
            pos = spec.find(search_str, start)
            if pos == -1:
                break
            # Walk back to find preceding blank line
            block_start = pos
            for ind in range(pos - 1, 0, -1):
                if spec[ind] == "\n" and ind > 0 and spec[ind - 1] == "\n":
                    block_start = ind + 1
                    break
            indexes.append(block_start)
            start = pos + len(search_str)
        return indexes

    @classmethod
    def _remove_spector_content(cls, content: str) -> str:
        """Remove spector annotations from TypeSpec content."""
        result = content
        # @scenarioDoc patterns
        result = re.sub(r'@scenarioDoc\("[\s\S]*?"\)\n', "", result)
        result = re.sub(r'@scenarioDoc\("""[\s\S]*?"""\)\n', "", result)
        # @scenarioService patterns
        result = re.sub(r'@scenarioService\("[\s\S]*?"\)\n', "", result)
        result = re.sub(r"@scenarioService\(\n[\s\S]*?\n\)\n", "", result)
        # Other
        result = result.replace("@scenario", "")
        result = re.sub(r'import "@typespec/spector";\n', "", result)
        result = re.sub(r"using Spector;\n", "", result)
        # Remove #suppress and missing-scenario lines
        lines = result.split("\n")
        lines = [l for l in lines if "#suppress " not in l and "missing-scenario" not in l]
        return "\n".join(lines)
