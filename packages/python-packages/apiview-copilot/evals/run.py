import argparse

import dotenv
from _runner import EvalRunner

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
        nargs="+",
        required=True,
        help="Paths to directories containing test files.",
    )
    args = parser.parse_args()
    test_paths = args.test_paths
    for test_path in test_paths or [test_paths]:
        runner = EvalRunner(test_path=test_path, num_runs=args.num_runs)
        runner.run()
