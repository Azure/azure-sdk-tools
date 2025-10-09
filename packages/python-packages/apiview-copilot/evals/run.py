import argparse

import dotenv
from _discovery import EvaluationDiscovery
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
    args = parser.parse_args()
    targets = EvaluationDiscovery.discover_targets(args.test_paths)
    runner = EvaluationRunner()
    results = runner.run(targets)
    runner.show_summary(results)
