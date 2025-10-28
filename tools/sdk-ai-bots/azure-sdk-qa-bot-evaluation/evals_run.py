import argparse
from datetime import datetime
import logging
import os
import sys
from _evals_runner import EvalsRunner, EvaluatorClass
from dotenv import load_dotenv
from azure.ai.evaluation import evaluate, SimilarityEvaluator, GroundednessEvaluator
from azure.identity import AzurePipelinesCredential, DefaultAzureCredential, AzureCliCredential
from _evals_result import build_output_table, establish_baseline, show_results, verify_results

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
        similarity_threshold = os.environ.get("SIMILARITY_THRESHOLD", 3)
        simialirty_evaluator = SimilarityEvaluator(model_config=model_config, threshold=similarity_threshold)
        groundedness_evaluator = GroundednessEvaluator(model_config=model_config)
        simiarity_class = EvaluatorClass("similarity", simialirty_evaluator, {"column_mapping": {
            "query": "${data.query}",
            "response": "${data.response}",
            "ground_truth": "${data.ground_truth}",
            "testcase": "${data.testcase}"
        }})

        groundedness_class = EvaluatorClass("similarity", groundedness_evaluator, {"column_mapping": {
            "query": "${data.query}",
            "response": "${data.response}",
            "context": "${data.context}",
            "testcase": "${data.testcase}"
        }})
        evaluators = {
            "similarity": simiarity_class,
            "groundedness": groundedness_class
        }
        # evaluators = {
        #     "similarity": EvaluatorClass("similarity", simialirty_evaluator, {"column_mapping": {
        #                                                                                         "query": "${data.query}",
        #                                                                                         "response": "${data.response}",
        #                                                                                         "ground_truth": "${data.ground_truth}",
        #                                                                                         "testcase": "${data.testcase}"
        #                                                                                     }}),
        #     "groundedness": EvaluatorClass("groundedness", groundedness_evaluator, {"column_mapping": {
        #                                                                                             "query": "${data.query}",
        #                                                                                             "response": "${data.response}",
        #                                                                                             "context": "${data.context}",
        #                                                                                             "testcase": "${data.testcase}"
        #                                                                                         }})
        #     }
        evals_runner = EvalsRunner(evaluators)

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
        
        all_results = evals_runner.evaluate_run(args.test_folder, args.prefix, args.retrieve_response, args.evaluators, args.evaluation_name_prefix, azure_ai_project_endpoint if args.send_result else None, **kwargs)
    except Exception as e:
        logging.info(f"❌ Error occurred: {str(e)}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
    
    if (args.cache_result) :
        now = datetime.now()
        result_file = open(os.path.join(script_directory, f"evaluate-result-{now.strftime("%Y-%m-%d-%H-%S")}"), 'a', encoding='utf-8')
        logging.info(f"all_results:{len(all_results.keys())}")
        for name, test_results in all_results.items():
            result_file.write(f"\n-----------{name}----------------------\n")
            # for result in test_results[:-1]:  # Skip summary object
            #     testcase = result["testcase"]
            #     score = result["overall_score"]
            #     sim = result["similarity"]
            #     sim_result = result["similarity_result"]

            #     groundedness = result['groundedness']
            #     groundedness_result = result['groundedness_result']
            #     data = {
            #         "testcase": result["testcase"],
            #         "similarity": result["similarity"],
            #         "similarity_result": result["similarity_result"],
            #         "groundedness": result['groundedness'],
            #         "groundedness_result": result["similarity_result"]
            #     }
            #     result_file.write(json.dumps(data, ensure_ascii=False) + '\n')
            result_file.write(build_output_table(test_results))
        result_file.flush()
        result_file.close()
    
    show_results(all_results, args.baseline_check)
    if args.baseline_check:
        establish_baseline(all_results, args.is_cli)
    isPass = verify_results(all_results, args.baseline_check)
    if not isPass:
        sys.exit(1)