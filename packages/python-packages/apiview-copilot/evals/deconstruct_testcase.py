import argparse
import json
import pathlib



if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Deconstruct a test case for editing.")
    parser.add_argument(
        "--language",
        type=str,
        required=True,
        help="The language for the test case.",
    )
    parser.add_argument(
        "--test-file",
        type=str,
        required=True,
        help="The full path to the jsonl test file.",
    )
    parser.add_argument(
        "--test-case",
        type=str,
        required=True,
        help="The specific test case to deconstruct.",
    )

    args = parser.parse_args()
    language = args.language
    test_file = args.test_file
    test_case = args.test_case

    test_cases = {}
    with open(test_file, 'r') as f:
        for line in f:
            if line.strip():
                parsed = json.loads(line)
                if 'testcase' in parsed:
                    test_cases[parsed['testcase']] = parsed

    if test_case not in test_cases:
        raise ValueError(f"Test case '{test_case}' not found in the file.")

    apiview = test_cases[test_case].get('query', '')
    expected = test_cases[test_case].get('response', '')
    deconstructed_apiview = pathlib.Path(__file__).parent / "tests" / language / f"{test_case}.txt"
    deconstructed_expected = pathlib.Path(__file__).parent / "tests" / language / f"{test_case}.json"
    with open(deconstructed_apiview, 'w') as f:
        f.write(apiview)

    with open(deconstructed_expected, 'w') as f:
        f.write(expected)

    print(f"Deconstructed test case '{test_case}' into {deconstructed_apiview} and {deconstructed_expected}.")