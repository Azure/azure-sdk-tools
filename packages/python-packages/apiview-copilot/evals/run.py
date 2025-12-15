import argparse
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
        "--use-recording",
        action="store_true",
        help="Use recordings instead of executing LLM calls to speed up runs. If recordings are not available, LLM calls will be made and saved as recordings.",
    )
    parser.add_argument(
        "--style",
        "-s",
        type=str,
        choices=["compact", "verbose"],
        default="compact",
        help="Choose whether to show only failing and partial test cases (compact) or to also show passing ones (verbose)",
    )
    args = parser.parse_args()
    targets = discover_targets(args.test_paths)
    runner = EvaluationRunner(num_runs=args.num_runs, use_recording=args.use_recording, verbose=(args.style == "verbose"))
    results = runner.run(targets)
    runner.show_results(results)
