import argparse
import os
import dotenv
from _discovery import discover_targets
from _runner import EvaluationRunner

dotenv.load_dotenv()

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Run evaluations.")
    parser.add_argument(
        "--num-runs",
        "-n",
        type=int,
        default=1,
        help="The number of runs to perform, with the median of results kept. Defaults to 1.",
    )
    parser.add_argument(
        "--test-paths",
        "-p",
        type=str,
        nargs="*",
        help="Paths to directories containing test files.",
    )
    parser.add_argument(
        "--use-cache",
        action="store_true",
        help="Enable caching of evaluation results to speed up runs.",
    )
    args = parser.parse_args()
    targets = discover_targets(args.test_paths)
    os.environ['EVALS_USE_CACHE'] = 'true' if args.use_cache else 'false'
    runner = EvaluationRunner(num_runs=args.num_runs)
    results = runner.run(targets)
    runner.show_results(results)
    runner.show_summary(results)
