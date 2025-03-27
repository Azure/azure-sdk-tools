import os
import json
import openai
import dotenv
from typing import List, Union

from azure.identity import DefaultAzureCredential, get_bearer_token_provider

from ._sectioned_document import SectionedDocument, Section
from ._models import GuidelinesResult, Violation

if "APPSETTING_WEBSITE_SITE_NAME" not in os.environ:
    # running on dev machine, loadenv
    import dotenv
    dotenv.load_dotenv()

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_GUIDELINES_FOLDER = os.path.join(_PACKAGE_ROOT, "guidelines")


class GptReviewer:

    def __init__(self, log_prompts: bool = False):
        # FIXME: Hook prompty up
        # result = prompty.execute(prompt_file_path, inputs={"question": payload})
        self.client = openai.AzureOpenAI(
            azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
            azure_ad_token_provider=get_bearer_token_provider(DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default"),
            api_version="2025-01-01-preview"
        )
        self.output_parser = GuidelinesResult
        if log_prompts:
            # remove the folder if it exists
            base_path = os.path.join(_PACKAGE_ROOT, "scratch", "prompts")
            if os.path.exists(base_path):
                import shutil

                shutil.rmtree(base_path)
            os.makedirs(base_path)
            os.environ["APIVIEW_LOG_PROMPT"] = str(log_prompts)
            os.environ["APIVIEW_PROMPT_INDEX"] = "0"

        self.system_prompt = """
You are an expert code reviewer for SDKs. You will analyze an entire client library apiview surface for {language} to determine whether it meets the SDK guidelines. ONLY mention if the library is clearly and visibly violating a guideline. Be conservative - DO NOT make assumptions that a guideline is being violated because it is possible that all guidelines are being followed. Evaluate each
piece of code against all guidelines. Code may violate multiple guidelines. Some additional notes: each class will contain its namespace, like class azure.contoso.ClassName where 'azure.contoso' is the namespace and ClassName is the name of the class. The apiview will not contain runnable code, it is meant to be a high-level {language} pseudocode summary of a client library surface.
Format instructions: 'rule_ids' should contain the unique rule ID or IDs that were violated. 'line_no' should contain the line number of the violation, if known. 'bad_code' should contain the original code that was bad, cited verbatim. It should contain a single line of code. 'suggestion' should contain the suggested {language} code which fixes the bad code. If code is not feasible, a description is fine. 'comment' should contain a comment about the violation.
"""
        self.human_prompt = """
Given the following guidelines:
{guidelines}

Evaluate the apiview for any violations:
```
{apiview}
```

"""

    def _hash(self, obj) -> str:
        return str(hash(json.dumps(obj)))

    def get_response(self, apiview, language):
        apiview = self.unescape(apiview)

        guidelines = self.retrieve_guidelines(language)

        chunked_apiview = SectionedDocument(apiview.splitlines(), chunk=False)

        final_results = GuidelinesResult(status="Success", violations=[])

        extra_comments = {}

        guidelines.extend(list(extra_comments.values()))
        full_apiview = "\n".join(chunked_apiview.sections[0].lines)
        system_prompt = self.system_prompt.format(
            language=language, apiview=full_apiview
        )
        human_prompt = self.human_prompt.format(
            guidelines=json.dumps(guidelines), apiview=full_apiview
        )

        results = self.client.beta.chat.completions.parse(
            model="o3-mini",
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": human_prompt},
            ],
            response_format=GuidelinesResult,
        )

        output = results.choices[0].message.parsed
        final_results.violations.extend(
            self.process_violations(output.violations, chunked_apiview.sections[0])
        )
        # FIXME see: https://github.com/Azure/azure-sdk-tools/issues/6571
        if len(output.violations) > 0:
            final_results.status = "Error"
        self.process_rule_ids(final_results, guidelines)
        return final_results

    def process_rule_ids(self, results, guidelines):
        """Ensure that each rule ID matches with an actual guideline ID.
        This ensures that the links that appear in APIView should never be broken (404).
        """
        # create an index for easy lookup
        index = {x["id"]: x for x in guidelines}
        for violation in results.violations:
            to_remove = []
            to_add = []
            for rule_id in violation.rule_ids:
                try:
                    index[rule_id]
                    continue
                except KeyError:
                    # see if any guideline ID ends with the rule_id. If so, update it and preserve in the index
                    matched = False
                    for guideline in guidelines:
                        if guideline["id"].endswith(rule_id):
                            to_remove.append(rule_id)
                            to_add.append(guideline["id"])
                            index[rule_id] = guideline["id"]
                            matched = True
                            break
                    if matched:
                        continue
                    # no match or partial match found, so remove the rule_id
                    to_remove.append(rule_id)
                    print(
                        f"WARNING: Rule ID {rule_id} not found. Possible hallucination."
                    )
            # update the rule_ids arrays with the new values. Don't modify the array while iterating over it!
            for rule_id in to_remove:
                violation.rule_ids.remove(rule_id)
            for rule_id in to_add:
                violation.rule_ids.append(rule_id)

    def unescape(self, text: str) -> str:
        return str(bytes(text, "utf-8").decode("unicode_escape"))

    def process_violations(
        self, violations: List[Violation], section: Section
    ) -> List[Violation]:
        if not violations:
            return violations

        combined_violations = {}
        for violation in violations:
            line_no = self.find_line_number(section, violation.bad_code)
            violation.line_no = line_no
            # FIXME see: https://github.com/Azure/azure-sdk-tools/issues/6590
            if not line_no:
                continue
            existing = combined_violations.get(line_no, None)
            if existing:
                for rule_id in violation.rule_ids:
                    if rule_id not in existing.rule_ids:
                        existing.rule_ids.append(rule_id)
                        if existing.suggestion != violation.suggestion:
                            # FIXME: Collect all suggestions and use the most popular??
                            existing.suggestion = violation.suggestion
                        existing.comment = existing.comment + " " + violation.comment
            else:
                combined_violations[line_no] = violation
        return [x for x in combined_violations.values() if x.line_no != 1]

    def find_line_number(self, chunk: Section, bad_code: str) -> Union[int, None]:
        offset = chunk.start_line_no
        line_no = None
        for i, line in enumerate(chunk.lines):
            # FIXME: see: https://github.com/Azure/azure-sdk-tools/issues/6572
            if line.strip().startswith(bad_code.strip()):
                if line_no is None:
                    line_no = offset + i
                else:
                    print(
                        f"WARNING: Found multiple instances of bad code, default to first: {bad_code}"
                    )
        # FIXME: see: https://github.com/Azure/azure-sdk-tools/issues/6572
        if not line_no:
            print(
                f"WARNING: Could not find bad code. Trying less precise method: {bad_code}"
            )
            for i, line in enumerate(chunk.lines):
                if bad_code.strip().startswith(line.strip()):
                    if line_no is None:
                        line_no = offset + i
                    else:
                        print(
                            f"WARNING: Found multiple instances of bad code, default to first: {bad_code}"
                        )
        return line_no

    def select_guidelines(self, all, select_ids):
        return [guideline for guideline in all if guideline["id"] in select_ids]

    def retrieve_guidelines(self, language, include_general_guidelines: bool = False):
        general_guidelines = []
        if include_general_guidelines:
            general_guidelines_path = os.path.join(_GUIDELINES_FOLDER, "general")
            for filename in os.listdir(general_guidelines_path):
                with open(os.path.join(general_guidelines_path, filename), "r") as f:
                    items = json.loads(f.read())
                    general_guidelines.extend(items)

        language_guidelines = []
        language_guidelines_path = os.path.join(_GUIDELINES_FOLDER, language)
        for filename in os.listdir(language_guidelines_path):
            with open(os.path.join(language_guidelines_path, filename), "r") as f:
                items = json.loads(f.read())
                language_guidelines.extend(items)
        return general_guidelines + language_guidelines
