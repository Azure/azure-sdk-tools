import argparse
import dotenv
from _runner import EvalRunner

dotenv.load_dotenv()

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Run evaluations.")
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
        help="The number of runs to perform, with the median of results kept. Defaults to 1.",
    )
    parser.add_argument(
        "--test-file",
        "-t",
        type=str,
        required=True,
        help="Path to workflow YAML.",
    )
    parser.add_argument(
        "--testcase",
        "-c",
        type=str,
        help="Filter to run only the specified testcase (by testcase field value)."
    )
    args = parser.parse_args()
    runner = EvalRunner(
        language=args.language, 
        test_path=args.test_file,
        testcase=args.testcase,
        num_runs=args.num_runs
    )
    runner.run()
