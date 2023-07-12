import os
import dotenv
import json
from langchain.chains import LLMChain
from langchain.prompts import PromptTemplate
from langchain.chat_models import AzureChatOpenAI
from langchain.output_parsers import PydanticOutputParser
import openai
from typing import List

from ._sectioned_document import SectionedDocument, Section
from ._models import GuidelinesResult

dotenv.load_dotenv()

openai.api_type = "azure"
openai.api_base = os.getenv("OPENAI_API_BASE")
openai.api_key = os.getenv("OPENAI_API_KEY")

OPENAI_API_VERSION = "2023-05-15"

_PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
_GUIDELINES_FOLDER = os.path.join(_PACKAGE_ROOT, "guidelines")


class GptReviewer:
    def __init__(self):
        self.llm = AzureChatOpenAI(client=openai.ChatCompletion, deployment_name="gpt-4", openai_api_version=OPENAI_API_VERSION, temperature=0)
        self.output_parser = PydanticOutputParser(pydantic_object=GuidelinesResult)
        self.prompt_template = PromptTemplate(
            input_variables=["apiview", "guidelines", "language"],
            partial_variables={"format_instructions": self.output_parser.get_format_instructions()},
            template="""
                Given the following {language} Azure SDK Guidelines:
                  {guidelines}
                Verify whether the following code satisfies the guidelines:
                ```
                  {apiview}
                ```
                
                {format_instructions}
            """
        )
        self.chain = LLMChain(llm=self.llm, prompt=self.prompt_template)

    def get_response(self, apiview, language):
        general_guidelines, language_guidelines = self.retrieve_guidelines(language)
        all_guidelines = general_guidelines + language_guidelines

        guidelines = self.select_guidelines(all_guidelines, [
            "python-client-naming",
            "python-client-options-naming",
            "python-models-async",
            "python-models-dict-result",
            "python-models-enum-string",
            "python-models-enum-name-uppercase",
            "python-client-sync-async",
            "python-client-async-keywords",
            "python-client-separate-sync-async",
            "python-client-same-name-sync-async",
            "python-client-namespace-sync",
        ])

        for i, g in enumerate(guidelines):
            g["number"] = i

        chunked_apiview = SectionedDocument(apiview.splitlines(), chunk=True)

        final_results = GuidelinesResult(status="Success", violations=[])
        for chunk in chunked_apiview.sections:
            if self.should_evaluate(chunk):
                results = self.chain.run(apiview=str(chunk), guidelines=guidelines, language=language)
                output = self.output_parser.parse(results)
                # FIXME: Associate line numbers with violations
                final_results.violations.extend(output.violations)
                if output.status == "Error":
                    final_results.status = output.status
        return final_results

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
        return general_guidelines, language_guidelines
