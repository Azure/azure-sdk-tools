import os
import pathlib

import dotenv
from azure.ai.evaluation import evaluate, SimilarityEvaluator

from custom_eval import CustomAPIViewEvaluator, review_apiview

dotenv.load_dotenv()

# needed for SimilarityEvaluator which is an AI-assisted evaluation
model_config = {
    "azure_endpoint": os.environ.get("AZURE_OPENAI_ENDPOINT"),
    "api_key": os.environ.get("AZURE_OPENAI_API_KEY"),
    "azure_deployment": "gpt-4o",
    "api_version": "2025-01-01-preview",
}

custom_eval = CustomAPIViewEvaluator()
similarity_eval = SimilarityEvaluator(model_config=model_config)


if __name__ == "__main__":
    path = pathlib.Path(__file__).parent / "tests" / "python.jsonl"
    result = evaluate(
        data=str(path),
        evaluators={
            "custom_eval": custom_eval,
            "similarity": similarity_eval,
        },
        evaluator_config={
            "similarity": {
                "column_mapping": {
                    "response": "${target.response}",
                    "query": "${data.query}",
                    "language": "${data.language}",
                    "ground_truth": "${data.response}",
                },
            },
            "custom_eval": {
                "column_mapping": {
                    "response": "${data.response}",
                    "query": "${data.query}",
                    "language": "${data.language}",
                    "output": "${target.response}"
                },
            }
        },
        target=review_apiview,
        # TODO we can send data to our foundry project for history / more graphical insights
        # azure_ai_project={
        #     "subscription_id": os.environ.get("AZURE_SUBSCRIPTION_ID"),
        #     "resource_group_name": os.environ.get("AZURE_FOUNDRY_RESOURCE_GROUP"),
        #     "project_name": os.environ.get("AZURE_FOUNDRY_PROJECT_NAME"),
        # }
    )
    print(result)
