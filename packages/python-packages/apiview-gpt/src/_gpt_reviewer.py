import os
import json
from langchain.chains import LLMChain
from langchain.prompts import ChatPromptTemplate, SystemMessagePromptTemplate, HumanMessagePromptTemplate
from langchain.chat_models import AzureChatOpenAI
from langchain.output_parsers import PydanticOutputParser
import openai
import re
from typing import List, Union, Dict, Any, Optional

from ._sectioned_document import SectionedDocument, Section
from ._models import GuidelinesResult, Violation
from ._vector_db import VectorDB

if "APPSETTING_WEBSITE_SITE_NAME" not in os.environ:
    # running on dev machine, loadenv
    import dotenv
    dotenv.load_dotenv()

openai.api_type = "azure"
openai.api_base = os.getenv("OPENAI_API_BASE")
openai.api_key = os.getenv("OPENAI_API_KEY")

OPENAI_API_VERSION = "2023-05-15"

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_GUIDELINES_FOLDER = os.path.join(_PACKAGE_ROOT, "guidelines")


class GptReviewer:

    def __init__(self, log_prompts: bool = False):
        self.llm = AzureChatOpenAI(client=openai.ChatCompletion, deployment_name="gpt-4", openai_api_version=OPENAI_API_VERSION, temperature=0)
        self.output_parser = PydanticOutputParser(pydantic_object=GuidelinesResult)
        if log_prompts:
            # remove the folder if it exists
            base_path = os.path.join(_PACKAGE_ROOT, "scratch", "prompts")
            if os.path.exists(base_path):
                import shutil
                shutil.rmtree(base_path)
            os.makedirs(base_path)
            os.environ["APIVIEW_LOG_PROMPT"] = str(log_prompts)
            os.environ["APIVIEW_PROMPT_INDEX"] = "0"

        system_prompt = SystemMessagePromptTemplate.from_template("""
You are trying to analyze an API for {language} to determine whether it meets the SDK guidelines.
We only provide one class at a time right now, but if you need it, here's a list of all the classes in this API:
{class_list}
""")
        human_prompt = HumanMessagePromptTemplate.from_template("""
Given the following guidelines:
{guidelines}

Evaluate the following class for any violations:
```
{apiview}
```
                                                                
{format_instructions}
""")
        prompt_template = ChatPromptTemplate.from_messages([system_prompt, human_prompt])
        self.chain = LLMChain(llm=self.llm, prompt=prompt_template)

    def _hash(self, obj) -> str:
        return str(hash(json.dumps(obj)))

    def get_response(self, apiview, language):
        apiview = self.unescape(apiview)
        class_list = self.get_class_list(apiview)

        all_guidelines = self.retrieve_guidelines(language)

        chunked_apiview = SectionedDocument(apiview.splitlines(), chunk=True)

        final_results = GuidelinesResult(status="Success", violations=[])
        for chunk in chunked_apiview.sections:
            if self.should_evaluate(chunk):
                # retrieve the most similar comments to identify guidelines to check
                semantic_matches = VectorDB().search_documents(language, chunk)
                
                guidelines_to_check = []
                extra_comments = {}

                # extract the unique guidelines to include in the prompt grounding.
                # documents not included in the prompt grounding will be treated as extra comments.
                for match in semantic_matches:

                    comment_model = match["aiCommentModel"]
                    if comment_model["isDeleted"] == True:
                        continue

                    guideline_ids = comment_model["guidelineIds"]
                    goodCode = comment_model["goodCode"]
                    comment = comment_model["comment"]

                    if guideline_ids:
                        guidelines_to_check.extend(guideline_ids)

                    # remove unnecessary or empty fields to conserve tokens and not confuse the AI
                    del comment_model["language"]
                    del comment_model["embedding"]
                    del comment_model["guidelineIds"]
                    del comment_model["changeHistory"]
                    del comment_model["isDeleted"]
                    if not comment_model["goodCode"]:
                        del comment_model["goodCode"]
                    if not comment_model["comment"]:
                        del comment_model["comment"]

                    if goodCode or comment:
                        extra_comments[self._hash(comment_model)] = comment_model
                    if not goodCode and not comment and not guideline_ids:
                        comment_model["comment"] = "Please have an architect look at this."
                        extra_comments[self._hash(comment_model)] = comment_model
                guidelines_to_check = list(set(guidelines_to_check))
                if not guidelines_to_check:
                    continue
                guidelines = self.select_guidelines(all_guidelines, guidelines_to_check)

                # append the extra comments to the list of guidelines to treat them equally.
                guidelines.extend(list(extra_comments.values()))

                params = {
                    "apiview": str(chunk),
                    "guidelines": guidelines,
                    "language": language,
                    "class_list": class_list,
                    "format_instructions": self.output_parser.get_format_instructions()
                }
                results = self.chain.run(**params)
                output = self.output_parser.parse(results)
                final_results.violations.extend(self.process_violations(output.violations, chunk))
                # FIXME see: https://github.com/Azure/azure-sdk-tools/issues/6571
                if len(output.violations) > 0:
                    final_results.status = "Error"
        self.process_rule_ids(final_results, all_guidelines)
        return final_results

    """ Ensure that each rule ID matches with an actual guideline ID. 
        This ensures that the links that appear in APIView should never be broken (404).
    """
    def process_rule_ids(self, results, guidelines):
        # create an index for easy lookup
        index = { x["id"]: x for x in guidelines }
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
                    print(f"WARNING: Rule ID {rule_id} not found. Possible hallucination.")
            # update the rule_ids arrays with the new values. Don't modify the array while iterating over it!
            for rule_id in to_remove:
                violation.rule_ids.remove(rule_id)
            for rule_id in to_add:
                violation.rule_ids.append(rule_id)

    def unescape(self, text: str) -> str:
        return str(bytes(text, "utf-8").decode("unicode_escape"))

    def process_violations(self, violations: List[Violation], section: Section) -> List[Violation]:
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
        return [x for x in combined_violations.values()]

    def find_line_number(self, chunk: Section, bad_code: str) -> Union[int, None]:
        offset = chunk.start_line_no
        line_no = None
        for i, line in enumerate(chunk.lines):
            # FIXME: see: https://github.com/Azure/azure-sdk-tools/issues/6572
            if line.strip().startswith(bad_code.strip()):
                if line_no is None:
                    line_no = offset + i
                else:
                    print(f"WARNING: Found multiple instances of bad code, default to first: {bad_code}")
        # FIXME: see: https://github.com/Azure/azure-sdk-tools/issues/6572
        if not line_no:
            print(f"WARNING: Could not find bad code. Trying less precise method: {bad_code}")
            for i, line in enumerate(chunk.lines):
                if bad_code.strip().startswith(line.strip()):
                    if line_no is None:
                        line_no = offset + i
                    else:
                        print(f"WARNING: Found multiple instances of bad code, default to first: {bad_code}")
        return line_no

    def should_evaluate(self, chunk: Section):
        for line in chunk.lines:
            if not line.strip().startswith("#") and not line.strip() == "":
                return True
        return False

    def select_guidelines(self, all, select_ids):
        return [guideline for guideline in all if guideline["id"] in select_ids]

    def retrieve_guidelines(self, language):
        general_guidelines = []
        general_guidelines_path = os.path.join(_GUIDELINES_FOLDER, "general")
        language_guidelines_path = os.path.join(_GUIDELINES_FOLDER, language)
        for filename in os.listdir(general_guidelines_path):
            with open(os.path.join(general_guidelines_path, filename), "r") as f:
                items = json.loads(f.read())
                general_guidelines.extend(items)

        language_guidelines = []
        for filename in os.listdir(language_guidelines_path):
            with open(os.path.join(language_guidelines_path, filename), "r") as f:
                items = json.loads(f.read())
                language_guidelines.extend(items)
        return general_guidelines + language_guidelines

    def get_class_list(self, apiview) -> List[str]:
        return re.findall(r'class ([\w\.]+)', apiview)


# custom monkey patch to save the prompts
def _custom_generate(
    self,
    input_list: List[Dict[str, Any]],
    run_manager: Optional["CallbackManagerForChainRun"] = None,
) -> "LLMResult":
    """Generate LLM result from inputs."""
    prompts, stop = self.prep_prompts(input_list, run_manager=run_manager)
    log_prompts = os.getenv("APIVIEW_LOG_PROMPT", "False").lower() == "true"
    if log_prompts:
        base_path = os.path.join(_PACKAGE_ROOT, "scratch", "prompts")
        for prompt in prompts:
            request_no = os.environ.get("APIVIEW_PROMPT_INDEX", 0)
            filepath = os.path.join(base_path, f"prompt_{request_no}.txt")
            with open(filepath, "w") as f:
                for message in prompt.messages:
                    f.write(f"==={message.type.upper()}===\n")
                    f.write(message.content + "\n")
            os.environ["APIVIEW_PROMPT_INDEX"] = str(int(request_no) + 1)
    return self.llm.generate_prompt(
        prompts,
        stop,
        callbacks=run_manager.get_child() if run_manager else None,
        **self.llm_kwargs,
    )
LLMChain.generate = _custom_generate