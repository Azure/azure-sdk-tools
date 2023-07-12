import os
import dotenv
import json
from langchain.chains import LLMChain
from langchain.prompts import PromptTemplate
from langchain.chat_models import AzureChatOpenAI
from langchain.output_parsers import PydanticOutputParser
import openai

from ._models import GuidelinesResult

dotenv.load_dotenv()

openai.api_type = "azure"
openai.api_base = os.getenv("OPENAI_API_BASE")
openai.api_key = os.getenv("OPENAI_API_KEY")

OPENAI_API_VERSION = "2023-05-15"

class GptReviewer:
    def __init__(self):
        self.llm = AzureChatOpenAI(deployment_name="gpt-4", openai_api_version=OPENAI_API_VERSION)
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
        # FIXME: Restore this
        # general_guidelines, language_guidelines = self.retrieve_guidelines(language)
        # all_guidelines = general_guidelines + language_guidelines

        # FIXME: Restore this
        # guidelines = self.select_guidelines(all_guidelines, [
        #     "python-client-sync-async-separate-clients",
        #     "python-client-naming",
        #     "python-client-constructor-form",
        #     "python-client-options-naming",
        #     "python-codestyle-pep484"
        # ])

        for i, g in enumerate(guidelines):
            g["number"] = i

        results = self.chain.run(apiview=apiview, guidelines=guidelines, language=language)
        parsed = self.output_parser.parse(results)
        with open(os.path.join(os.path.dirname(__file__), "output.json"), "w") as f:
            f.write(parsed.json(indent=2))

    def select_guidelines(self, all, select_ids):
        return [guideline for guideline in all if guideline["id"] in select_ids]

    def retrieve_guidelines(self, language):
        general_guidelines = []
        general_guidelines_path = os.path.join(os.path.dirname(__file__), "..", "docs", "general")
        language_guidelines_path = os.path.join(os.path.dirname(__file__), "..", "docs", language)
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
