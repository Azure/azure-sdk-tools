import argparse
from datetime import datetime
import logging
import os
import sys
from _evals_runner import EvalsRunner, EvaluatorClass
from dotenv import load_dotenv
from azure.ai.evaluation import evaluate, SimilarityEvaluator, GroundednessEvaluator, ResponseCompletenessEvaluator
from azure.identity import AzurePipelinesCredential, DefaultAzureCredential, AzureCliCredential
from _evals_result import EvalsResult
from eval.evaluator import AzureBotEvaluator

if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
    logging.info("🚀 Starting evaluation ...")

    parser = argparse.ArgumentParser(description="Run evals for Azure Chat Bot.")

    parser.add_argument("--test_folder", type=str, help="the path to the test folder")
    parser.add_argument("--prefix", type=str, help="Process only files starting with this prefix")
    parser.add_argument("--is_bot", type=str, default="True", help="Use bot API for processing Q&A pairs (True/False)")
    parser.add_argument("--is_ci", type=str, default="True", help="Run in CI/CD pipeline (True/False)")
    parser.add_argument("--evaluation_name_prefix", type=str, help="the prefix of evaluation name")
    parser.add_argument("--send_result", type=str, default="True", help="Send the evaluation result to AI foundry project")
    parser.add_argument("--baseline_check", type=str, default="True", help="Compare the result with baseline.")
    parser.add_argument("--retrieve_response", type=str, default="True", help="Call bot api to retrieve response.")
    parser.add_argument("--cache_result", type=str, default="False", help="cache the evaluation result persistently.")
    parser.add_argument("--evaluators", type=str, help="choose evaluators to run, string separated by comma")
    args = parser.parse_args()

    args.is_bot = args.is_bot.lower() in ('true', '1', 'yes', 'on')
    args.is_ci = args.is_ci.lower() in ('true', '1', 'yes', 'on')
    args.send_result = args.send_result.lower() in ('true', '1', 'yes')
    args.baseline_check = args.baseline_check.lower() in ('true', '1', 'yes')
    args.retrieve_response = args.retrieve_response.lower() in ('true', '1', 'yes')
    args.cache_result = args.cache_result.lower() in ('true', '1', 'yes')
    args.evaluators = args.evaluators.split(',') if args.evaluators is not None else None

    
    script_directory = os.path.dirname(os.path.abspath(__file__))
    logging.info(f"Script directory:{script_directory}")

    
    current_file_path = os.getcwd()
    logging.info(f"Current working directory:{current_file_path}")


    if (args.test_folder is None):
        args.test_folder = os.path.join(script_directory, "tests")
    
    logging.info(f"test folder: {args.test_folder}")
    # Required environment variables
    load_dotenv() 
    
    all_results = {}
    try: 
        logging.info("📊 Preparing dataset...")
        azure_ai_project_endpoint = os.environ["AZURE_AI_PROJECT_ENDPOINT"]
        logging.info(f"📋 Using project endpoint: {azure_ai_project_endpoint}")
        model_config: dict[str, str] = {
            "azure_endpoint": os.environ["AZURE_OPENAI_ENDPOINT"],
            "api_key": os.environ["AZURE_OPENAI_API_KEY"],
            "azure_deployment": os.environ["AZURE_EVALUATION_MODEL_NAME"],
            "api_version": os.environ["AZURE_API_VERSION"],
        }

        similarity_threshold = int(os.environ.get("SIMILARITY_THRESHOLD", "3"))
        simialirty_evaluator = SimilarityEvaluator(model_config=model_config, threshold=similarity_threshold)
        groundedness_evaluator = GroundednessEvaluator(model_config=model_config)
        simiarity_class = EvaluatorClass("similarity", simialirty_evaluator, {"column_mapping": {
            "query": "${data.query}",
            "response": "${data.response}",
            "ground_truth": "${data.ground_truth}",
            "testcase": "${data.testcase}"
        }})

        groundedness_class = EvaluatorClass("groundedness", groundedness_evaluator, {"column_mapping": {
            "query": "${data.query}",
            "response": "${data.response}",
            "context": "${data.context}",
            "testcase": "${data.testcase}"
        }})

        response_completion_evaluator = ResponseCompletenessEvaluator(model_config=model_config)
        response_completion_class = EvaluatorClass("response_completeness", response_completion_evaluator, {"column_mapping": {
            "response": "${data.response}",
            "ground_truth": "${data.ground_truth}",
            "testcase": "${data.testcase}"
        }})

        qa_evaluator = AzureBotEvaluator(model_config=model_config, threshold = similarity_threshold)
        qa_evaluator_class = EvaluatorClass("bot_evals", qa_evaluator, {"column_mapping": {
            "response": "${data.response}",
            "ground_truth": "${data.ground_truth}",
            "expected_reference_urls": "${data.expected_reference_urls}",
            "reference_urls": "${data.reference_urls}",
            "testcase": "${data.testcase}"
        }},
        [
            "bot_evals",
            "bot_evals_similarity",
            "bot_evals_response_completeness",
            "bot_evals_result"
        ])

        evaluators = {
            "similarity": simiarity_class,
            "groundedness": groundedness_class,
            "response_completeness": response_completion_class,
            "bot_evals": qa_evaluator_class
        }

        
        metrics = {}
        evals = {}
        if args.evaluators:
            evals = {eval_key: evaluators[eval_key] for eval_key in args.evaluators}
            metrics = {eval_key: evaluators[eval_key].output_fileds for eval_key in args.evaluators}
        else:
            evals = evaluators
            metrics = {eval_key: evaluators[eval_key].output_fileds for eval_key in evaluators.keys()}

        weights: dict[str, float] = {
            "similarity_weight": 0.6,  # Similarity between expected and actual
            "groundedness_weight": 0.4,  # Staying grounded in guidelines
            "response_completeness_weight": 0.4,
        }
        eval_result = EvalsResult(weights=weights, metrics=metrics)
        
        evals_runner = EvalsRunner(evaluators=evals, evals_result=eval_result)

        kwargs = {}
        if args.send_result:
            if args.is_ci:
                kwargs = {
                    "credential": DefaultAzureCredential()
                }
            else:
                kwargs = {
                    # run in local, use Azure Cli Credential, make sure you already run `az login`
                    "credential": AzureCliCredential()
                }
        
        all_results = evals_runner.evaluate_run(args.test_folder, args.prefix, args.retrieve_response, args.evaluation_name_prefix, azure_ai_project_endpoint if args.send_result else None, **kwargs)

        if (args.cache_result) :
            now = datetime.now()
            result_file = open(os.path.join(script_directory, f"evaluate-result-{now.strftime('%Y-%m-%d-%H-%S')}"), 'a', encoding='utf-8')
            logging.info(f"all_results:{len(all_results.keys())}")
            for name, test_results in all_results.items():
                result_file.write(f"\n-----------{name}----------------------\n")
                result_file.write(evals_runner.evals_result.build_output_table(test_results))
            result_file.flush()
            result_file.close()

        evals_runner.evals_result.show_results(all_results, args.baseline_check)
        if args.baseline_check:
            evals_runner.evals_result.establish_baseline(all_results, args.is_ci)
        isPass = evals_runner.evals_result.verify_results(all_results, args.baseline_check)
        if not isPass:
            sys.exit(1)
    except Exception as e:
        logging.info(f"❌ Error occurred: {str(e)}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
