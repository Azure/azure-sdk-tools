import argparse
import os

import dotenv
from evals._custom import EvalRunner

# set before azure.ai.evaluation import to make PF output less noisy
os.environ["PF_LOGGING_LEVEL"] = "CRITICAL"

dotenv.load_dotenv()

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Run evals for APIview copilot.")
    parser.add_argument(
        "--language",
        "-l",
        type=str,
        default="python",
        help="The language to run evals for. Defaults to python.",
    )
    parser.add_argument(
        "--num-runs",
        "-n",
        type=int,
        default=1,
        help="The number of runs to perform, with the median of results kept. Defaults to 3.",
    )
    parser.add_argument(
        "--test-file",
        "-t",
        type=str,
        default="reviews.jsonl",
        help="Only run a particular jsonl test file, takes the name or path to the file. Defaults to all.",
    )
    args = parser.parse_args()
    runner = EvalRunner(language=args.language, test_path=args.test_file, num_runs=args.num_runs)
    runner.run()
